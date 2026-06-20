using StandardsCalculator.Core.Models;

namespace StandardsCalculator.Core.Math;

/// <summary>
/// Box-Cox Cole-Green (LMS) model for strength percentile estimation.
///
/// PURPOSE:
///   The PCHIP spline in PercentileModel is C¹ and passes exactly through the 5 published
///   anchor percentiles, but it has no global distributional interpretation.  The LMS model
///   fits a parametric Box-Cox Cole-Green (BCCG) distribution (Cole &amp; Green, 1992) to the same 5
///   anchors, capturing skewness (L), median (M), and spread (S) explicitly.
///
///   This is a research/diagnostic complement, not a replacement for the production spline.
///   Switching between them reveals how much skewness the simpler model misses and whether
///   the underlying strength distribution is approximately lognormal (L≈0) or power-transformed.
///
/// MODEL:
///   For a lifter at a given bodyweight, let y be the 1RM.  The BCCG z-score is:
///
///     z = { ((y/M)^L - 1) / (L·S)    if L ≠ 0
///           ln(y/M) / S               if L = 0
///
///   percentile = Φ(z) × 100
///
///   Inversion (percentile → lift):
///     y = M · (1 + L·S·z)^(1/L)    [for L ≠ 0, requires 1 + L·S·z > 0]
///     y = M · exp(S·z)              [for L = 0]
///
///   M is the Intermediate boundary (exactly the 50th percentile, since z=0 iff y=M).
///
/// FITTING (per bodyweight row):
///   With M fixed, fit L and S to minimise the variance of { f_i(L) / z_i }_i
///   where f_i(L) = ((y_i/M)^L - 1) / L  (= ln(y_i/M) as L→0).
///   The optimal S given L is the mean of { f_i(L) / z_i }.
///   L is found by golden-section search over [-2, 2].
///
/// 2D SURFACE (across bodyweight rows):
///   After fitting per-row (L_k, M_k, S_k), the surface interpolates:
///   • M(bw): monotone cubic (PCHIP) — strength increases with BW.
///   • L(bw), S(bw): linear interpolation (they vary slowly and need not be monotone).
///
/// GOODNESS-OF-FIT:
///   Root-mean-squared error of BCCG z-scores at the 5 anchors vs their theoretical values.
///   Smaller = better (perfect fit gives RMSE = 0, up to numerical precision).
///
/// Reference: Cole, T.J. &amp; Green, P.J. (1992). Smoothing reference centile curves: The LMS
///   method and penalized likelihood. Statistics in Medicine, 11(10), 1305–1319.
/// </summary>
public class LmsModel
{
    // Fixed anchor z-scores — same as PercentileModel uses
    private static readonly double[] AnchorZ =
        new[] { 5.0, 20.0, 50.0, 80.0, 95.0 }
            .Select(p => Distributions.NormalInverseCdf(p / 100.0))
            .ToArray();

    // Non-median anchor indices (for fitting)
    private static readonly int[] FitIndices = [0, 1, 3, 4];

    // ── Per-row fit result ────────────────────────────────────────────────────

    public sealed record LmsRowFit(
        double Bodyweight,
        double L,           // Box-Cox power (0 = lognormal, <0 = left-skewed, >0 = right-skewed)
        double M,           // Median (Intermediate boundary)
        double S,           // Coefficient of variation
        double FitRmse      // RMSE of z-score residuals at the 5 anchors
    );

    // ── Full table surface ────────────────────────────────────────────────────

    public sealed class LmsTableFit
    {
        private readonly LmsRowFit[] _rows;
        private readonly MonotoneSpline _mSpline;   // M(bw): strength vs BW — monotone
        // L and S interpolated linearly (they vary slowly, not necessarily monotone)

        public LmsRowFit[] RowFits => _rows;
        public double OverallRmse  { get; }

        internal LmsTableFit(LmsRowFit[] rows)
        {
            _rows = rows;
            OverallRmse = System.Math.Sqrt(rows.Average(r => r.FitRmse * r.FitRmse));

            // Build M spline (monotone — higher BW → higher strength)
            double[] bwVals = rows.Select(r => r.Bodyweight).ToArray();
            double[] mVals  = rows.Select(r => r.M).ToArray();
            _mSpline = new MonotoneSpline(bwVals, mVals);
        }

        /// <summary>
        /// Interpolate (L, M, S) at a given bodyweight.
        /// </summary>
        public (double L, double M, double S) Evaluate(double bodyweight)
        {
            double m = _mSpline.Evaluate(bodyweight);

            // Linear interpolation for L and S
            double l = LinearInterp(_rows, bodyweight, r => r.L);
            double s = LinearInterp(_rows, bodyweight, r => r.S);

            return (l, m, s);
        }

        /// <summary>
        /// Map a lift to a percentile using the LMS model at the given bodyweight.
        /// </summary>
        public double LiftToPercentile(double lift, double bodyweight)
        {
            var (l, m, s) = Evaluate(bodyweight);
            double z = BccgZ(lift, l, m, s);
            return System.Math.Clamp(Distributions.NormalCdf(z) * 100.0, 0.0, 100.0);
        }

        /// <summary>
        /// Map a percentile to a lift using the LMS model at the given bodyweight.
        /// </summary>
        public double PercentileToLift(double percentile, double bodyweight)
        {
            var (l, m, s) = Evaluate(bodyweight);
            double z = Distributions.NormalInverseCdf(percentile / 100.0);
            return BccgInverse(z, l, m, s);
        }

        // ── Static helpers ────────────────────────────────────────────────────

        private static double LinearInterp(LmsRowFit[] rows, double bw, Func<LmsRowFit, double> selector)
        {
            if (bw <= rows[0].Bodyweight)     return selector(rows[0]);
            if (bw >= rows[^1].Bodyweight)    return selector(rows[^1]);
            int i = System.Array.FindLastIndex(rows, r => r.Bodyweight <= bw);
            var lo = rows[i]; var hi = rows[i + 1];
            double t = (bw - lo.Bodyweight) / (hi.Bodyweight - lo.Bodyweight);
            return selector(lo) + t * (selector(hi) - selector(lo));
        }
    }

    // ── Fitting ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fit the LMS model to a standards table.
    /// </summary>
    /// <param name="table">The standards table (by-bodyweight rows required).</param>
    /// <returns>A fitted surface, or null if the table has fewer than 2 bodyweight rows.</returns>
    public static LmsTableFit? Fit(StandardsTable table)
    {
        var rows = table.ByBodyweight;
        if (rows.Count < 2) return null;

        var fits = rows.Select(row => FitRow(row)).ToArray();
        return new LmsTableFit(fits);
    }

    /// <summary>
    /// Fit LMS to a single boundary row.
    /// </summary>
    public static LmsRowFit FitRow(StandardsRow row)
    {
        double[] boundaries = [row.Beginner, row.Novice, row.Intermediate, row.Advanced, row.Elite];
        double m = row.Intermediate;  // fixed: median = 50th percentile boundary

        // Golden-section search for L that minimises variance of {f_i(L)/z_i}
        (double variance, double sEstimate) Objective(double l)
        {
            double[] vals = FitIndices.Select(i => F(boundaries[i], m, l) / AnchorZ[i]).ToArray();
            double mean  = vals.Average();
            double v     = vals.Average(x => (x - mean) * (x - mean));
            return (v, mean);
        }

        double bestL = GoldenSection(l => Objective(l).variance, -2.0, 2.0, tol: 1e-7);
        var (_, bestS) = Objective(bestL);

        // Compute RMSE at the 5 anchors
        double rmse = System.Math.Sqrt(
            boundaries.Select((b, i) =>
            {
                double zFit = BccgZ(b, bestL, m, bestS);
                double diff = zFit - AnchorZ[i];
                return diff * diff;
            }).Average()
        );

        return new LmsRowFit(row.Key, bestL, m, bestS, rmse);
    }

    // ── BCCG formulas ─────────────────────────────────────────────────────────

    /// <summary>BCCG z-score for a lift y given (L, M, S).</summary>
    public static double BccgZ(double y, double l, double m, double s)
    {
        if (y <= 0 || m <= 0 || s <= 0) return double.NaN;
        double ratio = y / m;
        if (System.Math.Abs(l) < 1e-6)
            return System.Math.Log(ratio) / s;
        return (System.Math.Pow(ratio, l) - 1.0) / (l * s);
    }

    /// <summary>Invert BCCG: given z, return lift y.</summary>
    public static double BccgInverse(double z, double l, double m, double s)
    {
        if (System.Math.Abs(l) < 1e-6)
            return m * System.Math.Exp(s * z);

        double inner = 1.0 + l * s * z;
        if (inner <= 0) return double.NaN; // undefined: outside support
        return m * System.Math.Pow(inner, 1.0 / l);
    }

    // ── Math utilities ─────────────────────────────────────────────────────────

    /// <summary>f_i(L) = ((y/M)^L - 1) / L, with L'Hopital at L=0 giving ln(y/M).</summary>
    private static double F(double y, double m, double l)
    {
        double ratio = y / m;
        if (System.Math.Abs(l) < 1e-6)
            return System.Math.Log(ratio);
        return (System.Math.Pow(ratio, l) - 1.0) / l;
    }

    /// <summary>
    /// Golden-section search for minimum of a unimodal function on [a, b].
    /// </summary>
    private static double GoldenSection(Func<double, double> f, double a, double b, double tol)
    {
        const double phi = 0.6180339887498949;   // (√5 - 1) / 2
        double c = b - phi * (b - a);
        double d = a + phi * (b - a);
        double fc = f(c), fd = f(d);

        while (b - a > tol)
        {
            if (fc < fd) { b = d; d = c; fd = fc; c = b - phi * (b - a); fc = f(c); }
            else         { a = c; c = d; fc = fd; d = a + phi * (b - a); fd = f(d); }
        }
        return 0.5 * (a + b);
    }
}
