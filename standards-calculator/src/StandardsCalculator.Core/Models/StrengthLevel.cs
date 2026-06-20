namespace StandardsCalculator.Core.Models;

/// <summary>
/// Five-tier classification used by strengthlevel.com.
/// Percentile thresholds: Beginner=5, Novice=20, Intermediate=50, Advanced=80, Elite=95.
/// </summary>
public enum StrengthLevel
{
    BelowBeginner = 0,
    Beginner      = 1,
    Novice        = 2,
    Intermediate  = 3,
    Advanced      = 4,
    Elite         = 5
}
