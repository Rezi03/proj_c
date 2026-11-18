using Quant.Data;
using Quant.Numerics;
using Quant.Report;

namespace RiskEngine.Analytics;

public sealed record MethodResult(string Name, string Short, double Var, double Es, string Description);

/// <summary>Tous les résultats du moteur, prêts pour le rapport.</summary>
public sealed class RiskResult
{
    public required DateTime AsOf { get; init; }
    public required DateTime FirstDate { get; init; }
    public required int Observations { get; init; }
    public required double[] Exposure { get; init; }
    public required double[] WindowPnl { get; init; }
    public required List<MethodResult> Methods { get; init; }
    public required MonteCarlo.McResult McNormal { get; init; }
    public required MonteCarlo.McResult McStudent { get; init; }
    public required double ParametricVar { get; init; }
    public required double[] ComponentVar { get; init; }
    public required double[] StandaloneVar { get; init; }
    public required double[,] Correlation { get; init; }
    public required double[] AssetVolAnn { get; init; }
    public required double[] AssetBeta { get; init; }
    public required double PortfolioVolAnn { get; init; }
    public required double PortfolioBeta { get; init; }
    public required double BenchmarkVolAnn { get; init; }
    public required BacktestResult BtHist { get; init; }
    public required BacktestResult BtEwma { get; init; }
    public required BacktestResult BtHistLong { get; init; }
    public required BacktestResult BtEwmaLong { get; init; }
    public required List<StressResult> Stress { get; init; }
    public required List<DateTime> PerfDates { get; init; }
    public required double[] PerfPortfolio { get; init; }
    public required double[] PerfBenchmark { get; init; }
    public required double[] RollingVol { get; init; }
    public required double[] RollingVolBench { get; init; }
    public required double MaxDrawdown { get; init; }
    public double DiversificationBenefit => StandaloneVar.Sum() - ParametricVar;
}

public static class RiskAnalysis
{
    public static RiskResult Run(IReadOnlyDictionary<string, List<Bar>> raw)
    {
        var assets = Config.Assets;
        int n = assets.Length;

        // 1. Alignement des cours ajustés (dividendes inclus) sur les dates communes
        var series = assets.Select(a => raw[a.Ticker]).Append(raw[Config.Benchmark]).ToList();
        var (dates, prices) = MarketData.Align(series, b => b.AdjClose);
        int T = dates.Count - 1;
        int minNeeded = Config.VarWindow + Config.BacktestLongDays + 10;
        if (T < minNeeded)
            throw new InvalidOperationException($"Historique commun trop court ({T} rendements, {minNeeded} nécessaires).");

        // 2. Rendements logarithmiques et simples
        var logR = new double[n + 1][];
        var simR = new double[n + 1][];
        for (int i = 0; i <= n; i++)
        {
            logR[i] = new double[T];
            simR[i] = new double[T];
            for (int t = 0; t < T; t++)
            {
                logR[i][t] = Math.Log(prices[i][t + 1] / prices[i][t]);
                simR[i][t] = prices[i][t + 1] / prices[i][t] - 1;
            }
        }
        var retDates = dates.Skip(1).ToList();
        var exposure = assets.Select(a => a.Weight * Config.PortfolioValue).ToArray();

        // P&L quotidien du portefeuille actuel rejoué sur tout l'historique
        var pnl = new double[T];
        for (int t = 0; t < T; t++)
            for (int i = 0; i < n; i++) pnl[t] += exposure[i] * simR[i][t];

        int w = Config.VarWindow;
        int start = T - w;
        var windowPnl = pnl.Skip(start).ToArray();
        var assetSim = simR.Take(n).ToArray();
        var assetLog = logR.Take(n).ToArray();

        double zVar = Stats.NormInv(Config.VarLevel);
        double zEs = Stats.NormInv(Config.EsLevel);
        double esFactor = Stats.NormPdf(zEs) / (1 - Config.EsLevel);

        // 3. VaR et ES selon cinq méthodes
        var methods = new List<MethodResult>();
        methods.Add(new MethodResult("Historique", "HS",
            Stats.VarLoss(windowPnl, Config.VarLevel), Stats.EsLoss(windowPnl, Config.EsLevel),
            "Quantile des 500 derniers P&L réels du portefeuille actuel"));

        var cov = Stats.CovMatrix(assetSim, start, w);
        double sigmaP = Math.Sqrt(Stats.Dot(exposure, Stats.MatVec(cov, exposure)));
        methods.Add(new MethodResult("Paramétrique normale", "VCV",
            zVar * sigmaP, esFactor * sigmaP, "Variance-covariance, loi normale"));

        var ewma = Stats.EwmaCov(assetSim, start, w, Config.EwmaLambda);
        double sigmaE = Math.Sqrt(Stats.Dot(exposure, Stats.MatVec(ewma, exposure)));
        methods.Add(new MethodResult("EWMA (RiskMetrics)", "EWMA",
            zVar * sigmaE, esFactor * sigmaE, "Covariance pondérée, λ = 0,94, réagit vite aux chocs"));

        var mu = new double[n];
        for (int i = 0; i < n; i++) mu[i] = Stats.Mean(new ArraySegment<double>(assetLog[i], start, w));
        var covLog = Stats.CovMatrix(assetLog, start, w);
        var mcN = MonteCarlo.Simulate(mu, covLog, exposure, Config.McSimulations, student: false, Config.StudentNu, Config.Seed);
        var mcT = MonteCarlo.Simulate(mu, covLog, exposure, Config.McSimulations, student: true, Config.StudentNu, Config.Seed + 1);
        methods.Add(new MethodResult("Monte Carlo normale", "MC-N", mcN.Var, mcN.Es,
            $"{Fmt.Num(Config.McSimulations, 0)} scénarios, loi normale multivariée"));
        methods.Add(new MethodResult("Monte Carlo Student-t", "MC-t", mcT.Var, mcT.Es,
            $"{Fmt.Num(Config.McSimulations, 0)} scénarios, Student à {Fmt.Num(Config.StudentNu, 0)} degrés de liberté"));

        // 4. Contributions au risque (décomposition d'Euler de la VaR paramétrique)
        var sigmaVec = Stats.MatVec(cov, exposure);
        var component = new double[n];
        var standalone = new double[n];
        var volAnn = new double[n];
        for (int i = 0; i < n; i++)
        {
            component[i] = zVar * exposure[i] * sigmaVec[i] / sigmaP;
            standalone[i] = zVar * exposure[i] * Math.Sqrt(cov[i, i]);
            volAnn[i] = Math.Sqrt(cov[i, i] * 252);
        }

        // 5. Bêtas et volatilités par rapport au CAC 40
        var bench = new ArraySegment<double>(simR[n], start, w);
        double varBench = Stats.Variance(bench);
        var beta = new double[n];
        for (int i = 0; i < n; i++)
            beta[i] = Stats.Covariance(new ArraySegment<double>(simR[i], start, w), bench) / varBench;
        var portRet = windowPnl.Select(p => p / Config.PortfolioValue).ToArray();
        double portBeta = Stats.Covariance(portRet, bench) / varBench;

        // 6. Backtesting (250 jours réglementaires et 750 jours)
        var btH = Backtest.Historical(retDates, pnl, Config.BacktestDays, w);
        var btE = Backtest.Ewma(retDates, pnl, Config.BacktestDays, Config.EwmaLambda);
        var btHL = Backtest.Historical(retDates, pnl, Config.BacktestLongDays, w);
        var btEL = Backtest.Ewma(retDates, pnl, Config.BacktestLongDays, Config.EwmaLambda);

        // 7. Stress tests
        var stress = StressTests.Run(raw, exposure);

        // 8. Performance et volatilité glissante sur 3 ans
        int perfDays = Math.Min(756, T);
        int p0 = T - perfDays;
        var perfDates = retDates.Skip(p0).ToList();
        var perfP = new double[perfDays];
        var perfB = new double[perfDays];
        double vp = 100, vb = 100, peak = 100, mdd = 0;
        for (int k = 0; k < perfDays; k++)
        {
            vp *= 1 + pnl[p0 + k] / Config.PortfolioValue;
            vb *= 1 + simR[n][p0 + k];
            perfP[k] = vp;
            perfB[k] = vb;
            peak = Math.Max(peak, vp);
            mdd = Math.Min(mdd, vp / peak - 1);
        }
        var rollVol = new double[perfDays];
        var rollVolB = new double[perfDays];
        for (int k = 0; k < perfDays; k++)
        {
            int t = p0 + k;
            rollVol[k] = Math.Sqrt(252) * Stats.Std(new ArraySegment<double>(pnl, t - 20, 21)) / Config.PortfolioValue;
            rollVolB[k] = Math.Sqrt(252) * Stats.Std(new ArraySegment<double>(simR[n], t - 20, 21));
        }

        return new RiskResult
        {
            AsOf = retDates[^1],
            FirstDate = dates[0],
            Observations = T,
            Exposure = exposure,
            WindowPnl = windowPnl,
            Methods = methods,
            McNormal = mcN,
            McStudent = mcT,
            ParametricVar = zVar * sigmaP,
            ComponentVar = component,
            StandaloneVar = standalone,
            Correlation = Stats.Correlation(cov),
            AssetVolAnn = volAnn,
            AssetBeta = beta,
            PortfolioVolAnn = sigmaP / Config.PortfolioValue * Math.Sqrt(252),
            PortfolioBeta = portBeta,
            BenchmarkVolAnn = Math.Sqrt(varBench * 252),
            BtHist = btH,
            BtEwma = btE,
            BtHistLong = btHL,
            BtEwmaLong = btEL,
            Stress = stress,
            PerfDates = perfDates,
            PerfPortfolio = perfP,
            PerfBenchmark = perfB,
            RollingVol = rollVol,
            RollingVolBench = rollVolB,
            MaxDrawdown = mdd,
        };
    }
}
