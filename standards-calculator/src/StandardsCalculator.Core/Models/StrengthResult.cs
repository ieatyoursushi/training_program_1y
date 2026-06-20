namespace StandardsCalculator.Core.Models;

/// <summary>Full output of the Phase-1 strength calculation.</summary>
public record StrengthResult(
    // --- inputs echoed ---
    LiftInput Input,

    // --- core outputs ---
    double         OneRepMax,           // Epley-estimated 1RM
    double         AgeAdjusted1RM,      // 1RM after age coefficient applied
    double         Percentile,          // 0-100, final body-weight-adjusted percentile
    double         UnadjustedPercentile,// before age/bw adjustment (step 1)
    double         AgeAdjustedPercentile,// after age adj, before BW (step 2)
    StrengthLevel  Level,
    double         BwMultiple,          // lift / bodyweight

    // --- boundary table at this bodyweight ---
    StandardsRow   BoundaryRow,         // interpolated row at user's BW
    double         AgeCoefficient       // c = peak_intermediate / user_intermediate
);
