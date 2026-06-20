using StandardsCalculator.Core.Models;

namespace StandardsCalculator.Core.Math;

/// <summary>
/// Computes the age adjustment coefficient and age-adjusted percentile.
///
/// METHOD:
///   1. Fit a PCHIP spline to the by-age table's Intermediate column vs age.
///      This produces a smooth M_age(age) curve (not the raw, noisy row values).
///   2. Find the peak age: sweep the spline on a 0.1-year grid and take the maximum.
///   3. Age coefficient: c(age) = M_age(peak) / M_age(user_age).
///   4. Age-adjusted 1RM = 1RM × c(age).
///
///   Using the smoothed curve (rather than the raw ratio of adjacent rows) reduces
///   sensitivity to individual data points in the table and gives a sensible coefficient
///   even when the user's age falls between two widely-spaced rows.
///
/// MULTIPLICATIVE-ASSUMPTION DIAGNOSTIC:
///   The multiplicative adjustment assumes that the age effect scales all boundary
///   levels proportionally:  boundary_i(peak) / boundary_i(age) ≈ constant for all i.
///
///   ComputeDiagnostic() tests this by computing the ratio for each of the 5 levels
///   and reporting the coefficient of variation (CV = SD/mean).  A small CV (< ~0.05)
///   validates the assumption; a large CV suggests that age affects novice and elite
///   lifters differently, motivating a more complex age-by-level model.
///
/// NOTE: strengthlevel.com's exact age-adjustment algorithm is not published.
///   This is a reverse-engineered approximation. Run --verify-live to measure drift
///   against the live site.
/// </summary>
public static class AgeAdjustment
{
    /// <summary>
    /// Returns the age coefficient c ≥ 1.
    /// At the peak age, c = 1; for younger or older ages, c &gt; 1 (lift is scaled up).
    /// </summary>
    public static double ComputeCoefficient(List<StandardsRow> ageRows, int age)
    {
        if (ageRows.Count == 0) return 1.0;
        if (ageRows.Count == 1) return 1.0;

        // Fit a smooth curve to (age, Intermediate) — PCHIP handles the peak naturally
        var (spline, peakAge) = FitAgeSpline(ageRows, r => r.Intermediate);
        if (spline is null) return 1.0;

        double peakValue = spline.Evaluate(peakAge);
        double userValue = spline.Evaluate(System.Math.Clamp(age, ageRows[0].Key, ageRows[^1].Key));

        if (userValue <= 0) return 1.0;
        return peakValue / userValue;
    }

    /// <summary>
    /// Diagnostic: test whether the multiplicative shift assumption holds across all 5 levels.
    /// </summary>
    /// <returns>
    /// A record containing one ratio per level (peak/user) and the coefficient of variation.
    /// CV &lt; 0.05 → assumption validated.  CV &gt; 0.15 → consider a level-specific model.
    /// </returns>
    public static AgeAdjustmentDiagnostic ComputeDiagnostic(List<StandardsRow> ageRows, int age)
    {
        if (ageRows.Count < 2)
            return new AgeAdjustmentDiagnostic(new double[5], double.NaN, 1.0);

        // Build individual splines per level
        Func<StandardsRow, double>[] selectors =
        [
            r => r.Beginner, r => r.Novice, r => r.Intermediate, r => r.Advanced, r => r.Elite
        ];

        double clampedAge = System.Math.Clamp(age, ageRows[0].Key, ageRows[^1].Key);
        double[] ratios = new double[5];
        double overallCoeff = 1.0;

        for (int i = 0; i < 5; i++)
        {
            var (spline, peakAge) = FitAgeSpline(ageRows, selectors[i]);
            if (spline is null) { ratios[i] = 1.0; continue; }

            double peak = spline.Evaluate(peakAge);
            double user = spline.Evaluate(clampedAge);
            ratios[i] = user > 0 ? peak / user : 1.0;
            if (i == 2) overallCoeff = ratios[i]; // Intermediate = the production coefficient
        }

        double mean = ratios.Average();
        double cv   = mean > 0
            ? System.Math.Sqrt(ratios.Average(r => (r - mean) * (r - mean))) / mean
            : double.NaN;

        return new AgeAdjustmentDiagnostic(ratios, cv, overallCoeff);
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Fit a PCHIP spline to (age, value) from ageRows using the given selector,
    /// and find the peak age on a fine grid.
    /// </summary>
    private static (MonotoneSpline? spline, double peakAge) FitAgeSpline(
        List<StandardsRow> rows, Func<StandardsRow, double> selector)
    {
        // Filter rows where the selected value is positive
        var valid = rows.Where(r => selector(r) > 0).ToList();
        if (valid.Count < 2) return (null, 0);

        double[] ages   = valid.Select(r => r.Key).ToArray();
        double[] values = valid.Select(selector).ToArray();

        MonotoneSpline spline;
        try { spline = new MonotoneSpline(ages, values); }
        catch { return (null, 0); }

        // Sweep on a 0.1-year grid to find the peak
        double minAge  = ages[0];
        double maxAge  = ages[^1];
        double peakAge = minAge;
        double peakVal = spline.Evaluate(minAge);

        for (double a = minAge; a <= maxAge; a += 0.1)
        {
            double v = spline.Evaluate(a);
            if (v > peakVal) { peakVal = v; peakAge = a; }
        }

        return (spline, peakAge);
    }
}

/// <summary>
/// Diagnostic report from AgeAdjustment.ComputeDiagnostic.
/// </summary>
public record AgeAdjustmentDiagnostic(
    double[] LevelRatios,               // peak/user ratio for each of 5 levels
    double   RatioCoeffOfVariation,     // CV of the 5 ratios; small → multiplicative OK
    double   IntermediateCoefficient    // the production coefficient (level 2 = Intermediate)
)
{
    /// <summary>Returns true if the multiplicative assumption is well-supported (CV &lt; 0.05).</summary>
    public bool IsMultiplicativeAssumptionValid => RatioCoeffOfVariation < 0.05;

    /// <summary>Human-readable description of the diagnostic result.</summary>
    public string Summary()
    {
        if (double.IsNaN(RatioCoeffOfVariation))
            return "Diagnostic unavailable (insufficient age data).";

        string verdict = IsMultiplicativeAssumptionValid
            ? "✓ Multiplicative assumption holds"
            : RatioCoeffOfVariation < 0.15
                ? "~ Multiplicative assumption is approximate"
                : "✗ Multiplicative assumption is suspect — level-specific age model recommended";

        string ratioStr = string.Join(", ", LevelRatios.Select((r, i) =>
            $"{new[]{"Beg","Nov","Int","Adv","Eli"}[i]}={r:F3}"));

        return $"{verdict} (CV={RatioCoeffOfVariation:F3}; {ratioStr})";
    }
}
