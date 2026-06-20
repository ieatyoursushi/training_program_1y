using StandardsCalculator.Core.Models;

namespace StandardsCalculator.Core.Math;

/// <summary>
/// Maps a lift (1RM) to a percentile and back, given a standards table.
///
/// METHOD (PCHIP spline + lognormal tails):
///   The five boundary values (Beginner, Novice, Intermediate, Advanced, Elite) at a
///   given bodyweight are known quantiles at fixed percentiles:
///     5%, 20%, 50%, 80%, 95%
///   corresponding to z-scores:
///     Φ⁻¹(0.05) ≈ -1.6449
///     Φ⁻¹(0.20) ≈ -0.8416
///     Φ⁻¹(0.50) =  0.0000
///     Φ⁻¹(0.80) ≈ +0.8416
///     Φ⁻¹(0.95) ≈ +1.6449
///
///   A monotone cubic Hermite (PCHIP, Fritsch &amp; Carlson 1980) spline is fitted
///   through the five knots (lift_i, z_i).  This gives a C¹-smooth, strictly
///   monotone lift→z map that passes EXACTLY through every published boundary.
///
///   Beyond the anchors (sub-Beginner / supra-Elite), a log-linear extension is used:
///     z_ext = z_edge + (lift_edge · dz/dlift_edge) · ln(lift / lift_edge)
///   This is equivalent to a lognormal tail, keeping the lift→percentile map
///   bounded in (0, 100) as lift→0 or lift→∞.
///
///   Inverse (percentile → lift): same spline inverted via bisection within each
///   cubic segment and exact analytic inversion in the tails.
///
///   Bodyweight rows are linearly interpolated first (unchanged from Stage 1).
///
///   For a research-mode fit using the Box-Cox Cole-Green (LMS) distribution,
///   see LmsModel.
/// </summary>
public static class PercentileModel
{
    // Five anchor percentiles and their z-scores (computed once at startup)
    private static readonly double[] AnchorPercentiles = [5, 20, 50, 80, 95];
    private static readonly double[] AnchorZ =
        AnchorPercentiles.Select(p => Distributions.NormalInverseCdf(p / 100.0)).ToArray();

    // Level names for display
    public static readonly string[] LevelNames =
        ["Below Beginner", "Beginner", "Novice", "Intermediate", "Advanced", "Elite"];

    /// <summary>
    /// Interpolate a StandardsRow at the given bodyweight from the table's ByBodyweight list.
    /// Clamps to the first/last row if outside range.
    /// </summary>
    public static StandardsRow InterpolateByBodyweight(List<StandardsRow> rows, double bodyweight)
    {
        if (rows.Count == 0) throw new InvalidOperationException("Standards table has no bodyweight rows.");
        if (bodyweight <= rows[0].Key)  return rows[0];
        if (bodyweight >= rows[^1].Key) return rows[^1];

        int i = rows.FindLastIndex(r => r.Key <= bodyweight);
        var lo = rows[i];
        var hi = rows[i + 1];
        double t = (bodyweight - lo.Key) / (hi.Key - lo.Key);

        return new StandardsRow(
            Key:          bodyweight,
            Beginner:     Lerp(lo.Beginner,    hi.Beginner,    t),
            Novice:       Lerp(lo.Novice,       hi.Novice,      t),
            Intermediate: Lerp(lo.Intermediate, hi.Intermediate, t),
            Advanced:     Lerp(lo.Advanced,     hi.Advanced,    t),
            Elite:        Lerp(lo.Elite,        hi.Elite,       t)
        );
    }

    /// <summary>
    /// Interpolate a StandardsRow at the given age from the table's ByAge list.
    /// Clamps to the first/last row if outside range.
    /// </summary>
    public static StandardsRow InterpolateByAge(List<StandardsRow> rows, int age)
    {
        if (rows.Count == 0) throw new InvalidOperationException("Standards table has no age rows.");
        double d = age;
        if (d <= rows[0].Key)  return rows[0];
        if (d >= rows[^1].Key) return rows[^1];

        int i = rows.FindLastIndex(r => r.Key <= d);
        var lo = rows[i];
        var hi = rows[i + 1];
        double t = (d - lo.Key) / (hi.Key - lo.Key);

        return new StandardsRow(
            Key:          d,
            Beginner:     Lerp(lo.Beginner,    hi.Beginner,    t),
            Novice:       Lerp(lo.Novice,       hi.Novice,      t),
            Intermediate: Lerp(lo.Intermediate, hi.Intermediate, t),
            Advanced:     Lerp(lo.Advanced,     hi.Advanced,    t),
            Elite:        Lerp(lo.Elite,        hi.Elite,       t)
        );
    }

    /// <summary>
    /// Compute the percentile (0–100) for a 1RM relative to a boundary row,
    /// using the PCHIP spline model.
    /// </summary>
    public static double LiftToPercentile(double oneRepMax, StandardsRow row)
    {
        var spline = BuildSpline(row);
        double z = spline.Evaluate(oneRepMax);
        return System.Math.Clamp(Distributions.NormalCdf(z) * 100.0, 0.0, 100.0);
    }

    /// <summary>
    /// Determine the StrengthLevel from a percentile using the fixed thresholds.
    /// </summary>
    public static StrengthLevel PercentileToLevel(double percentile) => percentile switch
    {
        >= 95 => StrengthLevel.Elite,
        >= 80 => StrengthLevel.Advanced,
        >= 50 => StrengthLevel.Intermediate,
        >= 20 => StrengthLevel.Novice,
        >= 5  => StrengthLevel.Beginner,
        _     => StrengthLevel.BelowBeginner
    };

    /// <summary>
    /// Inverse: given a percentile, return the lift that achieves it from a boundary row.
    /// </summary>
    public static double PercentileToLift(double percentile, StandardsRow row)
    {
        var spline = BuildSpline(row);
        double z = Distributions.NormalInverseCdf(percentile / 100.0);
        return spline.Inverse(z);
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Build a PCHIP spline for the given row.  x = lift values, y = z-scores.
    /// Called once per public API invocation; building a 5-knot spline is ~30 ops.
    /// </summary>
    internal static MonotoneSpline BuildSpline(StandardsRow row)
    {
        double[] lifts = [row.Beginner, row.Novice, row.Intermediate, row.Advanced, row.Elite];
        return new MonotoneSpline(lifts, AnchorZ);
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}
