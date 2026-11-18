namespace VolForecast.Models;

/// <summary>Algorithme du simplexe de Nelder-Mead (minimisation sans dérivées), écrit à la main.</summary>
public static class NelderMead
{
    public static (double[] X, double F, int Iterations) Minimize(Func<double[], double> f, double[] x0,
                                                                  double step = 0.6, int maxIter = 1000, double tol = 1e-10)
    {
        int n = x0.Length;
        var pts = new double[n + 1][];
        var vals = new double[n + 1];
        pts[0] = (double[])x0.Clone();
        for (int i = 0; i < n; i++)
        {
            pts[i + 1] = (double[])x0.Clone();
            pts[i + 1][i] += step;
        }
        for (int i = 0; i <= n; i++) vals[i] = f(pts[i]);

        int iter = 0;
        for (; iter < maxIter; iter++)
        {
            Array.Sort(vals, pts);
            if (Math.Abs(vals[n] - vals[0]) <= tol * (Math.Abs(vals[0]) + 1e-12)) break;

            var c = new double[n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++) c[j] += pts[i][j] / n;

            double[] Along(double t) => c.Select((cj, j) => cj + t * (pts[n][j] - cj)).ToArray();

            var xr = Along(-1);
            double fr = f(xr);
            if (fr < vals[0])
            {
                var xe = Along(-2);
                double fe = f(xe);
                if (fe < fr) { pts[n] = xe; vals[n] = fe; }
                else { pts[n] = xr; vals[n] = fr; }
            }
            else if (fr < vals[n - 1])
            {
                pts[n] = xr; vals[n] = fr;
            }
            else
            {
                var xc = fr < vals[n] ? Along(-0.5) : Along(0.5);
                double fc = f(xc);
                if (fc < Math.Min(fr, vals[n])) { pts[n] = xc; vals[n] = fc; }
                else
                {
                    for (int i = 1; i <= n; i++)
                    {
                        for (int j = 0; j < n; j++) pts[i][j] = pts[0][j] + 0.5 * (pts[i][j] - pts[0][j]);
                        vals[i] = f(pts[i]);
                    }
                }
            }
        }
        Array.Sort(vals, pts);
        return (pts[0], vals[0], iter);
    }
}
