using System.Diagnostics;
using Quant.Data;
using Quant.Numerics;
using Quant.Report;

namespace VolForecast.Models;

public sealed class ModelForecast
{
    public required string Name { get; init; }
    public required string Short { get; init; }
    public required string Color { get; init; }
    public required string Idea { get; init; }
    public required double[] Oos { get; init; }       // variance prévue pour chaque jour hors échantillon
    public required double NextDay { get; init; }     // variance prévue pour la prochaine séance
    public string Parameters { get; init; } = "";
    public ModelScore? Score { get; set; }
}

public sealed class StudyResult
{
    public required List<DateTime> Dates { get; init; }
    public required double[] Close { get; init; }
    public required double[] Returns { get; init; }
    public required double[] Rv { get; init; }            // variance réalisée (Garman-Klass + nuit)
    public required double[] RealizedVol21 { get; init; } // volatilité réalisée 21 jours, annualisée
    public required int OosStart { get; init; }
    public required List<ModelForecast> Models { get; init; }
    public required GarchParams GarchFinal { get; init; }
    public required GarchParams GjrFinal { get; init; }
    public required HarParams HarFinal { get; init; }
    public required int Refits { get; init; }
    public required double FitMilliseconds { get; init; }
    public required int LikelihoodCalls { get; init; }
    public DateTime AsOf => Dates[^1];
    public int OosDays => Returns.Length - OosStart;
    public ModelForecast Model(string shortName) => Models.First(m => m.Short == shortName);
}

/// <summary>Prévisions glissantes hors échantillon : chaque modèle ne voit jamais les données qu'il prévoit.</summary>
public static class Forecaster
{
    public static StudyResult Run(List<Bar> bars)
    {
        // 1. Rendements et variance réalisée
        var clean = bars.Where(b => b.Close > 0 && b.Open > 0 && b.High > 0 && b.Low > 0).ToList();
        int T = clean.Count - 1;
        var dates = new List<DateTime>(T);
        var r = new double[T];
        var rv = new double[T];
        var close = new double[T];
        double k = 2 * Math.Log(2) - 1;
        for (int t = 0; t < T; t++)
        {
            var prev = clean[t];
            var b = clean[t + 1];
            dates.Add(b.Date);
            close[t] = b.Close;
            r[t] = Math.Log(b.Close / prev.Close);
            double hl = Math.Log(b.High / b.Low), co = Math.Log(b.Close / b.Open), on = Math.Log(b.Open / prev.Close);
            double gk = 0.5 * hl * hl - k * co * co;
            double v = on * on + gk;
            // Séance sans plus haut / plus bas fiables : on retombe sur le rendement au carré
            if (b.High <= b.Low || !(v > 0) || !double.IsFinite(v)) v = r[t] * r[t];
            rv[t] = Math.Max(v, 1e-8);
        }

        int oos = Math.Min(Config.OutOfSampleDays, T - 1500);
        if (oos < 250) throw new InvalidOperationException($"Historique trop court ({T} séances).");
        int start = T - oos;

        var rv21 = new double[T];
        for (int t = 0; t < T; t++)
        {
            int from = Math.Max(0, t - 20);
            double s = 0;
            for (int i = from; i <= t; i++) s += rv[i];
            rv21[t] = Math.Sqrt(252 * s / (t - from + 1));
        }

        var models = new List<ModelForecast>();

        // 2. Volatilité historique (référence naïve)
        {
            var h = new double[oos];
            for (int t = start; t < T; t++) h[t - start] = MeanSquare(r, t - Config.NaiveWindow, Config.NaiveWindow);
            models.Add(new ModelForecast
            {
                Name = "Volatilité historique 21 j", Short = "HIST", Color = Theme.GreyLight,
                Idea = "Demain ressemblera au dernier mois. C'est la référence à battre.",
                Oos = h, NextDay = MeanSquare(r, T - Config.NaiveWindow, Config.NaiveWindow),
                Parameters = "fenêtre de 21 séances",
            });
        }

        // 3. EWMA RiskMetrics
        {
            var h = new double[oos];
            double v = MeanSquare(r, 0, 60);
            for (int t = 60; t < T; t++)
            {
                if (t >= start) h[t - start] = v;
                v = Config.EwmaLambda * v + (1 - Config.EwmaLambda) * r[t] * r[t];
            }
            models.Add(new ModelForecast
            {
                Name = "EWMA (RiskMetrics)", Short = "EWMA", Color = Theme.Amber,
                Idea = "Moyenne pondérée des rendements au carré, les jours récents pèsent plus.",
                Oos = h, NextDay = v, Parameters = "λ = 0,94",
            });
        }

        // 4. GARCH(1,1) et GJR-GARCH(1,1), ré-estimés chaque mois
        var sw = Stopwatch.StartNew();
        int refits = 0;
        var garch = RollingGarch(r, start, T, asymmetric: false, ref refits);
        var gjr = RollingGarch(r, start, T, asymmetric: true, ref refits);
        var garchFinal = Garch.Fit(r, T, asymmetric: false);
        var gjrFinal = Garch.Fit(r, T, asymmetric: true);
        refits += 2;
        sw.Stop();
        double garchNext = Garch.Filter(r, T, garchFinal, Garch.SampleVariance(r, T))[T];
        double gjrNext = Garch.Filter(r, T, gjrFinal, Garch.SampleVariance(r, T))[T];

        models.Add(new ModelForecast
        {
            Name = "GARCH(1,1)", Short = "GARCH", Color = Theme.PrimaryLight,
            Idea = "La volatilité revient vers une moyenne de long terme après chaque choc.",
            Oos = garch, NextDay = garchNext,
            Parameters = $"α = {Fmt.Num(garchFinal.Alpha, 3)}, β = {Fmt.Num(garchFinal.Beta, 3)}",
        });
        models.Add(new ModelForecast
        {
            Name = "GJR-GARCH(1,1)", Short = "GJR", Color = Theme.Primary,
            Idea = "Comme le GARCH, mais une baisse fait monter la volatilité plus qu'une hausse (effet de levier).",
            Oos = gjr, NextDay = gjrNext,
            Parameters = $"α = {Fmt.Num(gjrFinal.Alpha, 3)}, γ = {Fmt.Num(gjrFinal.Gamma, 3)}, β = {Fmt.Num(gjrFinal.Beta, 3)}",
        });

        // 5. HAR-RV, ré-estimé chaque mois
        {
            var h = new double[oos];
            for (int b = start; b < T; b += Config.RefitEvery)
            {
                var p = HarParams.Fit(rv, b);
                for (int t = b; t < Math.Min(T, b + Config.RefitEvery); t++) h[t - start] = p.Forecast(rv, t - 1);
            }
            var harFinal = HarParams.Fit(rv, T);
            models.Add(new ModelForecast
            {
                Name = "HAR-RV (Corsi)", Short = "HAR", Color = Theme.Secondary,
                Idea = "Régression sur la volatilité réalisée du jour, de la semaine et du mois (données haut / bas).",
                Oos = h, NextDay = harFinal.Forecast(rv, T - 1),
                Parameters = $"jour {Fmt.Num(harFinal.BDay, 2)}, semaine {Fmt.Num(harFinal.BWeek, 2)}, mois {Fmt.Num(harFinal.BMonth, 2)}",
            });

            var result = new StudyResult
            {
                Dates = dates, Close = close, Returns = r, Rv = rv, RealizedVol21 = rv21, OosStart = start, Models = models,
                GarchFinal = garchFinal, GjrFinal = gjrFinal, HarFinal = harFinal,
                Refits = refits, FitMilliseconds = sw.Elapsed.TotalMilliseconds, LikelihoodCalls = Garch.LikelihoodCalls,
            };
            Evaluation.ScoreAll(result);
            return result;
        }
    }

    private static double[] RollingGarch(double[] r, int start, int T, bool asymmetric, ref int refits)
    {
        var h = new double[T - start];
        for (int b = start; b < T; b += Config.RefitEvery)
        {
            var p = Garch.Fit(r, b, asymmetric);
            refits++;
            double v = Garch.Filter(r, b, p, Garch.SampleVariance(r, b))[b];
            for (int t = b; t < Math.Min(T, b + Config.RefitEvery); t++)
            {
                h[t - start] = v;
                v = p.Next(v, r[t]);
            }
        }
        return h;
    }

    private static double MeanSquare(double[] r, int from, int len)
    {
        double s = 0;
        for (int i = from; i < from + len; i++) s += r[i] * r[i];
        return s / len;
    }
}
