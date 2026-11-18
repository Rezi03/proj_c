using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Quant.Report;
using RiskEngine.Analytics;

namespace RiskEngine.Report;

/// <summary>Construit le rapport PDF du moteur de risque.</summary>
public static class RiskReport
{
    private const string Title = "Moteur de risque de marché";

    public static string Build(RiskResult r, string outputDir)
    {
        Kit.Init();
        Directory.CreateDirectory(outputDir);
        string path = Path.Combine(outputDir, $"Rapport_Risque_Portefeuille_{r.AsOf:yyyy-MM-dd}.pdf");
        string dateLabel = $"Données au {Fmt.LongDate(r.AsOf)}";

        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                Kit.StandardPage(page, Title, dateLabel, showHeader: false);
                page.Content().Column(col => { col.Spacing(10); CoverPage(col, r, dateLabel); });
            });
            doc.Page(page =>
            {
                Kit.StandardPage(page, Title, dateLabel);
                page.Content().Column(col =>
                {
                    col.Spacing(9);
                    PortfolioSection(col, r);
                    col.Item().PageBreak();
                    VarSection(col, r);
                    col.Item().PageBreak();
                    MonteCarloSection(col, r);
                    col.Item().PageBreak();
                    ContributionSection(col, r);
                    col.Item().PageBreak();
                    BacktestSection(col, r);
                    col.Item().PageBreak();
                    StressSection(col, r);
                    col.Item().PageBreak();
                    MethodologySection(col, r);
                });
            });
        })
        .WithMetadata(new DocumentMetadata { Title = "Rapport de risque de marché", Author = "Rezo Sabashvili" })
        .GeneratePdf(path);

        return path;
    }

    private static MethodResult Method(RiskResult r, string shortName) => r.Methods.First(m => m.Short == shortName);

    private static string Plural(int count, string word) => $"{count} {word}{(count > 1 ? "s" : "")}";

    private static string ZoneColor(string zone) => zone == "Verte" ? Theme.Green : zone == "Orange" ? Theme.Amber : Theme.Red;

    // ---------------------------------------------------------------- Couverture
    private static void CoverPage(ColumnDescriptor col, RiskResult r, string dateLabel)
    {
        Kit.Cover(col, "Projet personnel · Gestion des risques",
            Title,
            $"Portefeuille de {Config.Assets.Length} grandes actions françaises · {Fmt.MEur(Config.PortfolioValue, 0)} · " +
            "VaR, Expected Shortfall, Monte Carlo C++, backtesting bâlois et stress tests",
            $"{dateLabel}  ·  {Fmt.Num(r.Observations, 0)} séances depuis {Fmt.Date(r.FirstDate)}");

        var hs = Method(r, "HS");
        var worst = r.Stress.OrderBy(s => s.Pnl).First();
        Kit.SubTitle(col, "Synthèse");
        Kit.Kpis(col, new List<Kpi>
        {
            new("VaR 99 % à 1 jour", Fmt.Eur(hs.Var), $"{Fmt.Pct(hs.Var / Config.PortfolioValue)} du portefeuille", Theme.Navy),
            new("Expected Shortfall 97,5 %", Fmt.Eur(hs.Es), "moyenne des 2,5 % pires journées", Theme.Navy),
            new("Volatilité annualisée", Fmt.Pct(r.PortfolioVolAnn, 1), $"{Config.BenchmarkName} : {Fmt.Pct(r.BenchmarkVolAnn, 1)}"),
            new("Backtesting 250 jours", $"Zone {r.BtHist.Zone.ToLowerInvariant()}",
                $"{Plural(r.BtHist.Exceptions.Count, "exception")} pour {Fmt.Num(r.BtHist.Expected, 1)} attendues", ZoneColor(r.BtHist.Zone)),
            new("Pire stress test", Fmt.Eur(worst.Pnl), worst.Name, Theme.Red),
            new("Bêta au CAC 40", Fmt.Num(r.PortfolioBeta, 2), $"perte max. sur 3 ans : {Fmt.Pct(r.MaxDrawdown, 1)}"),
        });

        Kit.SubTitle(col, "Ce qu'il faut retenir");
        var mcN = Method(r, "MC-N");
        var mcT = Method(r, "MC-t");
        int top = Array.IndexOf(r.ComponentVar, r.ComponentVar.Max());
        var topAsset = Config.Assets[top];
        Kit.Bullets(col, new[]
        {
            $"Sur une journée, le portefeuille a 1 % de risque de perdre plus de {Fmt.Eur(hs.Var)} (VaR historique 99 %). " +
            $"Sur les 2,5 % pires journées, la perte moyenne est de {Fmt.Eur(hs.Es)} (Expected Shortfall 97,5 %).",
            $"Les queues épaisses comptent. Avec une loi de Student, la VaR Monte Carlo passe de {Fmt.Eur(mcN.Var)} à {Fmt.Eur(mcT.Var)}, " +
            $"soit {Fmt.SignedPct(mcT.Var / mcN.Var - 1)}. La loi normale sous-estime les krachs.",
            $"Le backtesting sur 250 jours compte {Plural(r.BtHist.Exceptions.Count, "dépassement")} de la VaR historique pour " +
            $"{Fmt.Num(r.BtHist.Expected, 1)} attendus. Le modèle est en zone {r.BtHist.Zone.ToLowerInvariant()} selon Bâle.",
            $"{topAsset.Name} est le premier contributeur au risque. Il pèse {Fmt.Pct(topAsset.Weight, 0)} du portefeuille " +
            $"mais {Fmt.Pct(r.ComponentVar[top] / r.ParametricVar, 0)} de la VaR.",
            $"La diversification réduit la VaR de {Fmt.Eur(r.DiversificationBenefit)} par rapport à la somme des VaR individuelles.",
            $"Le scénario le plus coûteux est « {worst.Name} », avec une perte de {Fmt.Eur(worst.Pnl)} ({Fmt.SignedPct(worst.PnlPct)}).",
        });

        Kit.TechBox(col, "Sous le capot", new[]
        {
            ("Langages", "C# (.NET 10) pour les calculs et le rapport, C++17 pour le moteur Monte Carlo"),
            ("Moteur Monte Carlo", $"{MonteCarlo.EngineLabel}, {Fmt.Num(Config.McSimulations, 0)} scénarios en {Fmt.Num(r.McStudent.Milliseconds, 0)} ms"),
            ("Données", $"Yahoo Finance, cours ajustés des dividendes, {Fmt.Num(r.Observations, 0)} séances"),
            ("Rapport", "Généré automatiquement (QuestPDF), graphiques vectoriels faits maison en SVG"),
        });
    }

    // ---------------------------------------------------------------- 1. Portefeuille
    private static void PortfolioSection(ColumnDescriptor col, RiskResult r)
    {
        Kit.Section(col, "1", "Le portefeuille", "Huit valeurs du CAC 40 réparties sur six secteurs, montants fixes en euros.");
        var rows = new List<string[]>();
        for (int i = 0; i < Config.Assets.Length; i++)
        {
            var a = Config.Assets[i];
            rows.Add(new[] { a.Name, a.Sector, Fmt.Pct(a.Weight, 0), Fmt.Eur(r.Exposure[i]), Fmt.Pct(r.AssetVolAnn[i], 1), Fmt.Num(r.AssetBeta[i], 2) });
        }
        rows.Add(new[] { "Portefeuille", "", "100 %", Fmt.Eur(Config.PortfolioValue), Fmt.Pct(r.PortfolioVolAnn, 1), Fmt.Num(r.PortfolioBeta, 2) });
        Kit.Table(col, new[] { "Société", "Secteur", "Poids", "Montant", "Volatilité ann.", "Bêta" }, rows,
            new[] { 2.4f, 1.4f, 0.9f, 1.5f, 1.3f, 0.8f }, highlightRow: rows.Count - 1, leftCols: 2);

        Kit.SubTitle(col, "Performance sur trois ans, base 100");
        Kit.Chart(col, Charts.Lines(r.PerfDates, new List<LineSeries>
        {
            new("Portefeuille", r.PerfPortfolio, Theme.Navy, 2),
            new(Config.BenchmarkName, r.PerfBenchmark, Theme.Gold, 1.8),
        }, new ChartOptions { Height = 270, YFormat = v => Fmt.Num(v, 0) }),
        "Dividendes réinvestis pour les actions (cours ajustés). Le CAC 40 est un indice de prix, hors dividendes.");

        Kit.SubTitle(col, "Volatilité glissante sur 21 jours, annualisée");
        Kit.Chart(col, Charts.Lines(r.PerfDates, new List<LineSeries>
        {
            new("Portefeuille", r.RollingVol, Theme.Navy, 1.6),
            new(Config.BenchmarkName, r.RollingVolBench, Theme.Gold, 1.4),
        }, new ChartOptions { Height = 230, YFormat = v => Fmt.Pct(v, 0), YMin = 0 }));
    }

    // ---------------------------------------------------------------- 2. VaR et ES
    private static void VarSection(ColumnDescriptor col, RiskResult r)
    {
        Kit.Section(col, "2", "VaR et Expected Shortfall",
            "Cinq méthodes de calcul, de la plus simple à la plus réaliste, sur les 500 dernières séances.");

        var rows = r.Methods.Select(m => new[]
        {
            m.Name, Fmt.Eur(m.Var), Fmt.Eur(m.Var * Math.Sqrt(10)), Fmt.Eur(m.Es), Fmt.Pct(m.Es / Config.PortfolioValue)
        }).ToList();
        Kit.Table(col, new[] { "Méthode", "VaR 99 % 1 j", "VaR 99 % 10 j", "ES 97,5 % 1 j", "ES / portefeuille" }, rows,
            new[] { 2.2f, 1.3f, 1.3f, 1.3f, 1.2f });
        col.Item().Text("La VaR à 10 jours applique la règle de la racine du temps (VaR 1 j × √10), comme dans le cadre bâlois historique.")
            .FontSize(7.5f).Italic().FontColor(Theme.Grey);

        var hs = Method(r, "HS");
        Kit.SubTitle(col, "Distribution des P&L quotidiens réels (500 séances)");
        Kit.Chart(col, Charts.Histogram(r.WindowPnl, 50, new ChartOptions { Height = 290 },
            new List<VLine>
            {
                new(-hs.Var, $"VaR 99 % : {Fmt.KEur(-hs.Var)}", Theme.Red),
                new(-hs.Es, $"ES 97,5 % : {Fmt.KEur(-hs.Es)}", Theme.Navy),
            },
            v => Fmt.KEur(v), tailBelow: -hs.Var),
            "Chaque barre compte les jours où le portefeuille actuel aurait gagné ou perdu ce montant. En rouge, les pertes au-delà de la VaR.");

        Kit.Note(col, "Pourquoi l'Expected Shortfall ?",
            "La VaR donne un seuil mais ne dit rien de la perte au-delà. L'Expected Shortfall mesure la perte moyenne dans les pires cas. " +
            "Depuis la réforme FRTB du Comité de Bâle, c'est la mesure de référence pour le capital du trading book, au niveau de 97,5 %.");
    }

    // ---------------------------------------------------------------- 3. Monte Carlo
    private static void MonteCarloSection(ColumnDescriptor col, RiskResult r)
    {
        Kit.Section(col, "3", "Simulation Monte Carlo en C++",
            $"{Fmt.Num(Config.McSimulations, 0)} journées de marché simulées à partir des volatilités et corrélations observées.");

        Kit.Para(col,
            "Le moteur C++ décompose la matrice de covariance (Cholesky), tire des chocs corrélés pour les huit actions, puis calcule " +
            "le P&L de chaque scénario. Deux lois sont comparées. La loi normale est la référence des manuels. La loi de Student " +
            $"à {Fmt.Num(Config.StudentNu, 0)} degrés de liberté a la même volatilité mais produit des krachs plus fréquents, comme sur les vrais marchés.");

        var mcN = r.McNormal;
        var mcT = r.McStudent;
        Kit.Kpis(col, new List<Kpi>
        {
            new("VaR 99 % normale", Fmt.Eur(mcN.Var), $"ES 97,5 % : {Fmt.Eur(mcN.Es)}"),
            new("VaR 99 % Student-t", Fmt.Eur(mcT.Var), $"ES 97,5 % : {Fmt.Eur(mcT.Es)}", Theme.Red),
            new("Temps de calcul", $"{Fmt.Num(mcN.Milliseconds + mcT.Milliseconds, 0)} ms",
                $"{Fmt.Num(2 * Config.McSimulations, 0)} scénarios, {(mcN.Native ? "moteur C++" : "moteur C#")}"),
        });

        Kit.SubTitle(col, "Distribution simulée avec la loi de Student");
        Kit.Chart(col, Charts.Histogram(mcT.Pnl, 70, new ChartOptions { Height = 300 },
            new List<VLine>
            {
                new(-mcT.Var, $"VaR Student : {Fmt.KEur(-mcT.Var)}", Theme.Red),
                new(-mcN.Var, $"VaR normale : {Fmt.KEur(-mcN.Var)}", Theme.Blue),
            },
            v => Fmt.KEur(v), tailBelow: -mcT.Var, color: Theme.NavySoft),
            "Les scénarios les plus extrêmes (0,1 % de chaque côté) sont coupés pour la lisibilité. Ils restent dans le calcul.");

        Kit.Note(col, "Lecture",
            $"À volatilité égale, la loi de Student donne une VaR {Fmt.SignedPct(mcT.Var / mcN.Var - 1)} et une ES " +
            $"{Fmt.SignedPct(mcT.Es / mcN.Es - 1)} par rapport à la loi normale. L'écart grandit à mesure que l'on va loin dans la queue, " +
            "ce qui explique pourquoi les régulateurs ont abandonné la VaR normale pour l'Expected Shortfall.");
    }

    // ---------------------------------------------------------------- 4. Contributions
    private static void ContributionSection(ColumnDescriptor col, RiskResult r)
    {
        Kit.Section(col, "4", "D'où vient le risque ?",
            "Décomposition d'Euler de la VaR paramétrique : la somme des contributions est égale à la VaR du portefeuille.");

        var labels = Config.Assets.Select(a => a.Name).ToList();
        var weightPct = Config.Assets.Select(a => a.Weight * 100).ToList();
        var contribPct = r.ComponentVar.Select(c => c / r.ParametricVar * 100).ToList();
        Kit.Chart(col, Charts.HBars(labels, new List<BarGroup>
        {
            new("Poids dans le portefeuille", weightPct, Theme.GreyLight),
            new("Contribution à la VaR", contribPct, Theme.Navy),
        }, 760, v => Fmt.Num(v, 1) + " %"),
        "Une action dont la contribution dépasse le poids ajoute plus de risque qu'elle ne pèse.");

        var rows = new List<string[]>();
        for (int i = 0; i < Config.Assets.Length; i++)
            rows.Add(new[]
            {
                Config.Assets[i].Name, Fmt.Eur(r.StandaloneVar[i]), Fmt.Eur(r.ComponentVar[i]),
                Fmt.Pct(r.ComponentVar[i] / r.ParametricVar, 1), Fmt.Pct(Config.Assets[i].Weight, 0)
            });
        rows.Add(new[] { "Total", Fmt.Eur(r.StandaloneVar.Sum()), Fmt.Eur(r.ParametricVar), "100 %", "100 %" });
        Kit.Table(col, new[] { "Société", "VaR isolée", "Contribution", "Part de la VaR", "Poids" }, rows,
            new[] { 2.2f, 1.3f, 1.3f, 1.2f, 0.9f }, highlightRow: rows.Count - 1);

        Kit.Chart(col, Charts.Heatmap(Config.Assets.Select(a => a.Short).ToList(), r.Correlation, 50),
            "Plus la case est foncée, plus les deux actions montent et baissent ensemble.", 0.66f,
            "Corrélations des rendements quotidiens (500 séances)");

        int n = Config.Assets.Length;
        (int I, int J, double V) hi = (0, 1, double.MinValue), lo = (0, 1, double.MaxValue);
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                double v = r.Correlation[i, j];
                if (v > hi.V) hi = (i, j, v);
                if (v < lo.V) lo = (i, j, v);
            }
        Kit.Note(col, "Lecture",
            $"Le couple le plus lié est {Config.Assets[hi.I].Name} et {Config.Assets[hi.J].Name} ({Fmt.Num(hi.V, 2)}). " +
            $"Le moins lié est {Config.Assets[lo.I].Name} et {Config.Assets[lo.J].Name} ({Fmt.Num(lo.V, 2)}). " +
            "Des actions peu corrélées se compensent les jours de baisse : c'est la source du gain de diversification.");
    }

    // ---------------------------------------------------------------- 5. Backtesting
    private static void BacktestSection(ColumnDescriptor col, RiskResult r)
    {
        Kit.Section(col, "5", "Backtesting",
            "Chaque jour, on compare la VaR calculée la veille à la perte réellement constatée.");

        var bt = r.BtHist;
        var markers = bt.Exceptions.Select(k => new Marker(k, bt.Pnl[k])).ToList();
        Kit.Chart(col, Charts.Lines(bt.Dates, new List<LineSeries>
        {
            new("P&L quotidien", bt.Pnl, Theme.GreyLight, 1.1),
            new("- VaR historique 99 %", bt.Var.Select(v => -v).ToList(), Theme.Navy, 2),
            new("- VaR EWMA 99 %", r.BtEwma.Var.Select(v => -v).ToList(), Theme.Gold, 1.6, Dashed: true),
        }, new ChartOptions { Height = 300, YFormat = v => Fmt.KEur(v) }, markers, Theme.Red, "Exception"),
        "Une exception est un jour où la perte dépasse la VaR prévue la veille (point rouge, VaR historique).");

        var all = new[] { r.BtHist, r.BtEwma, r.BtHistLong, r.BtEwmaLong };
        var rows = all.Select(b => new[]
        {
            $"{b.Method} ({b.Days} j)", b.Exceptions.Count.ToString(), Fmt.Num(b.Expected, 1), b.Zone,
            Fmt.Num(b.KupiecP, 3), Fmt.Num(b.ChristoffersenP, 3)
        }).ToList();
        Kit.Table(col, new[] { "Modèle", "Exceptions", "Attendues", "Zone Bâle", "Kupiec (p)", "Christoffersen (p)" }, rows,
            new[] { 2.6f, 1f, 1f, 1f, 1f, 1.3f },
            cellColor: (i, j) => j == 3 ? ZoneColor(all[i].Zone) : j >= 4 && double.TryParse(rows[i][j], System.Globalization.NumberStyles.Any, Fmt.Fr, out var p) && p < 0.05 ? Theme.Red : null);

        Kit.Bullets(col, new[]
        {
            "Feux tricolores de Bâle sur 250 jours : 0 à 4 exceptions en zone verte, 5 à 9 en zone orange (capital majoré), 10 ou plus en zone rouge.",
            "Test de Kupiec : le nombre d'exceptions est-il cohérent avec 1 % ? Une p-valeur inférieure à 5 % (en rouge) rejette le modèle.",
            "Test de Christoffersen : les exceptions arrivent-elles en grappes ? Un modèle trop lent à réagir aux crises échoue à ce test.",
        });
    }

    // ---------------------------------------------------------------- 6. Stress tests
    private static void StressSection(ColumnDescriptor col, RiskResult r)
    {
        Kit.Section(col, "6", "Stress tests",
            "Que perdrait le portefeuille d'aujourd'hui si une crise passée se reproduisait, ou si un choc hypothétique survenait ?");

        var ordered = r.Stress.OrderBy(s => s.Pnl).ToList();
        Kit.Chart(col, Charts.HBars(ordered.Select(s => s.Name).ToList(), new List<BarGroup>
        {
            new("P&L", ordered.Select(s => s.Pnl / 1000).ToList(), Theme.Red),
        }, 760, v => Fmt.Num(v, 0) + " k€", v => v < 0 ? Theme.Red : Theme.Green, labelWidth: 190));

        var rows = r.Stress.Select(s => new[]
        {
            s.Name + (s.Partial ? " *" : ""), s.Type, s.Period, Fmt.Eur(s.Pnl), Fmt.SignedPct(s.PnlPct),
            s.BenchmarkMove.HasValue ? Fmt.SignedPct(s.BenchmarkMove.Value) : "-"
        }).ToList();
        Kit.Table(col, new[] { "Scénario", "Type", "Période", "P&L", "En %", Config.BenchmarkName }, rows,
            new[] { 2.3f, 1.1f, 1.9f, 1.4f, 0.8f, 0.8f },
            cellColor: (i, j) => j == 3 || j == 4 ? (r.Stress[i].Pnl < 0 ? Theme.Red : Theme.Green) : null, leftCols: 3);

        if (r.Stress.Any(s => s.Partial))
            col.Item().Text("* Une action n'était pas encore cotée à cette date : sa variation est remplacée par celle du CAC 40.")
                .FontSize(7.5f).Italic().FontColor(Theme.Grey);

        var hs = Method(r, "HS");
        var worst = ordered.First();
        Kit.Note(col, "Lecture",
            $"Le pire scénario représente {Fmt.Num(-worst.Pnl / hs.Var, 1)} fois la VaR 99 % à un jour. Les stress tests complètent la VaR : " +
            "ils mesurent des chocs rares et longs que les modèles statistiques, calibrés sur deux ans, ne voient pas.");
    }

    // ---------------------------------------------------------------- 7. Méthodologie
    private static void MethodologySection(ColumnDescriptor col, RiskResult r)
    {
        Kit.Section(col, "7", "Méthodologie et limites");
        Kit.SubTitle(col, "Méthodes");
        Kit.Bullets(col, new[]
        {
            "VaR historique : P&L du portefeuille actuel rejoué sur les 500 dernières séances, quantile à 1 %. Aucune hypothèse de loi.",
            "VaR paramétrique : σ = √(w'Σw) avec la covariance des rendements, VaR = 2,326 × σ et ES = 2,338 × σ sous loi normale.",
            "EWMA (RiskMetrics) : chaque jour, Σ = λΣ + (1 - λ) r r' avec λ = 0,94. Les chocs récents pèsent davantage.",
            "Monte Carlo : chocs corrélés par décomposition de Cholesky, loi normale ou Student-t ramenée à la même variance.",
            "Contributions : décomposition d'Euler, contribution de l'action i = 2,326 × w(i) × (Σw)(i) / σ.",
            "Backtesting : test de couverture de Kupiec (1995) et test d'indépendance de Christoffersen (1998), khi-deux à 1 degré de liberté.",
        });
        Kit.SubTitle(col, "Limites");
        Kit.Bullets(col, new[]
        {
            "Le portefeuille est supposé constant : aucune vente ni couverture pendant la période de détention.",
            "La VaR à 10 jours par racine du temps néglige l'autocorrélation et la liquidité.",
            "Les rendements sont en euros et les données viennent d'une source publique gratuite, sans contrôle qualité de niveau bancaire.",
            "Projet pédagogique : il ne constitue pas un conseil en investissement.",
        });
        Kit.SubTitle(col, "Architecture du code");
        Kit.Table(col, new[] { "Module", "Langage", "Rôle" }, new List<string[]>
        {
            new[] { "native/riskengine.cpp", "C++17", "Monte Carlo multi-thread, Cholesky, VaR et ES" },
            new[] { "Analytics/MonteCarlo.cs", "C#", "Appel du C++ par P/Invoke, repli en C# si besoin" },
            new[] { "Analytics/RiskAnalysis.cs", "C#", "Rendements, VaR, ES, contributions, bêtas" },
            new[] { "Analytics/Backtest.cs", "C#", "VaR glissante, Kupiec, Christoffersen, zones de Bâle" },
            new[] { "Analytics/StressTests.cs", "C#", "Scénarios historiques et hypothétiques" },
            new[] { "Data/MarketData.cs", "C#", "Téléchargement Yahoo Finance et cache CSV" },
            new[] { "Report/*.cs", "C#", "Graphiques SVG et mise en page PDF (QuestPDF)" },
        }, new[] { 2.2f, 0.8f, 3.2f }, leftCols: 3);
    }
}
