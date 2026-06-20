using StandardsCalculator.Core.Math;
using StandardsCalculator.Core.Models;

namespace StandardsCalculator.Core.Services;

/// <summary>
/// Cross-sex equivalency method for FFMI normalization.
/// </summary>
public enum FfmiEquivMethod
{
    /// <summary>
    /// Equipercentile equating (default): match the user's FFMI percentile rank in their
    /// own sex's population distribution to the same rank in the opposite sex's distribution.
    /// This is the same statistical method used for height equating, and is the defensible
    /// standard in psychometric equating theory.
    /// Distribution params: male N(20.0, 1.9²), female N(15.4, 1.6²) — see anthropometrics.json.
    /// </summary>
    EquipercentileEquating,

    /// <summary>
    /// Ceiling-ratio scaling (Stage 1 default, retained for research/comparison):
    ///   normFFMI_other = normFFMI_self × (ceiling_other / ceiling_self)
    /// Interprets cross-sex equivalency as preserving "fraction of the natural ceiling."
    /// Statistically ad hoc and inconsistent with the percentile-preserving height mapping,
    /// but intuitive as a "potential utilized" framing.
    /// </summary>
    CeilingRatio
}

/// <summary>
/// Phase-2: derives the opposite-gender equivalent lifter from anthropometric inputs
/// and maps the user's lift (absolute and percentile) onto that lifter.
///
/// Two FFMI cross-sex methods are available (see FfmiEquivMethod):
///   - EquipercentileEquating (default): statistically consistent, mirrors height equating.
///   - CeilingRatio: Stage 1 approach; retained for research comparison.
///
/// The divergence between the two methods is itself a research artifact: it quantifies
/// how much "fraction of ceiling" departs from "fraction of population rank," which
/// is relevant to questions of body dysmorphia vs actual standards.
/// </summary>
public class EquivalencyCalculator
{
    private readonly StrengthCalculator _strengthCalc;

    /// <summary>FFMI cross-sex equating method. Defaults to EquipercentileEquating.</summary>
    public FfmiEquivMethod FfmiMethod { get; set; } = FfmiEquivMethod.EquipercentileEquating;

    // Default assumed body-fat % for the equivalent opposite-sex lifter.
    public double DefaultEquivMaleBodyFatPct   { get; set; } = 13.0;
    public double DefaultEquivFemaleBodyFatPct { get; set; } = 22.0;

    public EquivalencyCalculator(StrengthCalculator strengthCalc)
        => _strengthCalc = strengthCalc;

    /// <summary>
    /// Compute the full equivalency given the user's strength result and their anthropometric profile.
    /// </summary>
    public async Task<EquivalencyResult> CalculateAsync(
        StrengthResult strengthResult,
        AnthroProfile  profile,
        CancellationToken ct = default)
    {
        var input        = strengthResult.Input;
        var selfGender   = input.Gender;
        var otherGender  = selfGender == Gender.Male ? Gender.Female : Gender.Male;

        // Convert bodyweight to kg for FFMI
        double bwKg = input.Unit == Unit.Lb
            ? Anthropometrics.LbToKg(input.Bodyweight)
            : input.Bodyweight;

        double heightM = profile.HeightCm / 100.0;
        double bfFrac  = profile.BodyFatPct / 100.0;

        // ── Self FFMI ───────────────────────────────────────────────────────
        double selfFfmi     = Anthropometrics.ComputeFfmi(bwKg, heightM, bfFrac);
        double selfNormFfmi = Anthropometrics.ComputeNormFfmi(selfFfmi, heightM);

        // ── Height percentile & equiv height ────────────────────────────────
        double selfHeightPct  = Anthropometrics.HeightPercentile(profile.HeightCm, selfGender);
        double equivHeightCm  = Anthropometrics.EquivalentOppositeHeightCm(profile.HeightCm, selfGender);
        double equivHeightPct = Anthropometrics.HeightPercentile(equivHeightCm, otherGender);
        double equivHeightM   = equivHeightCm / 100.0;

        // ── Equiv FFMI normalization ─────────────────────────────────────────
        double equivNormFfmi = FfmiMethod switch
        {
            FfmiEquivMethod.EquipercentileEquating =>
                // Step 1: match FFMI percentile rank to opposite-sex distribution
                // Step 2: convert that raw FFMI to normFFMI at the equivalent height
                Anthropometrics.ComputeNormFfmi(
                    Anthropometrics.EquipercentileFfmi(selfFfmi, selfGender),
                    equivHeightM),

            FfmiEquivMethod.CeilingRatio =>
                Anthropometrics.NormalizeFfmiToOpposite(selfNormFfmi, selfGender),

            _ => throw new ArgumentOutOfRangeException(nameof(FfmiMethod))
        };

        // ── Reverse-engineer equiv bodyweight ────────────────────────────────
        double equivBfPct  = otherGender == Gender.Female
            ? DefaultEquivFemaleBodyFatPct
            : DefaultEquivMaleBodyFatPct;
        double equivBfFrac = equivBfPct / 100.0;

        double equivBw = Anthropometrics.ReverseBodyweight(
            equivNormFfmi, equivHeightM, equivBfFrac, input.Unit);

        // ── (a) Absolute equivalent: same lift for the equiv lifter ──────────
        var absInput = input with
        {
            Gender       = otherGender,
            Bodyweight   = equivBw,
            WeightLifted = input.WeightLifted,
            Reps         = input.Reps
        };
        StrengthResult? absEquiv = null;
        try { absEquiv = await _strengthCalc.CalculateAsync(absInput, ct); }
        catch { /* exercise may not be available for opposite gender */ }

        // ── (b) Percentile equivalent: lift the equiv lifter needs for same % ─
        double? pctEquivLift = null;
        StrengthResult? pctEquiv = null;
        if (absEquiv is not null)
        {
            double targetPct = strengthResult.Percentile;
            pctEquivLift = PercentileModel.PercentileToLift(targetPct, absEquiv.BoundaryRow);

            var pctInput = absInput with
            {
                WeightLifted = pctEquivLift.Value,
                Reps         = 1
            };
            try { pctEquiv = await _strengthCalc.CalculateAsync(pctInput, ct); }
            catch { /* best effort */ }
        }

        return new EquivalencyResult(
            SelfFfmi:               selfFfmi,
            SelfNormFfmi:           selfNormFfmi,
            SelfHeightPercentile:   selfHeightPct,
            EquivHeightCm:          equivHeightCm,
            EquivHeightPercentile:  equivHeightPct,
            EquivNormFfmi:          equivNormFfmi,
            EquivBodyweight:        equivBw,
            EquivAssumedBodyFatPct: equivBfPct,
            AbsoluteEquivalent:     absEquiv,
            PercentileEquivalentLift: pctEquivLift,
            PercentileEquivalent:   pctEquiv
        );
    }
}
