using System.Globalization;

namespace Quant.Report;

/// <summary>
/// Charte « laboratoire quant » propre à ce projet : fond blanc, violet et sarcelle,
/// police Avenir Next, chiffres en police à chasse fixe (Menlo).
/// </summary>
public static class Theme
{
    public const string Ink = "#111827";
    public const string Text = "#1F2937";
    public const string Primary = "#5B21B6";        // violet profond
    public const string PrimaryLight = "#A78BFA";
    public const string PrimarySoft = "#F1ECFE";
    public const string Secondary = "#0D9488";      // sarcelle
    public const string SecondarySoft = "#E8F6F4";
    public const string Amber = "#F59E0B";
    public const string Coral = "#E4572E";
    public const string Green = "#15803D";
    public const string Grey = "#6B7280";
    public const string GreyLight = "#A1A1AA";
    public const string Rule = "#E5E7EB";

    public const string Font = "Avenir Next";
    public const string Mono = "Menlo";

    // Graphiques
    public const string ChartFont = "Avenir Next, Helvetica, Arial, sans-serif";
    public const string ChartGrid = "#EEF0F4";
    public const string ChartAxis = "#C4C8D0";
    public const string ChartLabel = "#6B7280";
    public const string ChartText = Ink;
    public const string ChartMain = Primary;
    public const string ChartNegative = Coral;
    public const string HeatPositive = Primary;
}

/// <summary>Mise en forme des nombres à la française (espace simple comme séparateur de milliers).</summary>
public static class Fmt
{
    public static readonly CultureInfo Fr = BuildFr();
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static CultureInfo BuildFr()
    {
        var c = (CultureInfo)CultureInfo.GetCultureInfo("fr-FR").Clone();
        c.NumberFormat.NumberGroupSeparator = " ";
        c.NumberFormat.PercentGroupSeparator = " ";
        c.NumberFormat.CurrencyGroupSeparator = " ";
        c.NumberFormat.NegativeSign = "-";
        return c;
    }

    public static string Eur(double v) => v.ToString("N0", Fr) + " €";
    public static string KEur(double v) => (v / 1000).ToString("N0", Fr) + " k€";
    public static string MEur(double v, int d = 1) => (v / 1e6).ToString("N" + d, Fr) + " M€";
    public static string Pct(double v, int d = 2) => (v * 100).ToString("N" + d, Fr) + " %";
    public static string SignedPct(double v, int d = 1) => (v >= 0 ? "+" : "") + Pct(v, d);
    public static string Num(double v, int d = 2) => v.ToString("N" + d, Fr);
    public static string Date(DateTime d) => d.ToString("dd/MM/yyyy", Fr);
    public static string LongDate(DateTime d) => d.ToString("d MMMM yyyy", Fr);
    public static string MonthYear(DateTime d) => d.ToString("MMM yyyy", Fr);

    /// <summary>Format invariant, réservé au SVG (jamais de virgule décimale).</summary>
    public static string Svg(double v) => v.ToString("0.##", Inv);
}
