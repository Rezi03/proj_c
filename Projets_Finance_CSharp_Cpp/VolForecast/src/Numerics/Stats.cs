namespace Quant.Numerics;

/// <summary>Statistiques et algèbre linéaire, écrites à la main (aucune dépendance externe).</summary>
public static class Stats
{
    public static double Mean(IReadOnlyList<double> x)
    {
        double s = 0;
        for (int i = 0; i < x.Count; i++) s += x[i];
        return s / x.Count;
    }

    public static double Variance(IReadOnlyList<double> x)
    {
        double m = Mean(x), s = 0;
        for (int i = 0; i < x.Count; i++) { double d = x[i] - m; s += d * d; }
        return s / (x.Count - 1);
    }

    public static double Std(IReadOnlyList<double> x) => Math.Sqrt(Variance(x));

    public static double Covariance(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        double mx = Mean(x), my = Mean(y), s = 0;
        for (int i = 0; i < x.Count; i++) s += (x[i] - mx) * (y[i] - my);
        return s / (x.Count - 1);
    }

    public static double Quantile(IReadOnlyList<double> x, double p)
    {
        var a = x.ToArray();
        Array.Sort(a);
        return QuantileSorted(a, p);
    }

    public static double QuantileSorted(double[] a, double p)
    {
        if (a.Length == 0) return double.NaN;
        double h = (a.Length - 1) * p;
        int lo = (int)Math.Floor(h);
        int hi = Math.Min(lo + 1, a.Length - 1);
        return a[lo] + (h - lo) * (a[hi] - a[lo]);
    }

    /// <summary>VaR historique, renvoyée comme une perte positive.</summary>
    public static double VarLoss(IReadOnlyList<double> pnl, double confidence) => -Quantile(pnl, 1 - confidence);

    /// <summary>Expected Shortfall : moyenne des pires (1 - confidence) % des P&L, en perte positive.</summary>
    public static double EsLoss(IReadOnlyList<double> pnl, double confidence)
    {
        var a = pnl.ToArray();
        Array.Sort(a);
        int k = Math.Max(1, (int)Math.Floor(a.Length * (1 - confidence)));
        double s = 0;
        for (int i = 0; i < k; i++) s += a[i];
        return -s / k;
    }

    /// <summary>Matrice de covariance sur une fenêtre [start, start+len) de séries [actif][t].</summary>
    public static double[,] CovMatrix(double[][] series, int start, int len)
    {
        int n = series.Length;
        var means = new double[n];
        for (int i = 0; i < n; i++)
        {
            double s = 0;
            for (int t = start; t < start + len; t++) s += series[i][t];
            means[i] = s / len;
        }
        var cov = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = i; j < n; j++)
            {
                double s = 0;
                for (int t = start; t < start + len; t++) s += (series[i][t] - means[i]) * (series[j][t] - means[j]);
                cov[i, j] = cov[j, i] = s / (len - 1);
            }
        return cov;
    }

    /// <summary>Covariance EWMA (RiskMetrics) à moyenne nulle, sur la fenêtre donnée.</summary>
    public static double[,] EwmaCov(double[][] series, int start, int len, double lambda)
    {
        int n = series.Length;
        int seed = Math.Min(60, len);
        var cov = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                double s = 0;
                for (int t = start; t < start + seed; t++) s += series[i][t] * series[j][t];
                cov[i, j] = s / seed;
            }
        for (int t = start + seed; t < start + len; t++)
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    cov[i, j] = lambda * cov[i, j] + (1 - lambda) * series[i][t] * series[j][t];
        return cov;
    }

    public static double[,] Correlation(double[,] cov)
    {
        int n = cov.GetLength(0);
        var c = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                c[i, j] = cov[i, j] / Math.Sqrt(cov[i, i] * cov[j, j]);
        return c;
    }

    public static double[] MatVec(double[,] m, double[] v)
    {
        int n = v.Length;
        var r = new double[n];
        for (int i = 0; i < n; i++)
        {
            double s = 0;
            for (int j = 0; j < n; j++) s += m[i, j] * v[j];
            r[i] = s;
        }
        return r;
    }

    public static double Dot(double[] a, double[] b)
    {
        double s = 0;
        for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
        return s;
    }

    public static double[,]? Cholesky(double[,] a)
    {
        int n = a.GetLength(0);
        var l = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j <= i; j++)
            {
                double s = a[i, j];
                for (int k = 0; k < j; k++) s -= l[i, k] * l[j, k];
                if (i == j)
                {
                    if (s <= 0) return null;
                    l[i, i] = Math.Sqrt(s);
                }
                else l[i, j] = s / l[j, j];
            }
        return l;
    }

    // ---- Lois de probabilité ----

    public static double NormPdf(double x) => Math.Exp(-0.5 * x * x) / Math.Sqrt(2 * Math.PI);

    public static double NormCdf(double x) => 0.5 * Erfc(-x / Math.Sqrt(2));

    private static double Erfc(double x)
    {
        // Numerical Recipes, précision relative 1,2e-7
        double z = Math.Abs(x);
        double t = 1.0 / (1.0 + 0.5 * z);
        double r = t * Math.Exp(-z * z - 1.26551223 + t * (1.00002368 + t * (0.37409196 + t * (0.09678418 +
                   t * (-0.18628806 + t * (0.27886807 + t * (-1.13520398 + t * (1.48851587 +
                   t * (-0.82215223 + t * 0.17087277)))))))));
        return x >= 0 ? r : 2.0 - r;
    }

    /// <summary>Inverse de la fonction de répartition normale (algorithme d'Acklam).</summary>
    public static double NormInv(double p)
    {
        if (p <= 0) return double.NegativeInfinity;
        if (p >= 1) return double.PositiveInfinity;
        double[] a = { -3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02, 1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00 };
        double[] b = { -5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02, 6.680131188771972e+01, -1.328068155288572e+01 };
        double[] c = { -7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00, -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00 };
        double[] d = { 7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00, 3.754408661907416e+00 };
        const double plow = 0.02425, phigh = 1 - plow;
        double q, r;
        if (p < plow)
        {
            q = Math.Sqrt(-2 * Math.Log(p));
            return (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }
        if (p > phigh)
        {
            q = Math.Sqrt(-2 * Math.Log(1 - p));
            return -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }
        q = p - 0.5;
        r = q * q;
        return (((((a[0] * r + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * q / (((((b[0] * r + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1);
    }

    /// <summary>P-valeur d'un khi-deux à 1 degré de liberté.</summary>
    public static double Chi2PValue1(double x) => x <= 0 ? 1.0 : 2 * (1 - NormCdf(Math.Sqrt(x)));

    /// <summary>n * ln(p), avec la convention 0 * ln(0) = 0.</summary>
    public static double XLogY(double n, double p) => n == 0 ? 0 : n * Math.Log(p);

    /// <summary>Résout A x = b par élimination de Gauss avec pivot partiel.</summary>
    public static double[] Solve(double[,] a, double[] b)
    {
        int n = b.Length;
        var m = (double[,])a.Clone();
        var x = (double[])b.Clone();
        for (int k = 0; k < n; k++)
        {
            int piv = k;
            for (int i = k + 1; i < n; i++) if (Math.Abs(m[i, k]) > Math.Abs(m[piv, k])) piv = i;
            if (piv != k)
            {
                for (int j = 0; j < n; j++) (m[k, j], m[piv, j]) = (m[piv, j], m[k, j]);
                (x[k], x[piv]) = (x[piv], x[k]);
            }
            for (int i = k + 1; i < n; i++)
            {
                double f = m[i, k] / m[k, k];
                for (int j = k; j < n; j++) m[i, j] -= f * m[k, j];
                x[i] -= f * x[k];
            }
        }
        for (int i = n - 1; i >= 0; i--)
        {
            double s = x[i];
            for (int j = i + 1; j < n; j++) s -= m[i, j] * x[j];
            x[i] = s / m[i, i];
        }
        return x;
    }

    /// <summary>Moindres carrés ordinaires. Chaque ligne de X contient déjà la constante.</summary>
    public static double[] Ols(IReadOnlyList<double[]> x, IReadOnlyList<double> y)
    {
        int k = x[0].Length;
        var xtx = new double[k, k];
        var xty = new double[k];
        for (int t = 0; t < x.Count; t++)
        {
            var row = x[t];
            for (int i = 0; i < k; i++)
            {
                xty[i] += row[i] * y[t];
                for (int j = 0; j < k; j++) xtx[i, j] += row[i] * row[j];
            }
        }
        return Solve(xtx, xty);
    }
}
