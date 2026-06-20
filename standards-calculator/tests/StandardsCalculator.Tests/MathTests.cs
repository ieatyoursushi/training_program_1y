using StandardsCalculator.Core.Math;
using StandardsCalculator.Core.Models;
using Xunit;

namespace StandardsCalculator.Tests;

public class OneRepMaxTests
{
    [Fact] public void Epley_OneRep_IsIdentity()
        => Assert.Equal(405.0, OneRepMax.Epley(405, 1));

    [Fact] public void Epley_FiveReps()
        // 100 * (1 + 5/30) = 100 * 1.1667 = 116.67
        => Assert.InRange(OneRepMax.Epley(100, 5), 116.0, 117.5);

    [Fact] public void Epley_ZeroReps_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => OneRepMax.Epley(100, 0));
}

public class DistributionsTests
{
    [Theory]
    [InlineData(0.0,  0.5)]
    [InlineData(-1.6449, 0.05)]
    [InlineData( 1.6449, 0.95)]
    public void NormalCdf_KnownValues(double z, double expectedP)
        => Assert.InRange(Distributions.NormalCdf(z), expectedP - 0.002, expectedP + 0.002);

    [Fact] public void InverseCdf_RoundTrip()
    {
        double p = 0.80;
        double z = Distributions.NormalInverseCdf(p);
        double p2 = Distributions.NormalCdf(z);
        Assert.InRange(p2, p - 0.0001, p + 0.0001);
    }

    [Fact] public void InverseCdf_RoundTrip_Tails()
    {
        foreach (double p in new[] { 0.01, 0.05, 0.20, 0.50, 0.80, 0.95, 0.99 })
        {
            double z  = Distributions.NormalInverseCdf(p);
            double p2 = Distributions.NormalCdf(z);
            Assert.InRange(p2, p - 0.001, p + 0.001);
        }
    }
}

public class PercentileModelTests
{
    // Reference boundary row from strengthlevel.com deadlift, BW=175 lb
    private static readonly StandardsRow DeadliftBw175 =
        new(175, 181, 243, 318, 403, 494);

    [Fact] public void BoundaryHitsExactPercentiles()
    {
        // Beginner boundary → ~5th percentile
        double pBeg = PercentileModel.LiftToPercentile(181, DeadliftBw175);
        Assert.InRange(pBeg, 4.0, 6.0);

        // Advanced boundary → ~80th percentile
        double pAdv = PercentileModel.LiftToPercentile(403, DeadliftBw175);
        Assert.InRange(pAdv, 79.0, 81.0);

        // Elite boundary → ~95th percentile
        double pEli = PercentileModel.LiftToPercentile(494, DeadliftBw175);
        Assert.InRange(pEli, 94.0, 96.0);
    }

    [Fact] public void ReferenceCase_405lb_Advanced_80pct()
    {
        // User's reference: male 19, 175 lb BW, 405×1 deadlift → ~80%, Advanced
        // (age adjustment at age 19 vs peak may shift this ± a few %; test at BW only)
        double pct   = PercentileModel.LiftToPercentile(405, DeadliftBw175);
        var    level = PercentileModel.PercentileToLevel(pct);

        Assert.InRange(pct, 78.0, 85.0);           // allow ±3% before age adj
        Assert.Equal(StrengthLevel.Advanced, level);
    }

    [Fact] public void InverseRoundTrip()
    {
        double target = 75.0;
        double lift   = PercentileModel.PercentileToLift(target, DeadliftBw175);
        double pct    = PercentileModel.LiftToPercentile(lift, DeadliftBw175);
        Assert.InRange(pct, target - 0.5, target + 0.5);
    }

    [Fact] public void Interpolation_BetweenRows()
    {
        var row170 = new StandardsRow(170, 177, 238, 312, 396, 486);
        var row180 = new StandardsRow(180, 184, 248, 324, 410, 502);
        var rows   = new List<StandardsRow> { row170, row180 };

        var interp = PercentileModel.InterpolateByBodyweight(rows, 175);
        Assert.InRange(interp.Advanced, 396, 410);   // halfway
        Assert.InRange(interp.Intermediate, 312, 324);
    }
}

// ─── Stage 2 tests ────────────────────────────────────────────────────────────

public class MonotoneSplineTests
{
    // Deadlift BW=175 reference anchors: (lift, z) pairs
    private static readonly double[] Lifts = [181, 243, 318, 403, 494];
    private static readonly double[] Zs =
        new double[] { 5.0, 20.0, 50.0, 80.0, 95.0 }
            .Select(p => Distributions.NormalInverseCdf(p / 100.0))
            .ToArray();

    [Fact]
    public void PassesThroughEveryKnot()
    {
        var spline = new MonotoneSpline(Lifts, Zs);
        for (int i = 0; i < Lifts.Length; i++)
            Assert.InRange(spline.Evaluate(Lifts[i]), Zs[i] - 1e-9, Zs[i] + 1e-9);
    }

    [Fact]
    public void IsMonotoneOnDenseSample()
    {
        var spline = new MonotoneSpline(Lifts, Zs);
        double prev = spline.Evaluate(Lifts[0]);
        for (double lift = Lifts[0] + 1; lift <= Lifts[^1]; lift += 0.5)
        {
            double curr = spline.Evaluate(lift);
            Assert.True(curr >= prev - 1e-10, $"Spline not monotone at lift={lift:F1}");
            prev = curr;
        }
    }

    [Fact]
    public void TailPercentilesBoundedInZeroOne()
    {
        var row = new StandardsRow(175, 181, 243, 318, 403, 494);
        // Well below Beginner
        double pLow = PercentileModel.LiftToPercentile(50, row);
        Assert.InRange(pLow, 0.0, 5.0);
        // Well above Elite
        double pHigh = PercentileModel.LiftToPercentile(700, row);
        Assert.InRange(pHigh, 95.0, 100.0);
    }

    [Fact]
    public void InverseRoundTrip_DenseGrid()
    {
        var spline = new MonotoneSpline(Lifts, Zs);
        foreach (double z in Zs.Concat(new[] { -2.5, -1.2, 0.3, 1.1, 2.2 }))
        {
            double lift = spline.Inverse(z);
            double zBack = spline.Evaluate(lift);
            Assert.InRange(zBack, z - 1e-6, z + 1e-6);
        }
    }
}

public class LmsModelTests
{
    private static readonly StandardsRow DeadliftBw175 =
        new(175, 181, 243, 318, 403, 494);

    [Fact]
    public void FitRow_MedianIsIntermediate()
    {
        var fit = LmsModel.FitRow(DeadliftBw175);
        Assert.Equal(318.0, fit.M);   // M = Intermediate boundary exactly
    }

    [Fact]
    public void FitRow_LiftRoundTrip()
    {
        var fit = LmsModel.FitRow(DeadliftBw175);
        // For each of the 5 anchor percentiles, percentile→lift→percentile should round-trip
        foreach (double pct in new[] { 5.0, 20.0, 50.0, 80.0, 95.0 })
        {
            double z    = Distributions.NormalInverseCdf(pct / 100.0);
            double lift = LmsModel.BccgInverse(z, fit.L, fit.M, fit.S);
            double zBack = LmsModel.BccgZ(lift, fit.L, fit.M, fit.S);
            Assert.InRange(zBack, z - 0.01, z + 0.01);
        }
    }

    [Fact]
    public void FitRow_GoodnessFit_LowRmse()
    {
        var fit = LmsModel.FitRow(DeadliftBw175);
        // A 3-parameter fit to 5 points should be very close
        Assert.InRange(fit.FitRmse, 0.0, 0.2);
    }

    [Fact]
    public void LmsPercentile_OrderPreserving()
    {
        var fit = LmsModel.FitRow(DeadliftBw175);
        // Higher lift → higher percentile
        double p1 = LiftToPercentileFromFit(200, fit);
        double p2 = LiftToPercentileFromFit(318, fit);
        double p3 = LiftToPercentileFromFit(450, fit);
        Assert.True(p1 < p2 && p2 < p3, $"Expected monotone: {p1:F1} < {p2:F1} < {p3:F1}");
    }

    private static double LiftToPercentileFromFit(double lift, LmsModel.LmsRowFit fit)
    {
        double z = LmsModel.BccgZ(lift, fit.L, fit.M, fit.S);
        return Distributions.NormalCdf(z) * 100.0;
    }
}

public class AgeAdjustmentStage2Tests
{
    // Synthetic by-age table (Intermediate column peaks at age 26)
    private static List<StandardsRow> MakeAgeRows() =>
    [
        new(14, 80,  100, 135, 175, 220),
        new(17, 95,  120, 160, 205, 255),
        new(20, 110, 140, 185, 235, 295),
        new(23, 120, 152, 200, 255, 315),
        new(26, 125, 158, 208, 264, 325),   // peak
        new(29, 123, 156, 205, 261, 320),
        new(33, 118, 150, 198, 252, 312),
        new(40, 108, 137, 182, 232, 288),
        new(50, 95,  121, 161, 205, 256),
        new(60, 82,  105, 140, 179, 224),
    ];

    [Fact]
    public void Coefficient_AtPeakAge_IsOne()
    {
        var rows = MakeAgeRows();
        double c = AgeAdjustment.ComputeCoefficient(rows, 26);
        Assert.InRange(c, 0.995, 1.005);
    }

    [Fact]
    public void Coefficient_YoungAge_GreaterThanOne()
    {
        var rows = MakeAgeRows();
        double c = AgeAdjustment.ComputeCoefficient(rows, 16);
        Assert.True(c > 1.0, $"Expected c>1 for young age; got {c:F4}");
    }

    [Fact]
    public void Diagnostic_CV_ReasonablySmall()
    {
        var rows = MakeAgeRows();
        var diag = AgeAdjustment.ComputeDiagnostic(rows, 40);
        Assert.False(double.IsNaN(diag.RatioCoeffOfVariation));
        // For a cleanly proportional table, CV should be fairly small
        Assert.InRange(diag.RatioCoeffOfVariation, 0.0, 0.3);
    }
}

public class AnthropometricsStage2Tests
{
    [Fact]
    public void EquipercentileFfmi_MaleMeanMapsToFemaleMean()
    {
        // A male at the population mean FFMI should map to the female mean FFMI
        double equivFfmi = Anthropometrics.EquipercentileFfmi(
            Anthropometrics.MaleMeanFfmi, Gender.Male);
        Assert.InRange(equivFfmi, Anthropometrics.FemaleMeanFfmi - 0.01,
                                  Anthropometrics.FemaleMeanFfmi + 0.01);
    }

    [Fact]
    public void EquipercentileFfmi_RoundTrip()
    {
        // Male → Female → Male should recover the original FFMI
        double maleFfmi = 21.5;
        double femaleFfmi = Anthropometrics.EquipercentileFfmi(maleFfmi, Gender.Male);
        double maleFfmiBack = Anthropometrics.EquipercentileFfmi(femaleFfmi, Gender.Female);
        Assert.InRange(maleFfmiBack, maleFfmi - 0.001, maleFfmi + 0.001);
    }

    [Fact]
    public void FfmiPercentile_MeanIsHalf()
    {
        double pct = Anthropometrics.FfmiPercentile(Anthropometrics.MaleMeanFfmi, Gender.Male);
        Assert.InRange(pct, 49.0, 51.0);
    }
}

public class AnthropometricsTests
{
    [Theory]
    [InlineData("6'1",  185.42)]
    [InlineData("6-1",  185.42)]
    [InlineData("185",  185.0)]
    [InlineData("1.85", 185.0)]
    public void ParseHeight(string input, double expectedCm)
    {
        double actual = Anthropometrics.ParseHeightCm(input);
        Assert.InRange(actual, expectedCm - 1.0, expectedCm + 1.0);
    }

    [Fact] public void Ffmi_KnownValues()
    {
        // 175 lb = 79.38 kg, 6'1 = 185.4 cm, 13% BF
        double bwKg     = Anthropometrics.LbToKg(175);
        double heightM  = Anthropometrics.ParseHeightCm("6'1") / 100.0;
        double ffmi     = Anthropometrics.ComputeFfmi(bwKg, heightM, 0.13);
        double normFfmi = Anthropometrics.ComputeNormFfmi(ffmi, heightM);

        // LBM = 79.38 * 0.87 = 69.06 kg; FFMI = 69.06 / (1.854^2) = 69.06 / 3.437 ≈ 20.09
        Assert.InRange(ffmi,     19.5, 21.0);
        Assert.InRange(normFfmi, 19.0, 21.5);
    }

    [Fact] public void HeightPercentile_Symmetry()
    {
        // The male mean should be ~50th percentile
        double pct = Anthropometrics.HeightPercentile(Anthropometrics.MaleMeanHeightCm, Gender.Male);
        Assert.InRange(pct, 49.0, 51.0);
    }

    [Fact] public void EquivHeight_SamePercentile()
    {
        // A male at the 90th percentile → female at ~90th percentile
        double maleH  = Anthropometrics.MaleMeanHeightCm + 1.28 * Anthropometrics.MaleSdHeightCm;
        double femH   = Anthropometrics.EquivalentOppositeHeightCm(maleH, Gender.Male);
        double femPct = Anthropometrics.HeightPercentile(femH, Gender.Female);
        Assert.InRange(femPct, 88.0, 92.0);
    }
}
