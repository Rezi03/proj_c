using Quant.Data;

namespace RiskEngine.Analytics;

public sealed record StressResult(string Name, string Type, string Period, double Pnl, double PnlPct, double? BenchmarkMove, string Comment, bool Partial);

/// <summary>
/// Stress tests : on rejoue des crises passées sur le portefeuille d'aujourd'hui,
/// puis on applique des chocs hypothétiques par secteur.
/// </summary>
public static class StressTests
{
    public static List<StressResult> Run(IReadOnlyDictionary<string, List<Bar>> raw, double[] exposure)
    {
        var results = new List<StressResult>();
        var assets = Config.Assets;

        foreach (var sc in Config.HistoricalScenarios)
        {
            double? bench = Move(raw[Config.Benchmark], sc.Start, sc.End);
            double pnl = 0;
            bool partial = false;
            for (int i = 0; i < assets.Length; i++)
            {
                double? m = Move(raw[assets[i].Ticker], sc.Start, sc.End);
                if (m is null) { partial = true; m = bench ?? 0; }   // action pas encore cotée : on prend l'indice
                pnl += exposure[i] * m.Value;
            }
            results.Add(new StressResult(sc.Name, "Historique",
                $"{sc.Start:dd/MM/yyyy} - {sc.End:dd/MM/yyyy}", pnl, pnl / Config.PortfolioValue, bench, sc.Comment, partial));
        }

        foreach (var sc in Config.ShockScenarios)
        {
            double pnl = 0;
            for (int i = 0; i < assets.Length; i++)
            {
                double shock = sc.SectorShocks.TryGetValue(assets[i].Sector, out var s) ? s : sc.DefaultShock;
                pnl += exposure[i] * shock;
            }
            results.Add(new StressResult(sc.Name, "Hypothétique", "Choc instantané", pnl, pnl / Config.PortfolioValue, null, sc.Comment, false));
        }
        return results;
    }

    /// <summary>Variation du cours entre la dernière séance avant 'start' et la dernière séance avant 'end'.</summary>
    private static double? Move(List<Bar> bars, DateTime start, DateTime end)
    {
        var b0 = bars.LastOrDefault(b => b.Date <= start);
        var b1 = bars.LastOrDefault(b => b.Date <= end);
        if (b0 is null || b1 is null || b0.Date < start.AddDays(-10) || b1.Date <= b0.Date) return null;
        return b1.AdjClose / b0.AdjClose - 1;
    }
}
