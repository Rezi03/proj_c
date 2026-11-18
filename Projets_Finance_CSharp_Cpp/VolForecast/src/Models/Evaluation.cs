using Quant.Numerics;

namespace VolForecast.Models;

public sealed record ModelScore(double RmseVol, double MaeVol, double Qlike, double MzR2, double DmStat, double DmP,
                                int VarExceptions, double VarExpected, double KupiecP, List<int> ExceptionIdx);

/// <summary>
/// Comparaison des modèles hors échantillon :
/// erreurs sur la volatilité, perte QLIKE (robuste au bruit de la variance réalisée), régression de Mincer-Zarnowitz,
/// test de Diebold-Mariano contre la référence naïve, et backtesting de la VaR qui en découle.
/// </summary>
public static class Evaluation
{
    public static void ScoreAll(StudyResult s)
    {
        int start = s.OosStart, n = s.OosDays;
        var rv = s.Rv.Skip(start).ToArray();
        var r = s.Returns.Skip(start).ToArray();
        var naiveLoss = QlikeLosses(s.Model("HIST").Oos, rv);
        double z = Stats.NormInv(Config.VarLevel);

        foreach (var m in s.Models)
        {
            var h = m.Oos;
            double se = 0, ae = 0;
            for (int t = 0; t < n; t++)
            {
                double e = Math.Sqrt(252 * h[t]) - Math.Sqrt(252 * rv[t]);
                se += e * e;
                ae += Math.Abs(e);
            }
            var loss = QlikeLosses(h, rv);
            double r2 = Math.Pow(Corr(h, rv), 2);

            double dm = double.NaN, dmP = double.NaN;
            if (m.Short != "HIST")
            {
                var d = loss.Select((l, i) => l - naiveLoss[i]).ToArray();
                dm = DieboldMariano(d, Config.DieboldMarianoLag);
                dmP = 2 * (1 - Stats.NormCdf(Math.Abs(dm)));
            }

            var exc = new List<int>();
            for (int t = 0; t < n; t++) if (r[t] < -z * Math.Sqrt(h[t])) exc.Add(t);
            double alpha = 1 - Config.VarLevel, x = exc.Count, ph = x / n;
            double lr = -2 * (Stats.XLogY(n - x, 1 - alpha) + Stats.XLogY(x, alpha))
                        + 2 * (Stats.XLogY(n - x, 1 - ph) + Stats.XLogY(x, ph));

            m.Score = new ModelScore(Math.Sqrt(se / n), ae / n, loss.Average(), r2, dm, dmP,
                                     exc.Count, n * alpha, Stats.Chi2PValue1(Math.Max(0, lr)), exc);
        }
    }

    private static double[] QlikeLosses(double[] h, double[] rv)
        => h.Select((v, i) => rv[i] / v - Math.Log(rv[i] / v) - 1).ToArray();

    private static double Corr(double[] a, double[] b) => Stats.Covariance(a, b) / Math.Sqrt(Stats.Variance(a) * Stats.Variance(b));

    /// <summary>Statistique de Diebold-Mariano avec variance de long terme de Newey-West (noyau de Bartlett).</summary>
    private static double DieboldMariano(double[] d, int lag)
    {
        int n = d.Length;
        double mean = d.Average();
        double g0 = 0;
        for (int t = 0; t < n; t++) g0 += (d[t] - mean) * (d[t] - mean);
        g0 /= n;
        double lrv = g0;
        for (int k = 1; k <= lag; k++)
        {
            double gk = 0;
            for (int t = k; t < n; t++) gk += (d[t] - mean) * (d[t - k] - mean);
            gk /= n;
            lrv += 2 * (1 - k / (lag + 1.0)) * gk;
        }
        return mean / Math.Sqrt(Math.Max(lrv, 1e-300) / n);
    }
}
