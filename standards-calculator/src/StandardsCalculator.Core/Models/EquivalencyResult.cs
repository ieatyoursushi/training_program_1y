namespace StandardsCalculator.Core.Models;

/// <summary>Full output of the Phase-2 gender equivalency calculation.</summary>
public record EquivalencyResult(
    // --- self ---
    double SelfFfmi,
    double SelfNormFfmi,
    double SelfHeightPercentile,  // 0-100

    // --- equivalent opposite-sex lifter ---
    double EquivHeightCm,
    double EquivHeightPercentile, // same percentile mapped into other sex
    double EquivNormFfmi,
    double EquivBodyweight,       // reverse-engineered (in input unit)
    double EquivAssumedBodyFatPct,// body-fat assumption used

    // --- equivalency lift results (may be null if exercise unavailable) ---
    /// <summary>Percentile of the SAME absolute lift for the equivalent lifter.</summary>
    StrengthResult? AbsoluteEquivalent,

    /// <summary>Lift the equivalent lifter would need for the SAME percentile.</summary>
    double? PercentileEquivalentLift,
    StrengthResult? PercentileEquivalent
);
