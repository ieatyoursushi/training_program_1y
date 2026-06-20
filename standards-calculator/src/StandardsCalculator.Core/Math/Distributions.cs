namespace StandardsCalculator.Core.Math;

/// <summary>
/// Normal distribution helpers used throughout the calculator.
///
/// Φ(z)    — standard normal CDF, returns probability in [0,1].
/// Φ⁻¹(p)  — probit (inverse CDF), returns z-score for probability p ∈ (0,1).
///
/// METHOD:
///   CDF approximation: Abramowitz &amp; Stegun rational approximation (error &lt; 7.5e-8).
///   Inverse CDF: Rational approximation by Peter Acklam; max absolute error ~3e-9.
/// </summary>
public static class Distributions
{
    // --- Normal CDF (Φ) ---

    /// <summary>Standard normal CDF: P(Z ≤ z).</summary>
    public static double NormalCdf(double z)
    {
        // Abramowitz & Stegun §26.2.17
        const double a1 =  0.254829592;
        const double a2 = -0.284496736;
        const double a3 =  1.421413741;
        const double a4 = -1.453152027;
        const double a5 =  1.061405429;
        const double p  =  0.3275911;

        double sign = z < 0 ? -1 : 1;
        z = System.Math.Abs(z) / System.Math.Sqrt(2.0);
        double t = 1.0 / (1.0 + p * z);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t
                         * System.Math.Exp(-z * z);
        return 0.5 * (1.0 + sign * y);
    }

    /// <summary>Returns the percentile (0–100) for a given z-score.</summary>
    public static double ZToPercentile(double z) => NormalCdf(z) * 100.0;

    // --- Inverse CDF (Φ⁻¹ / probit) ---

    /// <summary>
    /// Probit function: z such that Φ(z) = p, for p ∈ (0,1).
    /// Acklam's rational approximation (2010).
    /// </summary>
    public static double NormalInverseCdf(double p)
    {
        if (p <= 0) return double.NegativeInfinity;
        if (p >= 1) return double.PositiveInfinity;

        // Coefficients from Acklam
        double[] a = [-3.969683028665376e+01,  2.209460984245205e+02,
                      -2.759285104469687e+02,   1.383577518672690e+02,
                      -3.066479806614716e+01,   2.506628277459239e+00];
        double[] b = [-5.447609879822406e+01,  1.615858368580409e+02,
                      -1.556989798598866e+02,   6.680131188771972e+01,
                      -1.328068155288572e+01];
        double[] c = [-7.784894002430293e-03, -3.223964580411365e-01,
                      -2.400758277161838e+00, -2.549732539343734e+00,
                       4.374664141464968e+00,  2.938163982698783e+00];
        double[] d = [ 7.784695709041462e-03,  3.224671290700398e-01,
                       2.445134137142996e+00,  3.754408661907416e+00];

        const double pLow  = 0.02425;
        const double pHigh = 1.0 - pLow;

        double x;
        if (p < pLow)
        {
            double q = System.Math.Sqrt(-2.0 * System.Math.Log(p));
            x = (((((c[0]*q+c[1])*q+c[2])*q+c[3])*q+c[4])*q+c[5]) /
                ((((d[0]*q+d[1])*q+d[2])*q+d[3])*q+1);
        }
        else if (p <= pHigh)
        {
            double q = p - 0.5;
            double r = q * q;
            x = (((((a[0]*r+a[1])*r+a[2])*r+a[3])*r+a[4])*r+a[5])*q /
                (((((b[0]*r+b[1])*r+b[2])*r+b[3])*r+b[4])*r+1);
        }
        else
        {
            double q = System.Math.Sqrt(-2.0 * System.Math.Log(1.0 - p));
            x = -(((((c[0]*q+c[1])*q+c[2])*q+c[3])*q+c[4])*q+c[5]) /
                  ((((d[0]*q+d[1])*q+d[2])*q+d[3])*q+1);
        }
        return x;
    }

    /// <summary>Converts a percentile (0–100) to a z-score.</summary>
    public static double PercentileToZ(double percentile) =>
        NormalInverseCdf(percentile / 100.0);
}
