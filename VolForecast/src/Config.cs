namespace VolForecast;

/// <summary>Paramètres de l'étude de prévision. Tout se change ici.</summary>
public static class Config
{
    public const string Symbol = "^FCHI";          // CAC 40
    public const string Name = "CAC 40";
    public static readonly DateTime HistoryStart = new(2005, 1, 1);

    public const int OutOfSampleDays = 1250;        // ~5 ans testés hors échantillon
    public const int RefitEvery = 21;               // ré-estimation des modèles chaque mois
    public const int NaiveWindow = 21;              // volatilité historique sur 1 mois
    public const double EwmaLambda = 0.94;          // RiskMetrics
    public const double VarLevel = 0.99;
    public const int TermStructureDays = 126;       // horizon de la courbe de prévision (6 mois)
    public const int DieboldMarianoLag = 5;         // retards Newey-West
}
