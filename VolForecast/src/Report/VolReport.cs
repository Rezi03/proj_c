using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Quant.Numerics;
using Quant.Report;
using VolForecast.Models;

namespace VolForecast.Report;

/// <summary>Construit le rapport PDF de l'étude de prévision de volatilité.</summary>
public static class VolReport
{
    private const string Title = "Prévision de la volatilité du CAC 40";

    private static double Ann(double variance) => Math.Sqrt(252 * variance);

    public static string Build(StudyResult s, string outputDir)
    {
        Kit.Init();
        Directory.CreateDirectory(outputDir);
        string path = Path.Combine(outputDir, $"Rapport_Prevision_Volatilite_{s.AsOf:yyyy-MM-dd}.pdf");
        string dateLabel = $"Données au {Fmt.LongDate(s.AsOf)}";

        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                Kit.StandardPage(page, Title, dateLabel, showHeader: false);
                page.Content().Column(col => { col.Spacing(10); Cover(col, s, dateLabel); });
            });
            doc.Page(page =>
            {
                Kit.StandardPage(page, Title, dateLabel);
                page.Content().Column(col =>
                {
                    col.Spacing(9);
                    DataSection(col, s);
                    col.Item().PageBreak();
                    ModelsSection(col, s);
                    col.Item().PageBreak();
                    EvaluationSection(col, s);
                    col.Item().PageBreak();
                    ForecastSection(col, s);
                    col.Item().PageBreak();
                    VarSection(col, s);
                    col.Item().PageBreak();
                    MethodologySection(col, s);
                });
            });
        })
        .WithMetadata(new DocumentMetadata { Title = Title, Author = "Rezo Sabashvili" })
        .GeneratePdf(path);
        return path;
    }

    private static ModelForecast Best(StudyResult s) => s.Models.OrderBy(m => m.Score!.Qlike).First();

    private static double Percentile(double[] history, double value) => history.Count(v => v <= value) / (double)history.Length;

    private static string Regime(double pct) =>
        pct < 0.25 ? "calme" : pct < 0.60 ? "normal" : pct < 0.85 ? "agité" : "de stress";

    // ---------------------------------------------------------------- Couverture
    private static void Cover(ColumnDescriptor col, StudyResult s, string dateLabel)
    {
        Kit.Cover(col, "Note de recherche quantitative · Prévision et modélisation",
            "Prévision de la ", "volatilité", $" du {Config.Name}",
            "Cinq modèles comparés hors échantillon sur cinq ans (EWMA, GARCH, GJR-GARCH, HAR-RV), " +
            "prévision pour demain et pour les six prochains mois, application à la VaR",
            $"{dateLabel.ToUpperInvariant()}  ·  {Fmt.Num(s.Returns.Length, 0)} SÉANCES DEPUIS {Fmt.Date(s.Dates[0])}");

        var gjr = s.Model("GJR");
        var best = Best(s);
        double nextVol = Ann(gjr.NextDay);
        double pct = Percentile(s.RealizedVol21, nextVol);
        var p = s.GjrFinal;
        double asym = p.AsymmetryRatio(0.03);
        var (tagColor, tagBg) = pct < 0.25 ? (Theme.Secondary, Theme.SecondarySoft)
                              : pct < 0.85 ? (Theme.Primary, Theme.PrimarySoft)
                              : (Theme.Coral, "#FDECE7");

        Kit.HeroStat(col, "Volatilité prévue pour la prochaine séance", Fmt.Pct(nextVol, 1),
            $"annualisée, modèle GJR-GARCH  ·  soit ± {Fmt.Pct(Math.Sqrt(gjr.NextDay), 2)} par jour, ± {Fmt.Num(s.Close[^1] * Math.Sqrt(gjr.NextDay), 0)} points",
            char.ToUpper(Regime(pct)[0]) + Regime(pct)[1..], tagColor, tagBg);

        Kit.Kpis(col, new List<Kpi>
        {
            new("Meilleur modèle", best.Short, $"QLIKE minimale sur {s.OosDays} j", Theme.Secondary),
            new("Vol. de long terme", Fmt.Pct(Ann(p.LongRunVariance), 1), "niveau d'équilibre"),
            new("Demi-vie d'un choc", $"{Fmt.Num(p.HalfLifeDays, 0)} j", $"persistance {Fmt.Num(p.Persistence, 3)}"),
            new("Effet de levier", $"× {Fmt.Num(asym, 2)}", "baisse de 3 % face à hausse de 3 %", Theme.Coral),
        });

        var hist = s.Model("HIST");
        Kit.SubTitle(col, "Ce qu'il faut retenir");
        Kit.Bullets(col, new[]
        {
            $"Le marché est dans un régime {Regime(pct)} : la volatilité prévue dépasse {Fmt.Pct(pct, 0)} des niveaux observés depuis {s.Dates[0].Year}.",
            $"Sur {s.OosDays} séances jamais vues par les modèles, le meilleur est {best.Name}. Sa perte QLIKE est " +
            $"{Fmt.Pct(1 - best.Score!.Qlike / hist.Score!.Qlike, 0)} plus faible que celle de la volatilité historique.",
            $"Après une baisse de 3 % du {Config.Name}, la volatilité prévue pour le lendemain est {Fmt.Num(asym, 2)} fois plus élevée " +
            "qu'après une hausse de 3 %. C'est l'effet de levier, capté par le GJR-GARCH et ignoré par le GARCH classique.",
            $"Un choc de volatilité perd la moitié de son effet en {Fmt.Num(p.HalfLifeDays, 0)} séances environ, " +
            $"avant un retour vers {Fmt.Pct(Ann(p.LongRunVariance), 1)} de long terme.",
            $"Utilisées pour une VaR 99 %, les prévisions du GJR-GARCH donnent {gjr.Score!.VarExceptions} exceptions pour " +
            $"{Fmt.Num(gjr.Score.VarExpected, 1)} attendues, contre {hist.Score!.VarExceptions} avec la volatilité historique.",
        });

        Kit.TechBox(col, "Sous le capot", new[]
        {
            ("langages", "C# (.NET 10) pour les modèles et le rapport, C++17 pour la vraisemblance GARCH"),
            ("estimation", $"{s.Refits} estimations par maximum de vraisemblance (Nelder-Mead), {Fmt.Num(s.LikelihoodCalls, 0)} appels au moteur en {Fmt.Num(s.FitMilliseconds / 1000, 1)} s ({Garch.EngineLabel})"),
            ("protocole", $"prévisions glissantes, modèles ré-estimés tous les {Config.RefitEvery} jours sans voir le futur"),
            ("données", $"Yahoo Finance, {Config.Name} quotidien (ouverture, plus haut, plus bas, clôture)"),
        });
    }

    // ---------------------------------------------------------------- 1. Données
    private static void DataSection(ColumnDescriptor col, StudyResult s)
    {
        Kit.Section(col, "1", "Les données", $"{Config.Name} depuis {s.Dates[0].Year}, cours d'ouverture, plus haut, plus bas et clôture.");
        Kit.Chart(col, Charts.Lines(s.Dates, new List<LineSeries> { new($"{Config.Name} (points)", s.Close, Theme.Primary, 1.3) },
            new ChartOptions { Height = 250, YFormat = v => Fmt.Num(v, 0) }));

        Kit.SubTitle(col, "Volatilité réalisée sur 21 jours, annualisée");
        var oosMask = s.RealizedVol21.Select((v, i) => i >= s.OosStart ? v : double.NaN).ToList();
        Kit.Chart(col, Charts.Lines(s.Dates, new List<LineSeries>
        {
            new("Période d'estimation", s.RealizedVol21, Theme.GreyLight, 1.2),
            new("Période de test hors échantillon", oosMask, Theme.Coral, 1.3),
        }, new ChartOptions { Height = 250, YFormat = v => Fmt.Pct(v, 0), YMin = 0 }),
        "Les pics correspondent à 2008, 2011, 2020 et 2022. Les modèles sont jugés uniquement sur la partie rouge.");

        Kit.Note(col, "Mesurer la volatilité d'une journée",
            "La volatilité ne s'observe pas directement. On l'estime chaque jour avec l'estimateur de Garman-Klass, qui utilise " +
            "le plus haut, le plus bas, l'ouverture et la clôture, plus le saut de la nuit. Il est environ sept fois plus précis que " +
            "le simple rendement au carré, ce qui rend la comparaison des modèles plus fiable.");
    }

    // ---------------------------------------------------------------- 2. Modèles
    private static void ModelsSection(ColumnDescriptor col, StudyResult s)
    {
        Kit.Section(col, "2", "Les cinq modèles", "Du plus simple au plus riche. Paramètres estimés sur tout l'historique.");
        var rows = s.Models.Select(m => new[] { m.Name, m.Idea, m.Parameters }).ToList();
        Kit.Table(col, new[] { "Modèle", "Idée", "Paramètres" }, rows, new[] { 1.5f, 3.4f, 1.9f }, leftCols: 3);

        var g = s.GarchFinal;
        var j = s.GjrFinal;
        Kit.SubTitle(col, "Estimation par maximum de vraisemblance");
        Kit.Table(col, new[] { "", "GARCH(1,1)", "GJR-GARCH(1,1)" }, new List<string[]>
        {
            new[] { "α  (réaction aux chocs)", Fmt.Num(g.Alpha, 4), Fmt.Num(j.Alpha, 4) },
            new[] { "γ  (supplément si baisse)", "-", Fmt.Num(j.Gamma, 4) },
            new[] { "β  (mémoire)", Fmt.Num(g.Beta, 4), Fmt.Num(j.Beta, 4) },
            new[] { "Persistance", Fmt.Num(g.Persistence, 4), Fmt.Num(j.Persistence, 4) },
            new[] { "Volatilité de long terme", Fmt.Pct(Ann(g.LongRunVariance), 1), Fmt.Pct(Ann(j.LongRunVariance), 1) },
            new[] { "Demi-vie d'un choc", $"{Fmt.Num(g.HalfLifeDays, 0)} jours", $"{Fmt.Num(j.HalfLifeDays, 0)} jours" },
            new[] { "Log-vraisemblance", Fmt.Num(g.LogLik, 1), Fmt.Num(j.LogLik, 1) },
        }, new[] { 2.4f, 1.3f, 1.3f });

        double lr = 2 * (j.LogLik - g.LogLik);
        Kit.Note(col, "Le GJR-GARCH est-il justifié ?",
            $"Le test du rapport de vraisemblance vaut {Fmt.Num(lr, 1)} (p-valeur {Fmt.Num(Stats.Chi2PValue1(Math.Max(0, lr)), 4)}). " +
            "Au-delà de 3,84, l'asymétrie est significative à 5 % : sur les actions, la volatilité réagit nettement plus aux baisses qu'aux hausses.");

        var shocks = Enumerable.Range(-24, 49).Select(i => i * 0.0025).ToList();
        double Nic(GarchParams p, double e) => Ann(p.Next(p.LongRunVariance, e));
        Kit.Chart(col, Charts.XY(shocks, new List<LineSeries>
        {
            new("GARCH(1,1)", shocks.Select(e => Nic(g, e)).ToList(), Theme.PrimaryLight, 2),
            new("GJR-GARCH(1,1)", shocks.Select(e => Nic(j, e)).ToList(), Theme.Primary, 2.4),
        }, new ChartOptions { Height = 240, YFormat = v => Fmt.Pct(v, 0) }, x => Fmt.Pct(x, 0)),
        "Volatilité prévue pour demain selon le rendement du jour, en partant du niveau de long terme. La courbe du GJR penche à gauche.",
        title: "Courbe d'impact des nouvelles");
    }

    // ---------------------------------------------------------------- 3. Évaluation
    private static void EvaluationSection(ColumnDescriptor col, StudyResult s)
    {
        Kit.Section(col, "3", "Qui prévoit le mieux ?",
            $"Test hors échantillon sur {s.OosDays} séances, du {Fmt.Date(s.Dates[s.OosStart])} au {Fmt.Date(s.AsOf)}.");

        var ranked = s.Models.OrderBy(m => m.Score!.Qlike).ToList();
        var rows = ranked.Select((m, i) => new[]
        {
            $"{i + 1}. {m.Name}", Fmt.Num(m.Score!.Qlike, 4), Fmt.Pct(m.Score.RmseVol, 2), Fmt.Pct(m.Score.MaeVol, 2),
            Fmt.Num(m.Score.MzR2, 3), double.IsNaN(m.Score.DmStat) ? "référence" : Fmt.Num(m.Score.DmStat, 2),
            double.IsNaN(m.Score.DmP) ? "-" : Fmt.Num(m.Score.DmP, 3)
        }).ToList();
        Kit.Table(col, new[] { "Modèle (classement)", "QLIKE", "RMSE vol.", "MAE vol.", "R² MZ", "DM", "p-valeur DM" }, rows,
            new[] { 2.3f, 0.9f, 0.9f, 0.9f, 0.8f, 0.9f, 0.9f },
            cellColor: (i, j) => j == 6 && !double.IsNaN(ranked[i].Score!.DmP) && ranked[i].Score!.DmP < 0.05
                ? (ranked[i].Score!.DmStat < 0 ? Theme.Green : Theme.Coral) : null,
            highlightRow: 0);
        col.Item().Text("QLIKE : perte moyenne, plus elle est faible, mieux c'est. DM négatif et p-valeur < 5 % (en vert) : " +
                        "le modèle bat significativement la volatilité historique. R² MZ : part de la volatilité réalisée expliquée par la prévision.")
            .FontSize(7.5f).Italic().FontColor(Theme.Grey);

        Kit.Chart(col, Charts.HBars(ranked.Select(m => m.Name).ToList(), new List<BarGroup>
        {
            new("QLIKE", ranked.Select(m => m.Score!.Qlike).ToList(), Theme.Primary),
        }, 760, v => Fmt.Num(v, 3), labelWidth: 190));

        int n = Math.Min(250, s.OosDays);
        int from = s.Returns.Length - n;
        var dates = s.Dates.Skip(from).ToList();
        var realized = new double[n];
        for (int k = 0; k < n; k++)
        {
            int t = from + k;
            double sum = 0;
            for (int i = t - 4; i <= t; i++) sum += s.Rv[i];
            realized[k] = Ann(sum / 5);
        }
        var series = new List<LineSeries> { new("Réalisée (moyenne 5 j)", realized, "#C5CBD6", 1.4) };
        foreach (var code in new[] { "HIST", "HAR", "GJR" })
        {
            var m = s.Model(code);
            series.Add(new LineSeries(m.Short == "HIST" ? "Historique 21 j" : m.Short, m.Oos.Skip(m.Oos.Length - n).Select(Ann).ToList(),
                m.Short == "HIST" ? Theme.Grey : m.Color, m.Short == "GJR" ? 2.2 : 1.6, Dashed: m.Short == "HIST"));
        }
        Kit.Chart(col, Charts.Lines(dates, series, new ChartOptions { Height = 270, YFormat = v => Fmt.Pct(v, 0), YMin = 0 }),
            "Volatilités annualisées. La référence historique réagit avec retard, les modèles GARCH et HAR suivent les chocs plus vite.",
            title: "Prévisions et réalité sur la dernière année");
    }

    // ---------------------------------------------------------------- 4. Prévisions
    private static void ForecastSection(ColumnDescriptor col, StudyResult s)
    {
        Kit.Section(col, "4", "Prévisions actuelles", $"Prévisions faites après la clôture du {Fmt.LongDate(s.AsOf)}.");

        var rows = s.Models.Select(m => new[]
        {
            m.Name, Fmt.Pct(Ann(m.NextDay), 1), $"± {Fmt.Pct(Math.Sqrt(m.NextDay), 2)}",
            $"± {Fmt.Num(s.Close[^1] * Math.Sqrt(m.NextDay), 0)} pts"
        }).ToList();
        Kit.Table(col, new[] { "Modèle", "Vol. annualisée demain", "Mouvement quotidien typique", $"Soit sur le {Config.Name}" }, rows,
            new[] { 2.2f, 1.5f, 1.6f, 1.5f }, highlightRow: s.Models.FindIndex(m => m.Short == "GJR"));

        Kit.SubTitle(col, "Courbe de prévision sur six mois (GJR-GARCH)");
        var p = s.GjrFinal;
        double lr = p.LongRunVariance;
        var hs = new double[Config.TermStructureDays];
        hs[0] = s.Model("GJR").NextDay;
        for (int k = 1; k < hs.Length; k++) hs[k] = lr + p.Persistence * (hs[k - 1] - lr);
        var days = Enumerable.Range(1, hs.Length).Select(d => (double)d).ToList();
        var spot = hs.Select(Ann).ToList();
        var avg = new List<double>();
        double cum = 0;
        for (int k = 0; k < hs.Length; k++) { cum += hs[k]; avg.Add(Ann(cum / (k + 1))); }
        Kit.Chart(col, Charts.XY(days, new List<LineSeries>
        {
            new("Volatilité prévue pour le jour j", spot, Theme.Primary, 2.2),
            new("Volatilité moyenne jusqu'à j", avg, Theme.Secondary, 2),
            new("Long terme", days.Select(_ => Ann(lr)).ToList(), Theme.Grey, 1.2, Dashed: true),
        }, new ChartOptions { Height = 260, YFormat = v => Fmt.Pct(v, 1) }, x => $"{x:0} j"),
        "La volatilité moyenne jusqu'à une échéance est ce qui compte pour évaluer une option de même maturité.");

        Kit.Table(col, new[] { "Horizon", "1 semaine", "1 mois", "3 mois", "6 mois" }, new List<string[]>
        {
            new[] { "Volatilité moyenne prévue", Fmt.Pct(avg[4], 1), Fmt.Pct(avg[20], 1), Fmt.Pct(avg[62], 1), Fmt.Pct(avg[^1], 1) },
        }, new[] { 2f, 1f, 1f, 1f, 1f });
    }

    // ---------------------------------------------------------------- 5. VaR
    private static void VarSection(ColumnDescriptor col, StudyResult s)
    {
        Kit.Section(col, "5", "Application : une VaR qui s'adapte",
            "Une bonne prévision de volatilité donne une meilleure VaR. VaR 99 % = 2,326 × volatilité prévue.");

        var rows = s.Models.Select(m => new[]
        {
            m.Name, m.Score!.VarExceptions.ToString(), Fmt.Num(m.Score.VarExpected, 1),
            Fmt.Pct(m.Score.VarExceptions / (double)s.OosDays, 2), Fmt.Num(m.Score.KupiecP, 3)
        }).ToList();
        Kit.Table(col, new[] { "Modèle", "Exceptions", "Attendues", "Taux", "Kupiec (p)" }, rows,
            new[] { 2.4f, 1f, 1f, 1f, 1f },
            cellColor: (i, j) => j == 4 ? (s.Models[i].Score!.KupiecP < 0.05 ? Theme.Coral : Theme.Green) : null);

        var gjr = s.Model("GJR");
        var hist = s.Model("HIST");
        double z = Stats.NormInv(Config.VarLevel);
        var r = s.Returns.Skip(s.OosStart).ToList();
        var dates = s.Dates.Skip(s.OosStart).ToList();
        var markers = gjr.Score!.ExceptionIdx.Select(k => new Marker(k, r[k])).ToList();
        Kit.Chart(col, Charts.Lines(dates, new List<LineSeries>
        {
            new("Rendement quotidien", r, "#C5CBD6", 1),
            new("- VaR 99 % GJR-GARCH", gjr.Oos.Select(h => -z * Math.Sqrt(h)).ToList(), Theme.Primary, 1.8),
            new("- VaR 99 % historique", hist.Oos.Select(h => -z * Math.Sqrt(h)).ToList(), Theme.Secondary, 1.3, Dashed: true),
        }, new ChartOptions { Height = 290, YFormat = v => Fmt.Pct(v, 0) }, markers, Theme.Coral, "Exception GJR"),
        "Période hors échantillon. Le test de Kupiec vérifie que le taux d'exceptions est compatible avec 1 % (p-valeur > 5 %, en vert).");

        Kit.Note(col, "Lecture",
            "La VaR fondée sur le GJR-GARCH monte dès le premier jour de crise, alors que la VaR historique attend que les mauvais jours " +
            "remplissent sa fenêtre. C'est pourquoi les banques utilisent des modèles de volatilité conditionnelle (filtered historical simulation).");
    }

    // ---------------------------------------------------------------- 6. Méthodologie
    private static void MethodologySection(ColumnDescriptor col, StudyResult s)
    {
        Kit.Section(col, "6", "Méthodologie et limites");
        Kit.SubTitle(col, "Protocole");
        Kit.Bullets(col, new[]
        {
            $"Les {s.OosDays} dernières séances sont réservées au test. Pour chaque jour, la prévision n'utilise que les données de la veille.",
            $"GARCH, GJR-GARCH et HAR sont ré-estimés tous les {Config.RefitEvery} jours sur une fenêtre croissante ({s.Refits} estimations au total).",
            "Maximum de vraisemblance gaussien avec ciblage de variance : la constante du modèle vaut variance empirique × (1 - persistance). Contraintes imposées par reparamétrage.",
            "Variance réalisée : estimateur de Garman-Klass plus le rendement de la nuit au carré, à partir des données ouverture, plus haut, plus bas, clôture.",
            "QLIKE = RV / h - ln(RV / h) - 1. Cette perte reste fiable quand la variance réalisée est bruitée (Patton, 2011).",
            "Diebold-Mariano : différence de pertes face à la volatilité historique, variance de Newey-West à 5 retards.",
        });
        Kit.SubTitle(col, "Limites");
        Kit.Bullets(col, new[]
        {
            "Les données quotidiennes gratuites ne permettent pas une vraie variance réalisée intrajournalière (données toutes les 5 minutes).",
            "La vraisemblance est gaussienne. Une loi de Student améliorerait la VaR dans les queues.",
            "Les prévisions décrivent un régime statistique. Elles ne prévoient pas les chocs eux-mêmes.",
            "Projet pédagogique : il ne constitue pas un conseil en investissement.",
        });
        Kit.SubTitle(col, "Architecture du code");
        Kit.Table(col, new[] { "Module", "Langage", "Rôle" }, new List<string[]>
        {
            new[] { "native/volengine.cpp", "C++17", "Log-vraisemblance et filtrage GARCH / GJR-GARCH" },
            new[] { "Models/Garch.cs", "C#", "Appel du C++ par P/Invoke, estimation, modèle HAR" },
            new[] { "Models/Optimizer.cs", "C#", "Simplexe de Nelder-Mead écrit à la main" },
            new[] { "Models/Forecaster.cs", "C#", "Prévisions glissantes hors échantillon des cinq modèles" },
            new[] { "Models/Evaluation.cs", "C#", "QLIKE, RMSE, Mincer-Zarnowitz, Diebold-Mariano, Kupiec" },
            new[] { "Data/MarketData.cs", "C#", "Téléchargement Yahoo Finance et cache CSV" },
            new[] { "Report/*.cs", "C#", "Graphiques SVG et mise en page PDF (QuestPDF)" },
        }, new[] { 2.2f, 0.8f, 3.2f }, leftCols: 3);
    }
}
