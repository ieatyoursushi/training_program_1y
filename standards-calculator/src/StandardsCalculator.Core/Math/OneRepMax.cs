namespace StandardsCalculator.Core.Math;

/// <summary>
/// 1-Rep Max estimation formulas.
///
/// METHOD: Epley formula (1985)
///   1RM = w × (1 + reps / 30)
///   When reps = 1, the formula is the identity: 1RM = w.
///   This is the standard used by strengthlevel.com.
/// </summary>
public static class OneRepMax
{
    /// <summary>
    /// Estimates 1RM using the Epley formula.
    /// </summary>
    /// <param name="weight">Weight lifted.</param>
    /// <param name="reps">Repetitions performed (≥1).</param>
    public static double Epley(double weight, int reps)
    {
        if (reps < 1) throw new ArgumentOutOfRangeException(nameof(reps), "Reps must be at least 1.");
        if (reps == 1) return weight;
        return weight * (1.0 + reps / 30.0);
    }
}
