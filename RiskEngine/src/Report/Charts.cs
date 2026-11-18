using System.Text;
using Quant.Numerics;

namespace Quant.Report;

/// <summary>Un graphique vectoriel prêt à être inséré dans le PDF.</summary>
public sealed record SvgChart(string Svg, int Width, int Height);

public sealed record LineSeries(string Name, IReadOnlyList<double> Y, string Color, double Width = 1.6, bool Dashed = false, double Opacity = 1.0);

public sealed record Marker(int Index, double Y);

public sealed record VLine(double X, string Label, string Color);

public sealed record BarGroup(string Name, IReadOnlyList<double> Values, string Color);

public sealed class ChartOptions
{
    public int Width { get; init; } = 760;
    public int Height { get; init; } = 300;
    public Func<double, string> YFormat { get; init; } = v => Fmt.Num(v, 0);
    public bool Legend { get; init; } = true;
    public double? YMin { get; init; }
    public double? YMax { get; init; }
}

/// <summary>
/// Petite bibliothèque de graphiques SVG écrite à la main (courbes, histogrammes, barres, heatmap).
/// Le SVG est vectoriel : les graphiques restent nets à n'importe quel zoom dans le PDF.
/// </summary>
public static class Charts
{
    // Couleurs et police viennent du thème du projet (chaque rapport a sa propre identité visuelle)
    private const string Font = Theme.ChartFont;
    private const string GridColor = Theme.ChartGrid;
    private const string AxisColor = Theme.ChartAxis;
    private const string LabelColor = Theme.ChartLabel;

    private static string S(double v) => Fmt.Svg(v);
    private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static StringBuilder Begin(int w, int h)
    {
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{w}\" height=\"{h}\" viewBox=\"0 0 {w} {h}\">");
        sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"#FFFFFF\"/>");
        return sb;
    }

    private static SvgChart End(StringBuilder sb, int w, int h)
    {
        sb.Append("</svg>");
        return new SvgChart(sb.ToString(), w, h);
    }

    private static void Text(StringBuilder sb, double x, double y, string s, double size = 11, string fill = LabelColor,
                             string anchor = "start", string weight = "normal")
        => sb.Append($"<text x=\"{S(x)}\" y=\"{S(y)}\" font-family=\"{Font}\" font-size=\"{S(size)}\" fill=\"{fill}\" " +
                     $"text-anchor=\"{anchor}\" font-weight=\"{weight}\">{Esc(s)}</text>");

    private static void Line(StringBuilder sb, double x1, double y1, double x2, double y2, string stroke, double width = 1,
                             bool dashed = false, double opacity = 1)
        => sb.Append($"<path d=\"M{S(x1)} {S(y1)} L{S(x2)} {S(y2)}\" fill=\"none\" stroke=\"{stroke}\" " +
                     $"stroke-width=\"{S(width)}\" stroke-opacity=\"{S(opacity)}\" stroke-linecap=\"butt\"" + (dashed ? " stroke-dasharray=\"5 4\"" : "") + "/>");

    private static void Rect(StringBuilder sb, double x, double y, double w, double h, string fill, double opacity = 1)
        => sb.Append($"<rect x=\"{S(x)}\" y=\"{S(y)}\" width=\"{S(Math.Max(0, w))}\" height=\"{S(Math.Max(0, h))}\" fill=\"{fill}\" fill-opacity=\"{S(opacity)}\"/>");

    private static void Circle(StringBuilder sb, double x, double y, double r, string fill)
        => sb.Append($"<circle cx=\"{S(x)}\" cy=\"{S(y)}\" r=\"{S(r)}\" fill=\"{fill}\" stroke=\"#FFFFFF\" stroke-width=\"1\"/>");

    public static double[] NiceTicks(double min, double max, int target = 5)
    {
        if (!(max > min)) max = min + 1;
        double raw = (max - min) / target;
        double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double norm = raw / mag;
        double step = (norm < 1.5 ? 1 : norm < 3 ? 2 : norm < 7 ? 5 : 10) * mag;
        double start = Math.Ceiling(min / step) * step;
        var ticks = new List<double>();
        for (double v = start; v <= max + step * 1e-9; v += step)
            ticks.Add(Math.Abs(v) < step * 1e-9 ? 0 : v);
        return ticks.ToArray();
    }

    private static void LegendRow(StringBuilder sb, double x, double y, IEnumerable<(string Name, string Color, bool Dashed, bool Square)> items)
    {
        foreach (var (name, color, dashed, square) in items)
        {
            if (square) Rect(sb, x, y - 8, 10, 10, color);
            else Line(sb, x, y - 3, x + 18, y - 3, color, 2.4, dashed);
            Text(sb, x + (square ? 15 : 24), y + 1, name, 11, Theme.ChartText);
            x += (square ? 15 : 24) + name.Length * 6.1 + 22;
        }
    }

    /// <summary>Courbes datées (une ou plusieurs séries), avec points de marquage optionnels.</summary>
    public static SvgChart Lines(IReadOnlyList<DateTime> x, IList<LineSeries> series, ChartOptions o,
                                 IList<Marker>? markers = null, string markerColor = Theme.ChartNegative, string? markerLabel = null)
    {
        int w = o.Width, h = o.Height;
        double ml = 74, mr = 16, mt = o.Legend ? 32 : 12, mb = 30;
        double pw = w - ml - mr, ph = h - mt - mb;
        int n = x.Count;

        double ymin = double.MaxValue, ymax = double.MinValue;
        foreach (var s in series)
            foreach (var v in s.Y)
                if (double.IsFinite(v)) { ymin = Math.Min(ymin, v); ymax = Math.Max(ymax, v); }
        if (markers != null)
            foreach (var m in markers)
                if (double.IsFinite(m.Y)) { ymin = Math.Min(ymin, m.Y); ymax = Math.Max(ymax, m.Y); }
        if (ymin == double.MaxValue) { ymin = 0; ymax = 1; }
        double pad = (ymax - ymin) * 0.06;
        if (pad <= 0) pad = Math.Abs(ymax) * 0.1 + 1e-9;
        ymin = o.YMin ?? ymin - pad;
        ymax = o.YMax ?? ymax + pad;

        double X(int i) => ml + (n <= 1 ? 0 : pw * i / (n - 1.0));
        double Y(double v) => mt + ph * (1 - (v - ymin) / (ymax - ymin));

        var sb = Begin(w, h);
        foreach (var t in NiceTicks(ymin, ymax))
        {
            Line(sb, ml, Y(t), w - mr, Y(t), GridColor);
            Text(sb, ml - 8, Y(t) + 4, o.YFormat(t), 10.5, LabelColor, "end");
        }
        if (ymin < 0 && ymax > 0) Line(sb, ml, Y(0), w - mr, Y(0), AxisColor, 1);
        Line(sb, ml, mt + ph, w - mr, mt + ph, AxisColor);

        int nt = Math.Min(6, n);
        for (int k = 0; k < nt; k++)
        {
            int i = (int)Math.Round((n - 1) * k / (double)Math.Max(1, nt - 1));
            Line(sb, X(i), mt + ph, X(i), mt + ph + 4, AxisColor);
            string anchor = k == 0 ? "start" : k == nt - 1 ? "end" : "middle";
            Text(sb, X(i), h - mb + 19, Fmt.MonthYear(x[i]), 10.5, LabelColor, anchor);
        }

        foreach (var s in series)
        {
            var path = new StringBuilder();
            bool pen = false;
            for (int i = 0; i < Math.Min(n, s.Y.Count); i++)
            {
                double v = s.Y[i];
                if (!double.IsFinite(v)) { pen = false; continue; }
                path.Append(pen ? " L" : " M").Append(S(X(i))).Append(' ').Append(S(Y(v)));
                pen = true;
            }
            sb.Append($"<path d=\"{path}\" fill=\"none\" stroke=\"{s.Color}\" stroke-width=\"{S(s.Width)}\" " +
                      $"stroke-opacity=\"{S(s.Opacity)}\" stroke-linejoin=\"round\"" + (s.Dashed ? " stroke-dasharray=\"6 4\"" : "") + "/>");
        }

        if (markers != null)
            foreach (var m in markers)
                if (double.IsFinite(m.Y)) Circle(sb, X(m.Index), Y(m.Y), 3.6, markerColor);

        if (o.Legend)
        {
            var items = series.Where(s => !string.IsNullOrEmpty(s.Name))
                              .Select(s => (s.Name, s.Color, s.Dashed, false)).ToList();
            if (markers != null && markerLabel != null) items.Add((markerLabel, markerColor, false, true));
            LegendRow(sb, ml, 16, items);
        }
        return End(sb, w, h);
    }

    /// <summary>Histogramme d'une distribution de P&L, avec la queue de perte colorée et des lignes verticales (VaR, ES).</summary>
    public static SvgChart Histogram(IReadOnlyList<double> values, int bins, ChartOptions o, IList<VLine> vlines,
                                     Func<double, string> xFormat, double tailBelow = double.NegativeInfinity,
                                     string color = Theme.ChartMain, string tailColor = Theme.ChartNegative)
    {
        int w = o.Width, h = o.Height;
        double ml = 52, mr = 16, mt = 34, mb = 30;
        double pw = w - ml - mr, ph = h - mt - mb;

        var sorted = values.ToArray();
        Array.Sort(sorted);
        double lo = Stats.QuantileSorted(sorted, 0.001), hi = Stats.QuantileSorted(sorted, 0.999);
        foreach (var vl in vlines) { lo = Math.Min(lo, vl.X); hi = Math.Max(hi, vl.X); }
        double span = hi - lo;
        lo -= span * 0.03; hi += span * 0.03;
        double bw = (hi - lo) / bins;
        var counts = new int[bins];
        foreach (var v in sorted)
        {
            if (v < lo || v > hi) continue;
            int b = Math.Min(bins - 1, (int)((v - lo) / bw));
            counts[b]++;
        }
        int cmax = Math.Max(1, counts.Max());
        double total = sorted.Length;
        double X(double v) => ml + pw * (v - lo) / (hi - lo);

        var sb = Begin(w, h);
        double fmax = cmax / total;
        foreach (var t in NiceTicks(0, fmax, 4))
        {
            double yy = mt + ph * (1 - t / fmax);
            Line(sb, ml, yy, w - mr, yy, GridColor);
            Text(sb, ml - 8, yy + 4, Fmt.Num(t * 100, t * 100 < 1 ? 1 : 0) + " %", 10.5, LabelColor, "end");
        }
        for (int b = 0; b < bins; b++)
        {
            double x0 = lo + b * bw;
            double bh = ph * counts[b] / cmax;
            bool tail = x0 + bw / 2 < tailBelow;
            Rect(sb, X(x0) + 0.4, mt + ph - bh, pw / bins - 0.8, bh, tail ? tailColor : color, tail ? 0.9 : 0.75);
        }
        Line(sb, ml, mt + ph, w - mr, mt + ph, AxisColor);
        foreach (var t in NiceTicks(lo, hi, 6))
        {
            Line(sb, X(t), mt + ph, X(t), mt + ph + 4, AxisColor);
            Text(sb, X(t), h - mb + 19, xFormat(t), 10.5, LabelColor, "middle");
        }
        var ordered = vlines.OrderBy(v => v.X).ToList();
        for (int idx = 0; idx < ordered.Count; idx++)
        {
            var vl = ordered[idx];
            double xx = X(vl.X);
            Line(sb, xx, mt - 6, xx, mt + ph, vl.Color, 1.8, dashed: true);
            // La ligne la plus à gauche porte son étiquette à gauche, les suivantes à droite : jamais de chevauchement
            double textWidth = vl.Label.Length * 6.3;
            bool left = idx == 0 && ordered.Count > 1 && xx - textWidth - 8 > ml;
            Text(sb, left ? xx - 6 : xx + 6, mt + 4 + idx * 15, vl.Label, 11, vl.Color, left ? "end" : "start", "bold");
        }
        return End(sb, w, h);
    }

    /// <summary>Barres horizontales groupées (valeurs positives ou négatives).</summary>
    public static SvgChart HBars(IList<string> labels, IList<BarGroup> groups, int width, Func<double, string> fmt,
                                 Func<double, string>? colorByValue = null, double labelWidth = 150)
    {
        int nG = groups.Count, n = labels.Count;
        double barH = nG == 1 ? 16 : 11, gap = 10;
        double rowH = barH * nG + gap;
        double mt = nG > 1 ? 30 : 10, mb = 26;
        int h = (int)(mt + rowH * n + mb);
        double ml = labelWidth, mr = 74;
        double pw = width - ml - mr;

        double vmin = 0, vmax = 0;
        foreach (var g in groups) foreach (var v in g.Values) { vmin = Math.Min(vmin, v); vmax = Math.Max(vmax, v); }
        double span = vmax - vmin + 1e-12;
        if (vmin < 0) vmin -= span * 0.18;   // place pour l'étiquette des barres négatives
        if (vmax > 0) vmax += span * 0.05;
        double X(double v) => ml + pw * (v - vmin) / (vmax - vmin);

        var sb = Begin(width, h);
        foreach (var t in NiceTicks(vmin, vmax, 5))
        {
            Line(sb, X(t), mt - 4, X(t), mt + rowH * n, GridColor);
            Text(sb, X(t), h - 8, fmt(t), 10.5, LabelColor, "middle");
        }
        for (int i = 0; i < n; i++)
        {
            double y0 = mt + i * rowH + gap / 2;
            Text(sb, ml - 10, y0 + barH * nG / 2 + 4, labels[i], 11.5, Theme.ChartText, "end");
            for (int g = 0; g < nG; g++)
            {
                double v = groups[g].Values[i];
                double xa = X(Math.Min(0, v)), xb = X(Math.Max(0, v));
                string c = colorByValue?.Invoke(v) ?? groups[g].Color;
                Rect(sb, xa, y0 + g * barH, xb - xa, barH - 2, c, 0.9);
                bool neg = v < 0;
                Text(sb, neg ? xa - 5 : xb + 5, y0 + g * barH + barH - 5, fmt(v), 10.5, Theme.ChartText, neg ? "end" : "start");
            }
        }
        Line(sb, X(0), mt - 4, X(0), mt + rowH * n, AxisColor, 1.2);
        if (nG > 1) LegendRow(sb, ml, 16, groups.Select(g => (g.Name, g.Color, false, true)));
        return End(sb, width, h);
    }

    /// <summary>Heatmap d'une matrice de corrélation.</summary>
    public static SvgChart Heatmap(IList<string> labels, double[,] m, int cell = 52)
    {
        int n = labels.Count;
        double ml = 70, mt = 30;
        int w = (int)(ml + n * cell + 12), h = (int)(mt + n * cell + 10);
        var sb = Begin(w, h);
        for (int j = 0; j < n; j++) Text(sb, ml + j * cell + cell / 2.0, mt - 10, labels[j], 11, Theme.ChartText, "middle", "bold");
        for (int i = 0; i < n; i++)
        {
            Text(sb, ml - 10, mt + i * cell + cell / 2.0 + 4, labels[i], 11, Theme.ChartText, "end", "bold");
            for (int j = 0; j < n; j++)
            {
                double v = m[i, j];
                string fill = v >= 0 ? Mix("#FFFFFF", Theme.HeatPositive, Math.Min(1, v)) : Mix("#FFFFFF", Theme.ChartNegative, Math.Min(1, -v));
                Rect(sb, ml + j * cell + 1, mt + i * cell + 1, cell - 2, cell - 2, fill);
                string tc = Math.Abs(v) > 0.55 ? "#FFFFFF" : Theme.ChartText;
                Text(sb, ml + j * cell + cell / 2.0, mt + i * cell + cell / 2.0 + 4, Fmt.Num(v, 2), 11, tc, "middle");
            }
        }
        return End(sb, w, h);
    }

    private static string Mix(string a, string b, double t)
    {
        int Hex(string s, int i) => Convert.ToInt32(s.Substring(i, 2), 16);
        int r = (int)Math.Round(Hex(a, 1) + (Hex(b, 1) - Hex(a, 1)) * t);
        int g = (int)Math.Round(Hex(a, 3) + (Hex(b, 3) - Hex(a, 3)) * t);
        int bl = (int)Math.Round(Hex(a, 5) + (Hex(b, 5) - Hex(a, 5)) * t);
        return $"#{r:X2}{g:X2}{bl:X2}";
    }

    /// <summary>Courbe sur un axe x numérique (ex. horizon de prévision, choc de rendement).</summary>
    public static SvgChart XY(IReadOnlyList<double> x, IList<LineSeries> series, ChartOptions o, Func<double, string> xFormat)
    {
        int w = o.Width, h = o.Height;
        double ml = 74, mr = 16, mt = o.Legend ? 32 : 12, mb = 30;
        double pw = w - ml - mr, ph = h - mt - mb;
        double xmin = x.Min(), xmax = x.Max();
        double ymin = double.MaxValue, ymax = double.MinValue;
        foreach (var s in series) foreach (var v in s.Y) if (double.IsFinite(v)) { ymin = Math.Min(ymin, v); ymax = Math.Max(ymax, v); }
        double pad = (ymax - ymin) * 0.08 + 1e-12;
        ymin = o.YMin ?? ymin - pad;
        ymax = o.YMax ?? ymax + pad;
        double X(double v) => ml + pw * (v - xmin) / (xmax - xmin);
        double Y(double v) => mt + ph * (1 - (v - ymin) / (ymax - ymin));

        var sb = Begin(w, h);
        foreach (var t in NiceTicks(ymin, ymax))
        {
            Line(sb, ml, Y(t), w - mr, Y(t), GridColor);
            Text(sb, ml - 8, Y(t) + 4, o.YFormat(t), 10.5, LabelColor, "end");
        }
        foreach (var t in NiceTicks(xmin, xmax, 7))
        {
            Line(sb, X(t), mt + ph, X(t), mt + ph + 4, AxisColor);
            Text(sb, X(t), h - mb + 19, xFormat(t), 10.5, LabelColor, "middle");
        }
        Line(sb, ml, mt + ph, w - mr, mt + ph, AxisColor);
        if (xmin < 0 && xmax > 0) Line(sb, X(0), mt, X(0), mt + ph, AxisColor, 1, dashed: true);
        foreach (var s in series)
        {
            var path = new StringBuilder();
            for (int i = 0; i < x.Count; i++)
                path.Append(i == 0 ? "M" : " L").Append(S(X(x[i]))).Append(' ').Append(S(Y(s.Y[i])));
            sb.Append($"<path d=\"{path}\" fill=\"none\" stroke=\"{s.Color}\" stroke-width=\"{S(s.Width)}\" stroke-linejoin=\"round\"" +
                      (s.Dashed ? " stroke-dasharray=\"6 4\"" : "") + "/>");
        }
        if (o.Legend) LegendRow(sb, ml, 16, series.Select(s => (s.Name, s.Color, s.Dashed, false)));
        return End(sb, w, h);
    }
}
