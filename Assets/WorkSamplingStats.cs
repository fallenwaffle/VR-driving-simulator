using System;

/// <summary>
/// Statistics helpers for work sampling: proportions, 95% confidence intervals
/// (E = Z * sqrt(p(1-p)/N)), required sample size and two-proportion z-test.
/// </summary>
public static class WorkSamplingStats
{
    public const double Z95 = 1.96;

    public static double Proportion(int k, int n) { return n > 0 ? (double)k / n : 0.0; }

    /// <summary>Classic work-sampling error: E = Z * sqrt(p(1-p)/N).</summary>
    public static double HalfWidth(double p, int n)
    {
        return n > 0 ? Z95 * Math.Sqrt(p * (1.0 - p) / n) : 0.0;
    }

    /// <summary>Wilson score interval - better than the normal approximation for small N (e.g. 8 zones).</summary>
    public static void Wilson(int k, int n, out double lo, out double hi)
    {
        if (n <= 0) { lo = 0; hi = 0; return; }
        double p = (double)k / n;
        double z2 = Z95 * Z95;
        double denom = 1.0 + z2 / n;
        double centre = (p + z2 / (2.0 * n)) / denom;
        double margin = Z95 * Math.Sqrt(p * (1.0 - p) / n + z2 / (4.0 * n * n)) / denom;
        lo = Math.Max(0.0, centre - margin);
        hi = Math.Min(1.0, centre + margin);
    }

    /// <summary>N = Z^2 p(1-p) / E^2 (E as a fraction, e.g. 0.05).</summary>
    public static double RequiredSamples(double p, double e)
    {
        return e > 0 ? Z95 * Z95 * p * (1.0 - p) / (e * e) : double.NaN;
    }

    public static double TwoProportionPValue(int k1, int n1, int k2, int n2)
    {
        if (n1 <= 0 || n2 <= 0) return double.NaN;
        double p1 = (double)k1 / n1, p2 = (double)k2 / n2;
        double pp = (double)(k1 + k2) / (n1 + n2);
        double se = Math.Sqrt(pp * (1.0 - pp) * (1.0 / n1 + 1.0 / n2));
        if (se <= 0) return 1.0;
        double z = (p1 - p2) / se;
        return 2.0 * (1.0 - NormalCdf(Math.Abs(z)));
    }

    public static double NormalCdf(double x) { return 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0))); }

    // Abramowitz & Stegun 7.1.26
    static double Erf(double x)
    {
        double sign = x < 0 ? -1.0 : 1.0;
        x = Math.Abs(x);
        double t = 1.0 / (1.0 + 0.3275911 * x);
        double y = 1.0 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return sign * y;
    }
}
