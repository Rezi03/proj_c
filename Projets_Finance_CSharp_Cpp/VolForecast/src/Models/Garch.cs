using System.Runtime.InteropServices;

namespace VolForecast.Models;

/// <summary>Paramètres d'un GJR-GARCH(1,1). Gamma = 0 correspond au GARCH(1,1) classique.</summary>
public sealed record GarchParams(double Omega, double Alpha, double Gamma, double Beta, double LogLik)
{
    public double Persistence => Alpha + Beta + Gamma / 2;
    public double LongRunVariance => Omega / (1 - Persistence);
    public double HalfLifeDays => Math.Log(0.5) / Math.Log(Persistence);
    /// <summary>
    /// Effet de levier : volatilité prévue après une baisse de 'shock' divisée par celle prévue après une hausse
    /// de même taille, en partant du niveau de long terme.
    /// </summary>
    public double AsymmetryRatio(double shock) =>
        Math.Sqrt(Next(LongRunVariance, -shock) / Next(LongRunVariance, shock));

    public double Next(double h, double r) => Omega + (Alpha + (r < 0 ? Gamma : 0)) * r * r + Beta * h;
}

/// <summary>
/// Estimation par maximum de vraisemblance. La boucle de vraisemblance est en C++ (libvolengine),
/// l'optimiseur Nelder-Mead est en C#. Repli automatique en C# si la bibliothèque n'est pas compilée.
/// </summary>
public static class Garch
{
    private const string Lib = "volengine";

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int ve_version();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern double ve_gjr_nll(double[] r, int n, double omega, double alpha, double gamma, double beta, double h0);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ve_gjr_filter(double[] r, int n, double omega, double alpha, double gamma, double beta, double h0, [Out] double[] hOut);

    private static bool? _native;

    public static bool NativeAvailable
    {
        get
        {
            if (_native.HasValue) return _native.Value;
            try { _native = ve_version() > 0; }
            catch (Exception) { _native = false; }
            return _native.Value;
        }
    }

    public static string EngineLabel => NativeAvailable ? "C++17 natif" : "C# (bibliothèque C++ non compilée)";

    public static int LikelihoodCalls { get; private set; }

    public static double NegLogLik(double[] r, int n, double omega, double alpha, double gamma, double beta, double h0)
    {
        LikelihoodCalls++;
        if (NativeAvailable) return ve_gjr_nll(r, n, omega, alpha, gamma, beta, h0);

        const double penalty = 1e12;
        if (omega <= 0 || alpha < 0 || beta < 0 || alpha + gamma < 0 || alpha + beta + gamma / 2 >= 1) return penalty;
        double h = h0, nll = 0;
        for (int t = 0; t < n; t++)
        {
            if (!(h > 0) || !double.IsFinite(h)) return penalty;
            double r2 = r[t] * r[t];
            nll += 0.5 * (1.8378770664093453 + Math.Log(h) + r2 / h);
            h = omega + (alpha + (r[t] < 0 ? gamma : 0)) * r2 + beta * h;
        }
        return nll;
    }

    /// <summary>Variance conditionnelle filtrée : tableau de n + 1 valeurs, la dernière est la prévision du jour suivant.</summary>
    public static double[] Filter(double[] r, int n, GarchParams p, double h0)
    {
        var h = new double[n + 1];
        if (NativeAvailable)
        {
            ve_gjr_filter(r, n, p.Omega, p.Alpha, p.Gamma, p.Beta, h0, h);
            return h;
        }
        double v = h0;
        for (int t = 0; t < n; t++) { h[t] = v; v = p.Next(v, r[t]); }
        h[n] = v;
        return h;
    }

    public static double SampleVariance(double[] r, int n)
    {
        double s = 0;
        for (int t = 0; t < n; t++) s += r[t] * r[t];
        return s / n;
    }

    private static double Sig(double x) => 1 / (1 + Math.Exp(-x));
    private static double Logit(double p) => Math.Log(p / (1 - p));

    /// <summary>
    /// Estime le modèle sur r[0..n-1]. Ciblage de variance : omega = variance empirique × (1 - persistance),
    /// ce qui garantit une variance de long terme réaliste et stabilise l'optimisation.
    /// </summary>
    public static GarchParams Fit(double[] r, int n, bool asymmetric)
    {
        double v = SampleVariance(r, n);
        const double aMax = 0.30, gMax = 0.40;

        (double a, double g, double b) Map(double[] th)
        {
            double a = aMax * Sig(th[0]);
            double g = asymmetric ? gMax * Sig(th[1]) : 0;
            double b = (1 - a - g / 2 - 1e-4) * Sig(th[asymmetric ? 2 : 1]);
            return (a, g, b);
        }

        double Objective(double[] th)
        {
            var (a, g, b) = Map(th);
            double omega = v * (1 - a - g / 2 - b);
            return NegLogLik(r, n, omega, a, g, b, v);
        }

        double a0 = asymmetric ? 0.03 : 0.08, g0 = 0.10, b0 = 0.90;
        var x0 = asymmetric
            ? new[] { Logit(a0 / aMax), Logit(g0 / gMax), Logit(b0 / (1 - a0 - g0 / 2 - 1e-4)) }
            : new[] { Logit(a0 / aMax), Logit(b0 / (1 - a0 - 1e-4)) };

        var (x, f, _) = NelderMead.Minimize(Objective, x0);
        var (alpha, gamma, beta) = Map(x);
        return new GarchParams(v * (1 - alpha - gamma / 2 - beta), alpha, gamma, beta, -f);
    }
}

/// <summary>Modèle HAR-RV de Corsi (2009) : la volatilité de demain dépend de celle du jour, de la semaine et du mois.</summary>
public sealed record HarParams(double B0, double BDay, double BWeek, double BMonth)
{
    public static double Week(double[] rv, int t) => Avg(rv, t - 4, 5);
    public static double Month(double[] rv, int t) => Avg(rv, t - 21, 22);

    private static double Avg(double[] x, int from, int len)
    {
        double s = 0;
        for (int i = from; i < from + len; i++) s += x[i];
        return s / len;
    }

    /// <summary>Prévision de la variance du jour t + 1, avec l'information connue en t.</summary>
    public double Forecast(double[] rv, int t)
    {
        double m = Month(rv, t);
        double f = B0 + BDay * rv[t] + BWeek * Week(rv, t) + BMonth * m;
        return Math.Max(f, 0.2 * m);   // garde-fou : une variance reste positive
    }

    /// <summary>Estimation par moindres carrés sur les données rv[0..n-1].</summary>
    public static HarParams Fit(double[] rv, int n)
    {
        var x = new List<double[]>();
        var y = new List<double>();
        for (int t = 21; t < n - 1; t++)
        {
            x.Add(new[] { 1.0, rv[t], Week(rv, t), Month(rv, t) });
            y.Add(rv[t + 1]);
        }
        var b = Quant.Numerics.Stats.Ols(x, y);
        return new HarParams(b[0], b[1], b[2], b[3]);
    }
}
