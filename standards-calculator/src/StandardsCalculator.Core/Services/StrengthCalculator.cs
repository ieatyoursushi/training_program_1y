using StandardsCalculator.Core.Data;
using StandardsCalculator.Core.Math;
using StandardsCalculator.Core.Models;

namespace StandardsCalculator.Core.Services;

/// <summary>
/// Phase-1: maps a LiftInput to a StrengthResult, replicating strengthlevel.com's
/// three-step computation (unadjusted → age-adjusted → bodyweight-relative percentile).
/// </summary>
public class StrengthCalculator
{
    private readonly StandardsRepository _repo;

    public StrengthCalculator(StandardsRepository repo) => _repo = repo;

    /// <summary>
    /// Async path: loads the table from the repository, then calculates.
    /// </summary>
    public async Task<StrengthResult> CalculateAsync(LiftInput input,
                                                      CancellationToken ct = default)
    {
        var table = await _repo.GetAsync(input.Exercise, input.Gender, input.Unit, ct: ct);
        return Calculate(input, table);
    }

    /// <summary>
    /// Synchronous path: computes from a pre-loaded table (used by the desktop GUI
    /// where the table is already in memory from the exercise-select page).
    /// </summary>
    public static StrengthResult Calculate(LiftInput input, StandardsTable table)
    {
        // Step 0 – Epley 1RM
        double oneRepMax = OneRepMax.Epley(input.WeightLifted, input.Reps);

        // Step 1 – Unadjusted percentile vs. all same-gender lifters at this bodyweight
        var bwRow = PercentileModel.InterpolateByBodyweight(table.ByBodyweight, input.Bodyweight);
        double unadjustedPct = PercentileModel.LiftToPercentile(oneRepMax, bwRow);

        // Step 2 – Age adjustment
        double ageCoeff       = AgeAdjustment.ComputeCoefficient(table.ByAge, input.Age);
        double ageAdj1RM      = oneRepMax * ageCoeff;
        double ageAdjustedPct = PercentileModel.LiftToPercentile(ageAdj1RM, bwRow);

        // Step 3 – Final BW-relative percentile (same bwRow)
        double finalPct = PercentileModel.LiftToPercentile(ageAdj1RM, bwRow);

        var    level    = PercentileModel.PercentileToLevel(finalPct);
        double bwMulti  = oneRepMax / input.Bodyweight;

        return new StrengthResult(
            Input:                  input,
            OneRepMax:              oneRepMax,
            AgeAdjusted1RM:         ageAdj1RM,
            Percentile:             finalPct,
            UnadjustedPercentile:   unadjustedPct,
            AgeAdjustedPercentile:  ageAdjustedPct,
            Level:                  level,
            BwMultiple:             bwMulti,
            BoundaryRow:            bwRow,
            AgeCoefficient:         ageCoeff
        );
    }
}
