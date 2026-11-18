// volengine.cpp
// Moteur GARCH / GJR-GARCH en C++17 : log-vraisemblance et filtrage de la variance.
// Compilé en bibliothèque partagée puis appelé depuis C# (.NET 10) par P/Invoke.
// L'optimiseur (Nelder-Mead) est en C# et appelle ve_gjr_nll plusieurs centaines de fois
// par estimation : c'est la boucle la plus coûteuse du projet, d'où le C++.

#include <cmath>
#include <cstdint>

#if defined(_WIN32)
#define VE_API extern "C" __declspec(dllexport)
#else
#define VE_API extern "C" __attribute__((visibility("default")))
#endif

namespace {
constexpr double kLog2Pi = 1.8378770664093453;
constexpr double kPenalty = 1e12;
}  // namespace

VE_API int ve_version() { return 110; }

// Modèle : h(t+1) = omega + (alpha + gamma * 1[r(t) < 0]) * r(t)^2 + beta * h(t)
// (gamma = 0 donne le GARCH(1,1) classique)
// Renvoie la log-vraisemblance négative (gaussienne) des rendements r[0..n-1].
VE_API double ve_gjr_nll(const double* r, int n, double omega, double alpha, double gamma,
                         double beta, double h0) {
    if (r == nullptr || n < 2) return kPenalty;
    if (omega <= 0.0 || alpha < 0.0 || beta < 0.0 || alpha + gamma < 0.0) return kPenalty;
    if (alpha + beta + 0.5 * gamma >= 1.0) return kPenalty;
    double h = h0;
    double nll = 0.0;
    for (int t = 0; t < n; ++t) {
        if (!(h > 0.0) || !std::isfinite(h)) return kPenalty;
        const double r2 = r[t] * r[t];
        nll += 0.5 * (kLog2Pi + std::log(h) + r2 / h);
        const double a = alpha + (r[t] < 0.0 ? gamma : 0.0);
        h = omega + a * r2 + beta * h;
    }
    return nll;
}

// Filtre la variance conditionnelle : hOut doit avoir n + 1 cases.
// hOut[t] = variance prévue pour le jour t, hOut[n] = prévision pour le jour suivant la dernière donnée.
VE_API int ve_gjr_filter(const double* r, int n, double omega, double alpha, double gamma,
                         double beta, double h0, double* hOut) {
    if (r == nullptr || hOut == nullptr || n < 1) return -1;
    double h = h0;
    for (int t = 0; t < n; ++t) {
        hOut[t] = h;
        const double a = alpha + (r[t] < 0.0 ? gamma : 0.0);
        h = omega + a * r[t] * r[t] + beta * h;
    }
    hOut[n] = h;
    return 0;
}
