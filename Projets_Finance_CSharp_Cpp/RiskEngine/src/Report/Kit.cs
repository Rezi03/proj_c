using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Quant.Report;

public sealed record Kpi(string Label, string Value, string? Sub = null, string? Color = null);

/// <summary>Briques de mise en page réutilisables pour des rapports au style « note de recherche ».</summary>
public static class Kit
{
    /// <summary>Largeur utile d'une page A4 avec des marges de 1,6 cm (en points).</summary>
    public const float ContentWidth = 504f;

    public static void Init()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    public static void StandardPage(PageDescriptor page, string reportTitle, string dateLabel, bool showHeader = true)
    {
        page.Size(PageSizes.A4);
        page.MarginHorizontal(1.6f, Unit.Centimetre);
        page.MarginVertical(1.2f, Unit.Centimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(Theme.Text).LineHeight(1.35f));

        if (showHeader)
            page.Header().PaddingBottom(12).Row(r =>
        {
            r.RelativeItem().Text(reportTitle.ToUpperInvariant()).FontSize(7.5f).Bold().FontColor(Theme.Navy);
            r.AutoItem().Text(dateLabel).FontSize(7.5f).FontColor(Theme.Grey);
        });

        page.Footer().PaddingTop(6).BorderTop(0.5f).BorderColor(Theme.Border).Row(r =>
        {
            r.RelativeItem().Text("Rezo Sabashvili  ·  Projet personnel  ·  C# .NET 10 et C++").FontSize(7.5f).FontColor(Theme.Grey);
            r.AutoItem().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Theme.Grey));
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    }

    /// <summary>Bandeau de couverture bleu marine.</summary>
    public static void Cover(ColumnDescriptor col, string kicker, string title, string subtitle, string dateLine)
    {
        col.Item().Background(Theme.Navy).PaddingHorizontal(26).PaddingVertical(26).Column(c =>
        {
            c.Item().Text(kicker.ToUpperInvariant()).FontSize(8).Bold().FontColor(Theme.Gold);
            c.Item().PaddingTop(8).Text(title).FontSize(25).Bold().FontColor(Colors.White).LineHeight(1.1f);
            c.Item().PaddingTop(8).Text(subtitle).FontSize(10.5f).FontColor("#C9D3E3");
            c.Item().PaddingTop(14).Width(60).LineHorizontal(2).LineColor(Theme.Gold);
            c.Item().PaddingTop(10).Text(dateLine).FontSize(9).FontColor("#C9D3E3");
        });
    }

    public static void Section(ColumnDescriptor col, string number, string title, string? lead = null)
    {
        col.Item().Row(r =>
        {
            r.ConstantItem(28).Text(number).FontSize(17).Bold().FontColor(Theme.Gold);
            r.RelativeItem().Text(title).FontSize(17).Bold().FontColor(Theme.Navy);
        });
        col.Item().LineHorizontal(1.2f).LineColor(Theme.Gold);
        if (lead != null) col.Item().Text(lead).FontColor(Theme.Grey).Italic();
    }

    public static void SubTitle(ColumnDescriptor col, string s)
        => col.Item().PaddingTop(4).Text(s).FontSize(11).Bold().FontColor(Theme.Navy);

    public static void Para(ColumnDescriptor col, string s) => col.Item().Text(s).Justify();

    public static void Bullets(ColumnDescriptor col, IEnumerable<string> items)
    {
        col.Item().Column(c =>
        {
            c.Spacing(3);
            foreach (var s in items)
                c.Item().Row(r =>
                {
                    r.ConstantItem(12).Text("•").Bold().FontColor(Theme.Gold);
                    r.RelativeItem().Text(s).Justify();
                });
        });
    }

    public static void Note(ColumnDescriptor col, string title, string body)
    {
        col.Item().Background(Theme.Cream).BorderLeft(3).BorderColor(Theme.Gold).Padding(10).Column(c =>
        {
            c.Item().Text(title).Bold().FontColor(Theme.Navy);
            c.Item().PaddingTop(2).Text(body).Justify();
        });
    }

    public static void Kpis(ColumnDescriptor col, IList<Kpi> items, int perRow = 3)
    {
        for (int i = 0; i < items.Count; i += perRow)
        {
            var chunk = items.Skip(i).Take(perRow).ToList();
            col.Item().Row(r =>
            {
                r.Spacing(8);
                foreach (var k in chunk)
                {
                    r.RelativeItem().Border(0.6f).BorderColor(Theme.Border).Background(Theme.Light)
                     .PaddingVertical(10).PaddingHorizontal(12).Column(c =>
                     {
                         c.Item().Text(k.Label.ToUpperInvariant()).FontSize(6.8f).Bold().FontColor(Theme.Grey);
                         c.Item().PaddingTop(3).Text(k.Value).FontSize(17).Bold().FontColor(k.Color ?? Theme.Navy);
                         if (!string.IsNullOrEmpty(k.Sub))
                             c.Item().Text(k.Sub).FontSize(7.5f).FontColor(Theme.Grey);
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
            if (title != null) c.Item().PaddingTop(4).Text(title).FontSize(11).Bold().FontColor(Theme.Navy);
            c.Item().AlignCenter().Width(w).Height(h).Svg(SvgImage.FromText(chart.Svg));
            if (caption != null)
                c.Item().PaddingTop(2).Text(caption).FontSize(7.5f).Italic().FontColor(Theme.Grey);
        });
    }

    /// <summary>Tableau sobre : en-tête bleu marine, lignes alternées, chiffres alignés à droite.</summary>
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
                    var cell = h.Cell().Background(Theme.Navy).PaddingVertical(5).PaddingHorizontal(6);
                    (j < leftCols ? cell.AlignLeft() : cell.AlignRight())
                        .Text(headers[j]).FontSize(7.8f).Bold().FontColor(Colors.White);
                }
            });
            for (int i = 0; i < rows.Count; i++)
            {
                string bg = i == highlightRow ? "#FBF3DC" : (i % 2 == 0 ? "#FFFFFF" : Theme.Light);
                for (int j = 0; j < headers.Length; j++)
                {
                    string value = j < rows[i].Length ? rows[i][j] : "";
                    var cell = t.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Theme.Border).PaddingVertical(4).PaddingHorizontal(6);
                    var text = (j < leftCols ? cell.AlignLeft() : cell.AlignRight()).Text(value).FontSize(8.4f);
                    var color = cellColor?.Invoke(i, j);
                    if (color != null) text.FontColor(color).Bold();
                    else if (j == 0) text.SemiBold();
                }
            }
        });
    }

    /// <summary>Encadré « technologies » affiché en couverture.</summary>
    public static void TechBox(ColumnDescriptor col, string title, IEnumerable<(string Label, string Value)> lines)
    {
        col.Item().Border(0.6f).BorderColor(Theme.Border).Padding(12).Column(c =>
        {
            c.Spacing(3);
            c.Item().Text(title.ToUpperInvariant()).FontSize(7).Bold().FontColor(Theme.Gold);
            foreach (var (label, value) in lines)
                c.Item().Row(r =>
                {
                    r.ConstantItem(110).Text(label).SemiBold().FontColor(Theme.Navy);
                    r.RelativeItem().Text(value);
                });
        });
    }
}
