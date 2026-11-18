// riskengine.cpp
// Moteur Monte Carlo de VaR et d'Expected Shortfall en C++17, multi-thread.
// Compilé en bibliothèque partagée (.dylib sur Mac, .so sous Linux, .dll sous Windows)
// puis appelé depuis C# (.NET 10) par P/Invoke.

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <random>
#include <thread>
#include <vector>

#if defined(_WIN32)
#define RE_API extern "C" __declspec(dllexport)
#else
#define RE_API extern "C" __attribute__((visibility("default")))
#endif

namespace {

// Décomposition de Cholesky d'une matrice de covariance (n x n, stockée par ligne).
// Renvoie false si la matrice n'est pas définie positive.
bool cholesky(const double* a, int n, std::vector<double>& L) {
    L.assign(static_cast<size_t>(n) * n, 0.0);
    for (int i = 0; i < n; ++i) {
        for (int j = 0; j <= i; ++j) {
            double sum = a[i * n + j];
            for (int k = 0; k < j; ++k) sum -= L[i * n + k] * L[j * n + k];
            if (i == j) {
                if (sum <= 0.0) return false;
                L[i * n + i] = std::sqrt(sum);
            } else {
                L[i * n + j] = sum / L[j * n + j];
            }
        }
    }
    return true;
}

}  // namespace

// Version du moteur (permet au C# de vérifier que la bibliothèque est bien chargée).
RE_API int re_version() { return 110; }

// Nombre de threads utilisés pour la simulation.
RE_API int re_threads() {
    unsigned hc = std::thread::hardware_concurrency();
    return static_cast<int>(std::max(1u, std::min(16u, hc == 0 ? 4u : hc)));
}

// Simulation Monte Carlo du P&L d'un portefeuille sur un jour.
//   mu, cov   : moyenne et covariance des rendements logarithmiques (n actifs)
//   exposure  : montant investi par actif (en euros)
//   dist      : 0 = loi normale multivariée, 1 = loi de Student multivariée (queues épaisses)
//   nu        : degrés de liberté de la Student (> 2)
//   outPnl    : tableau de taille nSims rempli avec les P&L simulés
// Code retour : 0 = succès, -1 = paramètres invalides, -2 = covariance non définie positive.
RE_API int re_mc_pnl(const double* mu, const double* cov, const double* exposure, int n,
                     int nSims, int dist, double nu, std::uint64_t seed, double* outPnl) {
    if (n <= 0 || nSims <= 0 || mu == nullptr || cov == nullptr || exposure == nullptr || outPnl == nullptr)
        return -1;
    if (dist == 1 && nu <= 2.0) return -1;

    std::vector<double> L;
    if (!cholesky(cov, n, L)) return -2;

    const int nThreads = re_threads();
    const int chunk = (nSims + nThreads - 1) / nThreads;
    std::vector<std::thread> pool;
    pool.reserve(nThreads);

    for (int t = 0; t < nThreads; ++t) {
        const int start = t * chunk;
        const int end = std::min(nSims, start + chunk);
        if (start >= end) break;
        pool.emplace_back([=, &L]() {
            std::mt19937_64 rng(seed + 0x9E3779B97F4A7C15ULL * static_cast<std::uint64_t>(t + 1));
            std::normal_distribution<double> gauss(0.0, 1.0);
            std::chi_squared_distribution<double> chi2(dist == 1 ? nu : 1.0);
            // Facteur qui ramène la variance de la Student à 1 (même covariance que la normale)
            const double scale = dist == 1 ? std::sqrt((nu - 2.0) / nu) : 1.0;
            std::vector<double> z(n);
            for (int s = start; s < end; ++s) {
                for (int i = 0; i < n; ++i) z[i] = gauss(rng);
                double mix = 1.0;
                if (dist == 1) mix = scale / std::sqrt(chi2(rng) / nu);
                double pnl = 0.0;
                for (int i = 0; i < n; ++i) {
                    double acc = 0.0;
                    const double* row = &L[static_cast<size_t>(i) * n];
                    for (int j = 0; j <= i; ++j) acc += row[j] * z[j];
                    const double r = mu[i] + mix * acc;
                    pnl += exposure[i] * std::expm1(r);
                }
                outPnl[s] = pnl;
            }
        });
    }
    for (auto& th : pool) th.join();
    return 0;
}

// VaR et Expected Shortfall d'une distribution de P&L (renvoyées en pertes positives).
//   varLevel : ex. 0.99   -> VaR 99 %
//   esLevel  : ex. 0.975  -> ES 97,5 % (norme FRTB)
RE_API int re_tail_stats(const double* pnl, int n, double varLevel, double esLevel,
                         double* outVar, double* outEs) {
    if (pnl == nullptr || n < 10 || outVar == nullptr || outEs == nullptr) return -1;
    std::vector<double> v(pnl, pnl + n);
    std::sort(v.begin(), v.end());
    // Quantile par interpolation linéaire
    const double h = (n - 1) * (1.0 - varLevel);
    const int lo = static_cast<int>(std::floor(h));
    const int hi = std::min(lo + 1, n - 1);
    *outVar = -(v[lo] + (h - lo) * (v[hi] - v[lo]));
    const int k = std::max(1, static_cast<int>(std::floor(n * (1.0 - esLevel))));
    double sum = 0.0;
    for (int i = 0; i < k; ++i) sum += v[i];
    *outEs = -sum / k;
    return 0;
}
