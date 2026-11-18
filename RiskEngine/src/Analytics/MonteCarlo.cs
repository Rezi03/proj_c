using System.Diagnostics;
using System.Runtime.InteropServices;
using Quant.Numerics;

namespace RiskEngine.Analytics;

/// <summary>
/// Pont entre le C# et le moteur C++ (libriskengine). Si la bibliothèque C++ n'est pas compilée,
/// le programme bascule automatiquement sur une version C# du même algorithme.
/// </summary>
public static class MonteCarlo
{
    private const string Lib = "riskengine";

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int re_version();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int re_threads();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int re_mc_pnl(double[] mu, double[] cov, double[] exposure, int n, int nSims,
                                        int dist, double nu, ulong seed, [Out] double[] outPnl);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int re_tail_stats(double[] pnl, int n, double varLevel, double esLevel, out double outVar, out double outEs);

    private static bool? _native;

    public static bool NativeAvailable
    {
        get
        {
            if (_native.HasValue) return _native.Value;
            try { _native = re_version() > 0; }
            catch (Exception) { _native = false; }
            return _native.Value;
        }
    }

    public static string EngineLabel => NativeAvailable
        ? $"C++17 natif, {re_threads()} threads"
        : "C# (bibliothèque C++ non compilée)";

    public sealed record McResult(double[] Pnl, double Var, double Es, double Milliseconds, bool Native);

    public static McResult Simulate(double[] mu, double[,] cov, double[] exposure, int nSims, bool student, double nu, ulong seed)
    {
        int n = mu.Length;
        var flat = new double[n * n];
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) flat[i * n + j] = cov[i, j];
        var pnl = new double[nSims];
        var sw = Stopwatch.StartNew();
        double varLoss, es;

        if (NativeAvailable)
        {
            int rc = re_mc_pnl(mu, flat, exposure, n, nSims, student ? 1 : 0, nu, seed, pnl);
            if (rc != 0) throw new InvalidOperationException($"Moteur C++ : code d'erreur {rc}");
            re_tail_stats(pnl, nSims, Config.VarLevel, Config.EsLevel, out varLoss, out es);
            sw.Stop();
            return new McResult(pnl, varLoss, es, sw.Elapsed.TotalMilliseconds, true);
        }

        SimulateManaged(mu, cov, exposure, nSims, student, nu, seed, pnl);
        varLoss = Stats.VarLoss(pnl, Config.VarLevel);
        es = Stats.EsLoss(pnl, Config.EsLevel);
        sw.Stop();
        return new McResult(pnl, varLoss, es, sw.Elapsed.TotalMilliseconds, false);
    }

    // Version C# de secours : même algorithme que le C++ (Cholesky + tirages normaux ou Student).
    private static void SimulateManaged(double[] mu, double[,] cov, double[] exposure, int nSims, bool student, double nu, ulong seed, double[] pnl)
    {
        int n = mu.Length;
        var l = Stats.Cholesky(cov) ?? throw new InvalidOperationException("Covariance non définie positive");
        var rng = new Random(unchecked((int)seed));
        double Gauss()
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }
        int k = (int)Math.Round(nu);
        double scale = student ? Math.Sqrt((k - 2.0) / k) : 1.0;
        var z = new double[n];
        for (int s = 0; s < nSims; s++)
        {
            for (int i = 0; i < n; i++) z[i] = Gauss();
            double mix = 1.0;
            if (student)
            {
                double chi2 = 0;
                for (int j = 0; j < k; j++) { double g = Gauss(); chi2 += g * g; }
                mix = scale / Math.Sqrt(chi2 / k);
            }
            double p = 0;
            for (int i = 0; i < n; i++)
            {
                double acc = 0;
                for (int j = 0; j <= i; j++) acc += l[i, j] * z[j];
                p += exposure[i] * (Math.Exp(mu[i] + mix * acc) - 1);
            }
            pnl[s] = p;
        }
    }
}
