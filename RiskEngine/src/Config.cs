namespace RiskEngine;

public sealed record Asset(string Ticker, string Name, string Short, string Sector, double Weight);

public sealed record HistoricalScenario(string Name, DateTime Start, DateTime End, string Comment);

public sealed record ShockScenario(string Name, IReadOnlyDictionary<string, double> SectorShocks, double DefaultShock, string Comment);

/// <summary>
/// Paramètres du portefeuille et du moteur. Tout se change ici :
/// actifs, poids, montant, niveaux de confiance, fenêtres, scénarios.
/// </summary>
public static class Config
{
    public const double PortfolioValue = 10_000_000; // 10 M€

    public static readonly Asset[] Assets =
    {
        new("TTE.PA", "TotalEnergies",      "TTE", "Énergie",   0.15),
        new("MC.PA",  "LVMH",               "MC",  "Luxe",      0.14),
        new("BNP.PA", "BNP Paribas",        "BNP", "Banque",    0.13),
        new("AIR.PA", "Airbus",             "AIR", "Industrie", 0.12),
        new("SAN.PA", "Sanofi",             "SAN", "Santé",     0.12),
        new("SU.PA",  "Schneider Electric", "SU",  "Industrie", 0.12),
        new("OR.PA",  "L'Oréal",            "OR",  "Luxe",      0.11),
        new("CS.PA",  "AXA",                "CS",  "Assurance", 0.11),
    };

    public const string Benchmark = "^FCHI";      // CAC 40
    public const string BenchmarkName = "CAC 40";

    public static readonly DateTime HistoryStart = new(2007, 1, 1);

    public const double VarLevel = 0.99;          // VaR 99 % (Bâle)
    public const double EsLevel = 0.975;          // Expected Shortfall 97,5 % (FRTB)
    public const int VarWindow = 500;             // ~2 ans de séances
    public const int BacktestDays = 250;          // fenêtre réglementaire de Bâle
    public const int BacktestLongDays = 750;      // ~3 ans, pour un test plus robuste
    public const int McSimulations = 200_000;
    public const double StudentNu = 5;            // degrés de liberté de la Student
    public const double EwmaLambda = 0.94;        // RiskMetrics
    public const ulong Seed = 20261118;

    public static readonly HistoricalScenario[] HistoricalScenarios =
    {
        new("Faillite de Lehman Brothers", new(2008, 9, 12), new(2008, 10, 10), "Crise financière mondiale"),
        new("Crise de la dette souveraine", new(2011, 7, 22), new(2011, 9, 22), "Zone euro, été 2011"),
        new("Krach du Covid-19", new(2020, 2, 19), new(2020, 3, 18), "Confinements et chute des marchés"),
        new("Invasion de l'Ukraine", new(2022, 2, 10), new(2022, 3, 7), "Choc géopolitique et énergétique"),
        new("Dissolution de l'Assemblée", new(2024, 6, 7), new(2024, 6, 14), "Choc politique français"),
    };

    public static readonly ShockScenario[] ShockScenarios =
    {
        new("Choc actions uniforme", new Dictionary<string, double>(), -0.20, "Toutes les actions baissent de 20 %"),
        new("Crise bancaire européenne",
            new Dictionary<string, double> { ["Banque"] = -0.35, ["Assurance"] = -0.30 }, -0.15,
            "Banques -35 %, assurances -30 %, reste -15 %"),
        new("Ralentissement chinois",
            new Dictionary<string, double> { ["Luxe"] = -0.30, ["Industrie"] = -0.12, ["Énergie"] = -0.10 }, -0.06,
            "Luxe -30 %, industrie -12 %, énergie -10 %, reste -6 %"),
        new("Chute du pétrole",
            new Dictionary<string, double> { ["Énergie"] = -0.30 }, -0.04,
            "Énergie -30 %, reste -4 %"),
    };
}
