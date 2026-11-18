using System.Globalization;

namespace Quant.Report;

/// <summary>Charte graphique commune au PDF et aux graphiques.</summary>
public static class Theme
{
    public const string Navy = "#0B1F3A";
    public const string NavySoft = "#1C355E";
    public const string Gold = "#C8A24A";
    public const string Blue = "#2E6FD8";
    public const string Teal = "#1A9E8F";
    public const string Red = "#D64545";
    public const string Green = "#2E9E5B";
    public const string Amber = "#E0A030";
    public const string Violet = "#7B61FF";
    public const string Grey = "#6B7280";
    public const string GreyLight = "#9AA3B2";
    public const string Light = "#F4F6FA";
    public const string Border = "#DDE2EA";
    public const string Text = "#1D2433";
    public const string Cream = "#F8F4EA";

    // Graphiques
    public const string ChartFont = "Helvetica, Arial, sans-serif";
    public const string ChartGrid = "#E8ECF2";
    public const string ChartAxis = "#9AA3B2";
    public const string ChartLabel = "#6B7280";
    public const string ChartText = Text;
    public const string ChartMain = Blue;
    public const string ChartNegative = Red;
    public const string HeatPositive = Navy;
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
