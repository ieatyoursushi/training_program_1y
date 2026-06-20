using StandardsCalculator.Core.Models;
using System.Text.RegularExpressions;

namespace StandardsCalculator.Core.Math;

/// <summary>
/// Anthropometric calculations for Phase-2 gender equivalency.
///
/// FFMI:
///   LBM  = weight_kg × (1 - bf_fraction)
///   FFMI = LBM / height_m²
///   normFFMI = FFMI + 6.1 × (1.8 - height_m)
///   (Casey &amp; Natasha Butt, 2001; common normalization)
///
/// Height percentile:
///   Male:   N(μ=175.3 cm, σ=7.4 cm)  [US CDC / NHANES adult data]
///   Female: N(μ=161.8 cm, σ=7.1 cm)
///   z = (h - μ) / σ;  percentile = Φ(z)
///
/// Opposite-sex equivalent height:
///   Same z applied to the other sex's distribution.
///
/// FFMI normalization across sexes:
///   Natural ceiling: male ≈ 25, female ≈ 22  (configurable)
///   normFFMI_other = normFFMI_self × (ceiling_other / ceiling_self)
///
/// Reverse bodyweight:
///   Given equivalent height (m) and normalized FFMI:
///   FFMI_other = normFFMI_other - 6.1 × (1.8 - height_other_m)
///   LBM_other  = FFMI_other × height_other_m²
///   BW_other   = LBM_other / (1 - bf_other_fraction)
/// </summary>
public static class Anthropometrics
{
    // ── Height distribution constants (cm) ──────────────────────────────────
    public const double MaleMeanHeightCm   = 175.3;
    public const double MaleSdHeightCm     =   7.4;
    public const double FemaleMeanHeightCm = 161.8;
    public const double FemaleSdHeightCm   =   7.1;

    // ── FFMI natural ceilings ────────────────────────────────────────────────
    public const double MaleNaturalFfmiCeiling   = 25.0;
    public const double FemaleNaturalFfmiCeiling = 22.0;

    // ── FFMI population distributions (for equipercentile equating) ──────────
    // Source: Kyle et al. (2003) Int J Obesity 27:953-963 (Caucasian adults 24-98 y);
    //         young-adult (18-34 y) medians: male 18.9, female 15.4 kg/m².
    // Mean set to median + slight right-skew adjustment; SD from reference range.
    // Configurable in data/anthropometrics.json (documented there for transparency).
    public const double MaleMeanFfmi   = 20.0;
    public const double MaleSdFfmi     =  1.9;
    public const double FemaleMeanFfmi = 15.4;
    public const double FemaleSdFfmi   =  1.6;

    // ── Unit conversion ──────────────────────────────────────────────────────
    public static double LbToKg(double lb) => lb * 0.453592;
    public static double KgToLb(double kg) => kg / 0.453592;

    // ── Height parsing ───────────────────────────────────────────────────────
    /// <summary>
    /// Parses height input to centimetres.
    /// Accepts: "6'1", "6'1\"", "6-1", "73", "185.4", "1.85" (raw metres if ≤ 2.5).
    /// Feet-and-inches separator: ' or -.
    /// </summary>
    public static double ParseHeightCm(string input)
    {
        input = input.Trim().Replace("\"", "");

        // Feet-inches: 6'1  or  6-1  or  6' 1
        var feetInches = Regex.Match(input, @"^(\d+)['\- ](\d+(?:\.\d+)?)$");
        if (feetInches.Success)
        {
            double feet   = double.Parse(feetInches.Groups[1].Value);
            double inches = double.Parse(feetInches.Groups[2].Value);
            return (feet * 12.0 + inches) * 2.54;
        }

        // Only feet: 6'
        var onlyFeet = Regex.Match(input, @"^(\d+)'$");
        if (onlyFeet.Success)
            return double.Parse(onlyFeet.Groups[1].Value) * 30.48;

        // Numeric
        if (double.TryParse(input, out double val))
        {
            // Treat values ≤ 2.5 as metres (e.g. 1.85 m → 185 cm)
            if (val <= 2.5) return val * 100.0;
            // Treat values 50–100 as probably inches
            if (val < 120)  return val * 2.54;
            // Otherwise already cm
            return val;
        }

        throw new FormatException($"Cannot parse height: '{input}'. Examples: 6'1, 6-1, 185, 1.85");
    }

    // ── FFMI calculations ────────────────────────────────────────────────────

    public static double ComputeFfmi(double weightKg, double heightM, double bodyFatFraction)
    {
        double lbm = weightKg * (1.0 - bodyFatFraction);
        return lbm / (heightM * heightM);
    }

    public static double ComputeNormFfmi(double ffmi, double heightM)
        => ffmi + 6.1 * (1.8 - heightM);

    /// <summary>Reverse: given normFFMI and height, return raw FFMI.</summary>
    public static double NormFfmiToFfmi(double normFfmi, double heightM)
        => normFfmi - 6.1 * (1.8 - heightM);

    // ── Height percentile ────────────────────────────────────────────────────

    public static double HeightPercentile(double heightCm, Gender gender)
    {
        double mu = gender == Gender.Male ? MaleMeanHeightCm : FemaleMeanHeightCm;
        double sd = gender == Gender.Male ? MaleSdHeightCm   : FemaleSdHeightCm;
        double z  = (heightCm - mu) / sd;
        return Distributions.NormalCdf(z) * 100.0;
    }

    /// <summary>
    /// Returns the height (cm) in the opposite sex's distribution that sits at
    /// the same percentile as the user's height in their own distribution.
    /// </summary>
    public static double EquivalentOppositeHeightCm(double heightCm, Gender selfGender)
    {
        Gender otherGender = selfGender == Gender.Male ? Gender.Female : Gender.Male;

        double muSelf = selfGender  == Gender.Male ? MaleMeanHeightCm : FemaleMeanHeightCm;
        double sdSelf = selfGender  == Gender.Male ? MaleSdHeightCm   : FemaleSdHeightCm;
        double muOther= otherGender == Gender.Male ? MaleMeanHeightCm : FemaleMeanHeightCm;
        double sdOther= otherGender == Gender.Male ? MaleSdHeightCm   : FemaleSdHeightCm;

        double z = (heightCm - muSelf) / sdSelf;
        return muOther + z * sdOther;
    }

    // ── FFMI cross-sex normalization ─────────────────────────────────────────

    public static double NormalizeFfmiToOpposite(double normFfmi, Gender selfGender,
                                                  double? maleCeiling   = null,
                                                  double? femaleCeiling = null)
    {
        double selfCeiling  = selfGender == Gender.Male
            ? (maleCeiling   ?? MaleNaturalFfmiCeiling)
            : (femaleCeiling ?? FemaleNaturalFfmiCeiling);
        double otherCeiling = selfGender == Gender.Male
            ? (femaleCeiling ?? FemaleNaturalFfmiCeiling)
            : (maleCeiling   ?? MaleNaturalFfmiCeiling);

        return normFfmi * (otherCeiling / selfCeiling);
    }

    /// <summary>
    /// Equipercentile equating of FFMI across sexes.
    ///
    /// Maps the user's raw FFMI to the same percentile rank in the opposite sex's
    /// FFMI distribution — the same statistical method used for height equating.
    ///
    /// The returned value is a raw FFMI (kg/m²) in the opposite sex's distribution;
    /// convert to normFFMI with ComputeNormFfmi(result, equivHeightM).
    ///
    /// Distribution parameters:
    ///   Male:   N(μ=20.0, σ=1.9)   [Kyle et al. 2003; NHANES reference]
    ///   Female: N(μ=15.4, σ=1.6)
    ///   Constants: MaleMeanFfmi, MaleSdFfmi, FemaleMeanFfmi, FemaleSdFfmi.
    ///
    /// This is the statistically preferred method (equivalent to height equating and
    /// standard in psychometric test equating).  The ceiling-ratio method
    /// (NormalizeFfmiToOpposite) is retained for comparison as a research alternative.
    /// </summary>
    public static double EquipercentileFfmi(double selfFfmi, Gender selfGender)
    {
        double muSelf  = selfGender == Gender.Male ? MaleMeanFfmi   : FemaleMeanFfmi;
        double sdSelf  = selfGender == Gender.Male ? MaleSdFfmi     : FemaleSdFfmi;
        double muOther = selfGender == Gender.Male ? FemaleMeanFfmi : MaleMeanFfmi;
        double sdOther = selfGender == Gender.Male ? FemaleSdFfmi   : MaleSdFfmi;

        double z = (selfFfmi - muSelf) / sdSelf;
        return muOther + z * sdOther;
    }

    /// <summary>
    /// Compute the user's FFMI percentile within their sex's population distribution.
    /// Uses the same parameters as EquipercentileFfmi.
    /// </summary>
    public static double FfmiPercentile(double ffmi, Gender gender)
    {
        double mu = gender == Gender.Male ? MaleMeanFfmi : FemaleMeanFfmi;
        double sd = gender == Gender.Male ? MaleSdFfmi   : FemaleSdFfmi;
        double z  = (ffmi - mu) / sd;
        return Distributions.NormalCdf(z) * 100.0;
    }

    // ── Reverse-engineered bodyweight ────────────────────────────────────────

    /// <summary>
    /// Given the equivalent lifter's normFFMI, height, and assumed body-fat %,
    /// reverse-engineer their bodyweight.
    /// </summary>
    /// <param name="equivNormFfmi">Normalized FFMI of the equivalent lifter.</param>
    /// <param name="equivHeightM">Height of the equivalent lifter in metres.</param>
    /// <param name="equivBodyFatFraction">Assumed body-fat fraction (0–1) for the equiv. lifter.</param>
    /// <param name="unit">Desired output unit.</param>
    public static double ReverseBodyweight(double equivNormFfmi, double equivHeightM,
                                           double equivBodyFatFraction, Unit unit)
    {
        double ffmi = NormFfmiToFfmi(equivNormFfmi, equivHeightM);
        double lbm  = ffmi * equivHeightM * equivHeightM;             // kg
        double bwKg = lbm / (1.0 - equivBodyFatFraction);
        return unit == Unit.Lb ? KgToLb(bwKg) : bwKg;
    }
}
