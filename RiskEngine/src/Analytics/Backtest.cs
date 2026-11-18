using Quant.Numerics;

namespace RiskEngine.Analytics;

public sealed class BacktestResult
{
    public required string Method { get; init; }
    public required List<DateTime> Dates { get; init; }
    public required double[] Pnl { get; init; }
    public required double[] Var { get; init; }      // VaR en perte positive
    public required List<int> Exceptions { get; init; }
    public int Days => Pnl.Length;
    public double Expected => Days * (1 - Config.VarLevel);
    public double KupiecLr { get; init; }
    public double KupiecP { get; init; }
    public double ChristoffersenLr { get; init; }
    public double ChristoffersenP { get; init; }

    /// <summary>Feux tricolores de Bâle (définis pour 250 jours, ramenés à l'échelle sinon).</summary>
    public string Zone
    {
        get
        {
            double scaled = Exceptions.Count * 250.0 / Days;
            return scaled <= 4 ? "Verte" : scaled <= 9 ? "Orange" : "Rouge";
        }
    }
}

/// <summary>Backtesting de la VaR : exceptions, test de Kupiec et test d'indépendance de Christoffersen.</summary>
public static class Backtest
{
    /// <summary>VaR historique glissante : chaque jour, VaR calculée sur les 500 jours précédents.</summary>
    public static BacktestResult Historical(List<DateTime> dates, double[] pnl, int days, int window)
    {
        int T = pnl.Length;
        var varSeries = new double[days];
        for (int k = 0; k < days; k++)
        {
            int t = T - days + k;
            var slice = new ArraySegment<double>(pnl, t - window, window);
            varSeries[k] = Stats.VarLoss(slice, Config.VarLevel);
        }
        return Build("VaR historique", dates, pnl, days, varSeries);
    }

    /// <summary>VaR paramétrique EWMA (RiskMetrics) mise à jour chaque jour.</summary>
    public static BacktestResult Ewma(List<DateTime> dates, double[] pnl, int days, double lambda)
    {
        int T = pnl.Length;
        double z = Stats.NormInv(Config.VarLevel);
        double v = 0;
        for (int t = 0; t < 60; t++) v += pnl[t] * pnl[t];
        v /= 60;
        var varSeries = new double[days];
        for (int t = 60; t < T; t++)
        {
            int k = t - (T - days);
            if (k >= 0) varSeries[k] = z * Math.Sqrt(v);   // prévision faite la veille
            v = lambda * v + (1 - lambda) * pnl[t] * pnl[t];
        }
        return Build("VaR EWMA (RiskMetrics)", dates, pnl, days, varSeries);
    }

    private static BacktestResult Build(string method, List<DateTime> dates, double[] pnl, int days, double[] varSeries)
    {
        int T = pnl.Length;
        var p = pnl.Skip(T - days).ToArray();
        var d = dates.Skip(T - days).ToList();
        var exc = new List<int>();
        for (int k = 0; k < days; k++) if (p[k] < -varSeries[k]) exc.Add(k);

        double alpha = 1 - Config.VarLevel;
        int x = exc.Count, n = days;
        double phat = (double)x / n;
        double lrPof = -2 * (Stats.XLogY(n - x, 1 - alpha) + Stats.XLogY(x, alpha))
                       + 2 * (Stats.XLogY(n - x, 1 - phat) + Stats.XLogY(x, phat));

        // Christoffersen : les exceptions arrivent-elles en grappes ?
        var hit = new bool[n];
        foreach (var k in exc) hit[k] = true;
        double n00 = 0, n01 = 0, n10 = 0, n11 = 0;
        for (int k = 1; k < n; k++)
        {
            if (!hit[k - 1] && !hit[k]) n00++;
            else if (!hit[k - 1] && hit[k]) n01++;
            else if (hit[k - 1] && !hit[k]) n10++;
            else n11++;
        }
        double pi0 = n00 + n01 > 0 ? n01 / (n00 + n01) : 0;
        double pi1 = n10 + n11 > 0 ? n11 / (n10 + n11) : 0;
        double pi = (n01 + n11) / Math.Max(1, n00 + n01 + n10 + n11);
        double l0 = Stats.XLogY(n00 + n10, 1 - pi) + Stats.XLogY(n01 + n11, pi);
        double l1 = Stats.XLogY(n00, 1 - pi0) + Stats.XLogY(n01, pi0) + Stats.XLogY(n10, 1 - pi1) + Stats.XLogY(n11, pi1);
        double lrInd = Math.Max(0, -2 * (l0 - l1));

        return new BacktestResult
        {
            Method = method,
            Dates = d,
            Pnl = p,
            Var = varSeries,
            Exceptions = exc,
            KupiecLr = Math.Max(0, lrPof),
            KupiecP = Stats.Chi2PValue1(Math.Max(0, lrPof)),
            ChristoffersenLr = lrInd,
            ChristoffersenP = Stats.Chi2PValue1(lrInd),
        };
    }
}
