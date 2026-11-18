using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Quant.Report;

public sealed record Kpi(string Label, string Value, string? Sub = null, string? Color = null);

/// <summary>
/// Mise en page « laboratoire quant » : couverture blanche typographique, sections numérotées 01, 02…,
/// indicateurs soulignés d'un trait de couleur, tableaux épurés, chiffres en police à chasse fixe.
/// </summary>
public static class Kit
{
    /// <summary>Largeur utile d'une page A4 avec des marges de 1,75 cm (en points).</summary>
    public const float ContentWidth = 495f;

    public static void Init()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    private static bool HasGreek(string s) => s.Any(ch => ch >= 'Ͱ' && ch <= 'Ͽ');

    public static void StandardPage(PageDescriptor page, string reportTitle, string dateLabel, bool showHeader = true)
    {
        page.Size(PageSizes.A4);
        page.MarginHorizontal(1.75f, Unit.Centimetre);
        page.MarginVertical(1.4f, Unit.Centimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontFamily(Theme.Font).FontSize(9.5f).FontColor(Theme.Text).LineHeight(1.4f));

        if (showHeader)
            page.Header().PaddingBottom(16).Row(r =>
            {
                r.RelativeItem().Text(t =>
                {
                    t.Span("VOL.LAB").FontFamily(Theme.Mono).FontSize(7.5f).Bold().FontColor(Theme.Primary);
                    t.Span("   " + reportTitle).FontSize(7.5f).FontColor(Theme.Grey);
                });
                r.AutoItem().Text(dateLabel).FontFamily(Theme.Mono).FontSize(7).FontColor(Theme.Grey);
            });

        page.Footer().Row(r =>
        {
            r.RelativeItem().Text("Rezo Sabashvili  ·  Recherche quantitative personnelle  ·  C# .NET 10 et C++")
             .FontSize(7.5f).FontColor(Theme.Grey);
            r.AutoItem().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontFamily(Theme.Mono).FontSize(7.5f).FontColor(Theme.Primary));
                t.Span("p. ");
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    }

    /// <summary>Couverture typographique : grand titre avec un mot mis en couleur, double filet violet et sarcelle.</summary>
    public static void Cover(ColumnDescriptor col, string kicker, string before, string accent, string after, string subtitle, string dateLine)
    {
        col.Item().PaddingTop(18).Text(kicker.ToUpperInvariant()).FontFamily(Theme.Mono).FontSize(8).Bold().FontColor(Theme.Primary);
        col.Item().Text(t =>
        {
            t.DefaultTextStyle(s => s.FontSize(31).Bold().FontColor(Theme.Ink).LineHeight(1.05f));
            t.Span(before);
            t.Span(accent).FontColor(Theme.Primary);
            t.Span(after);
        });
        col.Item().Text(subtitle).FontSize(10.5f).FontColor(Theme.Grey);
        col.Item().PaddingTop(4).Row(r =>
        {
            r.RelativeItem(3).Height(4).Background(Theme.Primary);
            r.RelativeItem(1).Height(4).Background(Theme.Secondary);
        });
        col.Item().Text(dateLine).FontFamily(Theme.Mono).FontSize(7.5f).FontColor(Theme.Grey);
    }

    /// <summary>Chiffre vedette de la couverture, avec une étiquette de régime à droite.</summary>
    public static void HeroStat(ColumnDescriptor col, string label, string value, string sub, string tag, string tagColor, string tagBackground)
    {
        col.Item().PaddingVertical(6).Row(r =>
        {
            r.RelativeItem().Column(c =>
            {
                c.Item().Text(label.ToUpperInvariant()).FontFamily(Theme.Mono).FontSize(7.5f).Bold().FontColor(Theme.Grey);
                c.Item().Text(value).FontSize(50).Bold().FontColor(Theme.Primary).LineHeight(1f);
                c.Item().Text(sub).FontColor(Theme.Grey);
            });
            r.ConstantItem(170).AlignMiddle().Background(tagBackground).Padding(14).Column(c =>
            {
                c.Item().Text("RÉGIME DE MARCHÉ").FontFamily(Theme.Mono).FontSize(7).Bold().FontColor(tagColor);
                c.Item().Text(tag).FontSize(20).Bold().FontColor(tagColor);
            });
        });
    }

    public static void Section(ColumnDescriptor col, string number, string title, string? lead = null)
    {
        col.Item().Text(number.PadLeft(2, '0')).FontFamily(Theme.Mono).FontSize(10).Bold().FontColor(Theme.Primary);
        col.Item().Text(title).FontSize(21).Bold().FontColor(Theme.Ink).LineHeight(1.1f);
        col.Item().Width(36).Height(3).Background(Theme.Secondary);
        if (lead != null) col.Item().PaddingTop(2).Text(lead).FontColor(Theme.Grey);
    }

    public static void SubTitle(ColumnDescriptor col, string s)
        => col.Item().PaddingTop(6).Text(s).FontSize(10.5f).Bold().FontColor(Theme.Primary);

    public static void Para(ColumnDescriptor col, string s) => col.Item().Text(s).Justify();

    /// <summary>Liste numérotée 01, 02, 03…</summary>
    public static void Bullets(ColumnDescriptor col, IEnumerable<string> items)
    {
        col.Item().Column(c =>
        {
            c.Spacing(5);
            int i = 1;
            foreach (var s in items)
            {
                string num = (i++).ToString("00");
                c.Item().Row(r =>
                {
                    r.ConstantItem(24).Text(num).FontFamily(Theme.Mono).FontSize(8.5f).Bold().FontColor(Theme.Primary);
                    r.RelativeItem().Text(s).Justify();
                });
            }
        });
    }

    public static void Note(ColumnDescriptor col, string title, string body)
    {
        col.Item().Background(Theme.SecondarySoft).Padding(12).Column(c =>
        {
            c.Item().Text("→  " + title).Bold().FontColor(Theme.Secondary);
            c.Item().PaddingTop(2).Text(body).Justify();
        });
    }

    /// <summary>Indicateurs alignés, chacun souligné à gauche d'un trait de couleur (pas de boîte).</summary>
    public static void Kpis(ColumnDescriptor col, IList<Kpi> items, int perRow = 4)
    {
        for (int i = 0; i < items.Count; i += perRow)
        {
            var chunk = items.Skip(i).Take(perRow).ToList();
            col.Item().PaddingVertical(4).Row(r =>
            {
                r.Spacing(14);
                foreach (var k in chunk)
                {
                    r.RelativeItem().BorderLeft(2.5f).BorderColor(k.Color ?? Theme.Primary).PaddingLeft(10).Column(c =>
                    {
                        c.Item().Text(k.Label.ToUpperInvariant()).FontFamily(Theme.Mono).FontSize(6.5f).Bold().FontColor(Theme.Grey);
                        c.Item().PaddingTop(2).Text(k.Value).FontSize(16).Bold().FontColor(Theme.Ink);
                        if (!string.IsNullOrEmpty(k.Sub)) c.Item().Text(k.Sub).FontSize(7.5f).FontColor(Theme.Grey);
                    });
                }
                for (int j = chunk.Count; j < perRow; j++) r.RelativeItem();
            });
        }
    }

    public static void Chart(ColumnDescriptor col, SvgChart chart, string? caption = null, float widthRatio = 1f, string? title = null)
    {
        float w = ContentWidth * widthRatio;
        float h = w * chart.Height / chart.Width + 2f;
        col.Item().ShowEntire().Column(c =>
        {
            c.Spacing(4);
            if (title != null) c.Item().PaddingTop(6).Text(title).FontSize(10.5f).Bold().FontColor(Theme.Primary);
            c.Item().AlignCenter().Width(w).Height(h).Svg(SvgImage.FromText(chart.Svg));
            if (caption != null)
                c.Item().Text(caption).FontSize(7.5f).FontColor(Theme.Grey);
        });
    }

    /// <summary>Tableau épuré : en-tête en petites capitales grises, filet noir, chiffres en police à chasse fixe.</summary>
    public static void Table(ColumnDescriptor col, string[] headers, IList<string[]> rows, float[] widths,
                             Func<int, int, string?>? cellColor = null, int highlightRow = -1, int leftCols = 1)
    {
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(c => { foreach (var w in widths) c.RelativeColumn(w); });
            t.Header(h =>
            {
                for (int j = 0; j < headers.Length; j++)
                {
                    var cell = h.Cell().BorderBottom(1.2f).BorderColor(Theme.Ink).PaddingVertical(5).PaddingHorizontal(5);
                    (j < leftCols ? cell.AlignLeft() : cell.AlignRight())
                        .Text(headers[j].ToUpperInvariant()).FontFamily(Theme.Mono).FontSize(6.6f).Bold().FontColor(Theme.Grey);
                }
            });
            for (int i = 0; i < rows.Count; i++)
            {
                string bg = i == highlightRow ? Theme.PrimarySoft : "#FFFFFF";
                for (int j = 0; j < headers.Length; j++)
                {
                    string value = j < rows[i].Length ? rows[i][j] : "";
                    var cell = t.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Theme.Rule).PaddingVertical(5).PaddingHorizontal(5);
                    var text = (j < leftCols ? cell.AlignLeft() : cell.AlignRight()).Text(value).FontSize(8.4f);
                    if (j >= leftCols || HasGreek(value)) text.FontFamily(Theme.Mono).FontSize(8f);
                    var color = cellColor?.Invoke(i, j);
                    if (color != null) text.FontColor(color).Bold();
                    else if (j == 0) text.SemiBold().FontColor(Theme.Ink);
                }
            }
        });
    }

    /// <summary>Encadré technique sombre, façon terminal.</summary>
    public static void TechBox(ColumnDescriptor col, string title, IEnumerable<(string Label, string Value)> lines)
    {
        col.Item().Background(Theme.Ink).Padding(16).Column(c =>
        {
            c.Spacing(4);
            c.Item().Text("$ " + title.ToLowerInvariant()).FontFamily(Theme.Mono).FontSize(8).Bold().FontColor("#5EEAD4");
            foreach (var (label, value) in lines)
                c.Item().Row(r =>
                {
                    r.ConstantItem(96).Text(label.ToLowerInvariant()).FontFamily(Theme.Mono).FontSize(8).FontColor("#C4B5FD");
                    r.RelativeItem().Text(value).FontSize(8.8f).FontColor("#E5E7EB");
                });
        });
    }
}
