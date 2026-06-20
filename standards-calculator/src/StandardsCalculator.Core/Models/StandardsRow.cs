namespace StandardsCalculator.Core.Models;

/// <summary>
/// One row in the by-bodyweight or by-age table.
/// Key is bodyweight (lb/kg) or age (years).
/// Boundaries are the five level thresholds: Beginner, Novice, Intermediate, Advanced, Elite.
/// </summary>
public record StandardsRow(
    double Key,        // bodyweight OR age
    double Beginner,
    double Novice,
    double Intermediate,
    double Advanced,
    double Elite
)
{
    /// <summary>Returns the boundary value for the given level index (1=Beg…5=Elite).</summary>
    public double GetBoundary(int levelIndex) => levelIndex switch
    {
        1 => Beginner,
        2 => Novice,
        3 => Intermediate,
        4 => Advanced,
        5 => Elite,
        _ => throw new ArgumentOutOfRangeException(nameof(levelIndex))
    };
}
