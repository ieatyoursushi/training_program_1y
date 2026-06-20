using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StandardsCalculator.Core.Math;
using StandardsCalculator.Core.Models;
using StandardsCalculator.Core.Services;
using StandardsCalculator.Desktop.Models;

namespace StandardsCalculator.Desktop.ViewModels;

/// <summary>
/// Page 2 — live reactive results.
///
/// COUPLING MODEL:
///   weight_kg, height_cm, body_fat_pct → FFMI, normFFMI  (forward derivation)
///   When FFMI is edited directly, a "solve for" toggle picks which upstream
///   variable absorbs the change (default: bodyweight).
///
/// RE-ENTRANCY GUARD:
///   _isUpdating prevents cascading property changes from triggering recursive recomputes.
///   The guard is set before any derived-field write and cleared after the Recompute pass.
///
/// METHOD SELECTORS (research affordances):
///   PercentileMethod: Spline (PCHIP, default) | LMS (Box-Cox Cole-Green)
///   FfmiEquivMethod:  Equipercentile (default) | CeilingRatio
/// </summary>
public partial class ResultsViewModel : ViewModelBase
{
    private readonly SessionModel _session;
    private bool _isUpdating;

    // ─────────────────────────────────────────────────────────────────────────
    // Page header
    // ─────────────────────────────────────────────────────────────────────────
    public string ExerciseTitle =>
        System.Globalization.CultureInfo.CurrentCulture.TextInfo
              .ToTitleCase(_session.Exercise.Replace('-', ' '));
    public string UnitLabel => _session.Unit == Unit.Lb ? "lb" : "kg";

    // ─────────────────────────────────────────────────────────────────────────
    // Inputs — observable so the UI binds to them
    // ─────────────────────────────────────────────────────────────────────────

    [ObservableProperty] private bool _isMale = true;    // true = Male, false = Female
    [ObservableProperty] private int  _age = 25;
    [ObservableProperty] private double _bodyweight = 175;
    [ObservableProperty] private string _heightInput = "5'10";
    [ObservableProperty] private double _bodyFatPct = 15;
    [ObservableProperty] private double _weightLifted = 135;
    [ObservableProperty] private int    _reps = 1;

    // Derived, but also directly editable (bidirectional coupling via SolveFor)
    [ObservableProperty] private double _ffmi = 0;
    [ObservableProperty] private double _normFfmi = 0;

    // ─── SolveFor toggle (which var absorbs a direct FFMI edit) ───────────────
    public ObservableCollection<string> SolveForOptions { get; } =
        ["Bodyweight", "Body-fat %", "FFMI (display only)"];
    [ObservableProperty] private int _solveForIndex = 0;   // 0=BW, 1=BF, 2=display-only

    // ─── Method selectors ─────────────────────────────────────────────────────
    [ObservableProperty] private bool _useLms;            // false = PCHIP (default)
    [ObservableProperty] private bool _useCeilingRatio;   // false = Equipercentile (default)

    // ─────────────────────────────────────────────────────────────────────────
    // Parsed height (kept so we can show it and for recompute)
    // ─────────────────────────────────────────────────────────────────────────
    [ObservableProperty] private double _heightCm;
    [ObservableProperty] private string _heightParseError = string.Empty;

    // ─────────────────────────────────────────────────────────────────────────
    // Output — user's result
    // ─────────────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _selfLevelText   = "—";
    [ObservableProperty] private string _selfStars       = "";
    [ObservableProperty] private string _selfPercentile  = "—";
    [ObservableProperty] private string _selfBwMultiple  = "—";
    [ObservableProperty] private string _selfOneRepMax   = "—";
    [ObservableProperty] private string _selfAgeCoeff    = "—";
    [ObservableProperty] private string _selfBoundaries  = "—";
    [ObservableProperty] private string _selfGenderLabel = "Male";

    // Output — equivalent result
    [ObservableProperty] private string _equivLevelText   = "—";
    [ObservableProperty] private string _equivStars       = "";
    [ObservableProperty] private string _equivPercentile  = "—";
    [ObservableProperty] private string _equivBwMultiple  = "—";
    [ObservableProperty] private string _equivOneRepMax   = "—";
    [ObservableProperty] private string _equivGenderLabel = "Female";
    [ObservableProperty] private string _equivBodyweight  = "—";
    [ObservableProperty] private string _equivHeight      = "—";
    [ObservableProperty] private string _equivFfmi        = "—";
    [ObservableProperty] private string _equivBoundaries  = "—";
    [ObservableProperty] private string _absEquivPercentile = "—";

    // LMS research info (shown if LMS mode on)
    [ObservableProperty] private string _lmsInfo = string.Empty;

    // Status
    [ObservableProperty] private string _errorMessage = string.Empty;

    // ─────────────────────────────────────────────────────────────────────────
    // Constructor
    // ─────────────────────────────────────────────────────────────────────────
    public ResultsViewModel(SessionModel session)
    {
        _session = session;
        // Set defaults that make sense for the loaded unit
        Bodyweight   = session.Unit == Unit.Lb ? 175 : 80;
        WeightLifted = session.Unit == Unit.Lb ? 135 : 60;
        Recompute();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Property-change hooks → trigger recompute
    // ─────────────────────────────────────────────────────────────────────────
    partial void OnIsMaleChanged(bool value)          => Recompute();
    partial void OnAgeChanged(int value)              => Recompute();
    partial void OnBodyweightChanged(double value)    { if (!_isUpdating) RecomputeForwardFfmi(); Recompute(); }
    partial void OnBodyFatPctChanged(double value)    { if (!_isUpdating) RecomputeForwardFfmi(); Recompute(); }
    partial void OnWeightLiftedChanged(double value)  => Recompute();
    partial void OnRepsChanged(int value)             => Recompute();
    partial void OnUseLmsChanged(bool value)          => Recompute();
    partial void OnUseCeilingRatioChanged(bool value) => Recompute();
    partial void OnSolveForIndexChanged(int value)    => Recompute();

    partial void OnHeightInputChanged(string value)
    {
        HeightParseError = string.Empty;
        try
        {
            HeightCm = Anthropometrics.ParseHeightCm(value);
            if (!_isUpdating) RecomputeForwardFfmi();
            Recompute();
        }
        catch (FormatException ex) { HeightParseError = ex.Message; }
    }

    // When FFMI is edited directly by the user:
    partial void OnFfmiChanged(double value)
    {
        if (_isUpdating) return;
        _isUpdating = true;
        try
        {
            ApplyFfmiEdit(value);
            Recompute();
        }
        finally { _isUpdating = false; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FFMI coupling logic
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Forward: derive FFMI from weight/height/BF.</summary>
    private void RecomputeForwardFfmi()
    {
        if (HeightCm <= 0 || Bodyweight <= 0) return;
        double heightM = HeightCm / 100.0;
        double bwKg    = _session.Unit == Unit.Lb ? Anthropometrics.LbToKg(Bodyweight) : Bodyweight;
        double bf      = System.Math.Clamp(BodyFatPct, 1, 60) / 100.0;

        _isUpdating = true;
        try
        {
            Ffmi     = Anthropometrics.ComputeFfmi(bwKg, heightM, bf);
            NormFfmi = Anthropometrics.ComputeNormFfmi(Ffmi, heightM);
        }
        finally { _isUpdating = false; }
    }

    /// <summary>
    /// Inverse: a user edited FFMI directly; back-solve based on SolveForIndex.
    /// </summary>
    private void ApplyFfmiEdit(double newFfmi)
    {
        if (HeightCm <= 0) return;
        double heightM = HeightCm / 100.0;

        switch (SolveForIndex)
        {
            case 0: // Solve for bodyweight
            {
                double bf    = System.Math.Clamp(BodyFatPct, 1, 60) / 100.0;
                double lbm   = newFfmi * heightM * heightM;           // kg
                double bwKg  = lbm / (1 - bf);
                Bodyweight   = _session.Unit == Unit.Lb ? Anthropometrics.KgToLb(bwKg) : bwKg;
                break;
            }
            case 1: // Solve for body-fat %
            {
                double bwKg = _session.Unit == Unit.Lb ? Anthropometrics.LbToKg(Bodyweight) : Bodyweight;
                double lbm  = newFfmi * heightM * heightM;            // kg
                double bf   = System.Math.Clamp(1 - lbm / bwKg, 0.01, 0.6);
                BodyFatPct  = bf * 100.0;
                break;
            }
            // case 2: display only — don't back-solve
        }

        // Update normFFMI from the new FFMI
        NormFfmi = Anthropometrics.ComputeNormFfmi(newFfmi, heightM);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Main recompute
    // ─────────────────────────────────────────────────────────────────────────
    private void Recompute()
    {
        ErrorMessage = string.Empty;
        LmsInfo      = string.Empty;

        try
        {
            var selfGender  = IsMale ? Gender.Male : Gender.Female;
            var otherGender = IsMale ? Gender.Female : Gender.Male;
            SelfGenderLabel  = IsMale ? "Male"   : "Female";
            EquivGenderLabel = IsMale ? "Female" : "Male";

            var selfTable = _session.TableFor(selfGender);
            if (selfTable.ByBodyweight.Count == 0)
            {
                SetPlaceholders();
                ErrorMessage = $"No data for {selfGender} {_session.Exercise}.";
                return;
            }

            // ── Self result ────────────────────────────────────────────────
            var selfInput = new LiftInput(
                Gender:       selfGender,
                Age:          Age,
                Exercise:     _session.Exercise,
                WeightLifted: WeightLifted,
                Reps:         Reps,
                Bodyweight:   Bodyweight,
                Unit:         _session.Unit);

            var selfResult = StrengthCalculator.Calculate(selfInput, selfTable);

            // LMS research mode: compute and surface the BCCG percentile
            if (UseLms)
            {
                var lmsFit = _session.LmsFitFor(selfGender);
                if (lmsFit is not null)
                {
                    double lmsPct = lmsFit.LiftToPercentile(selfResult.AgeAdjusted1RM, Bodyweight);
                    var (l, m, s) = lmsFit.Evaluate(Bodyweight);
                    LmsInfo = $"LMS: L={l:F3} M={m:F1} S={s:F4}  →  {FormatPct(lmsPct)} (BCCG)";
                }
            }

            PopulateSelf(selfResult);

            // ── Equivalency ────────────────────────────────────────────────
            if (HeightCm <= 0 || BodyFatPct <= 0)
            {
                SetEquivPlaceholders();
                return;
            }

            double heightM = HeightCm / 100.0;
            double bwKg    = _session.Unit == Unit.Lb ? Anthropometrics.LbToKg(Bodyweight) : Bodyweight;
            double bfFrac  = System.Math.Clamp(BodyFatPct, 1, 60) / 100.0;

            double selfFfmi     = Anthropometrics.ComputeFfmi(bwKg, heightM, bfFrac);
            double selfNormFfmi = Anthropometrics.ComputeNormFfmi(selfFfmi, heightM);
            double equivHeightCm = Anthropometrics.EquivalentOppositeHeightCm(HeightCm, selfGender);
            double equivHeightM  = equivHeightCm / 100.0;

            // FFMI cross-sex equating (method selector)
            double equivNormFfmi = UseCeilingRatio
                ? Anthropometrics.NormalizeFfmiToOpposite(selfNormFfmi, selfGender)
                : Anthropometrics.ComputeNormFfmi(
                    Anthropometrics.EquipercentileFfmi(selfFfmi, selfGender), equivHeightM);

            // Reverse-engineer bodyweight
            double equivBfPct  = otherGender == Gender.Female ? 22.0 : 13.0;
            double equivBw     = Anthropometrics.ReverseBodyweight(
                equivNormFfmi, equivHeightM, equivBfPct / 100.0, _session.Unit);

            EquivHeight     = $"{equivHeightCm:F1} cm  ({CmToFeetInches(equivHeightCm)})";
            EquivBodyweight = $"{equivBw:F1} {UnitLabel}  (est. {equivBfPct:F0}% BF)";
            EquivFfmi       = $"FFMI={selfFfmi:F2} → equiv normFFMI={equivNormFfmi:F2}";

            var otherTable = _session.TableFor(otherGender);
            if (otherTable.ByBodyweight.Count == 0)
            {
                SetEquivPlaceholders();
                return;
            }

            // (a) Same absolute lift for equivalent lifter
            var absInput   = selfInput with { Gender = otherGender, Bodyweight = equivBw };
            var absResult  = StrengthCalculator.Calculate(absInput, otherTable);
            AbsEquivPercentile = $"{FormatPct(absResult.Percentile)} ({absResult.Level})";

            // (b) Percentile-equivalent lift
            double pctEquivLift = PercentileModel.PercentileToLift(selfResult.Percentile, absResult.BoundaryRow);
            var pctInput  = absInput with { WeightLifted = pctEquivLift, Reps = 1 };
            var pctResult = StrengthCalculator.Calculate(pctInput, otherTable);

            PopulateEquiv(pctResult);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Populate output properties
    // ─────────────────────────────────────────────────────────────────────────

    private void PopulateSelf(StrengthResult r)
    {
        SelfLevelText  = r.Level.ToString();
        SelfStars      = LevelToStars(r.Level);
        SelfPercentile = FormatPct(r.Percentile);
        SelfBwMultiple = $"{r.BwMultiple:F2}×";
        SelfOneRepMax  = $"{r.OneRepMax:F1} {UnitLabel}";
        SelfAgeCoeff   = $"{r.AgeCoefficient:F3}";
        SelfBoundaries = FormatBoundaries(r.BoundaryRow, UnitLabel);
    }

    private void PopulateEquiv(StrengthResult r)
    {
        EquivLevelText  = r.Level.ToString();
        EquivStars      = LevelToStars(r.Level);
        EquivPercentile = FormatPct(r.Percentile);
        EquivBwMultiple = $"{r.BwMultiple:F2}×";
        EquivOneRepMax  = $"{r.OneRepMax:F1} {UnitLabel}  (needed for same percentile)";
        EquivBoundaries = FormatBoundaries(r.BoundaryRow, UnitLabel);
    }

    private void SetPlaceholders()
    {
        SelfLevelText = SelfPercentile = SelfBwMultiple = SelfOneRepMax = SelfAgeCoeff = "—";
        SelfStars = string.Empty;
        SelfBoundaries = "—";
        SetEquivPlaceholders();
    }

    private void SetEquivPlaceholders()
    {
        EquivLevelText = EquivPercentile = EquivBwMultiple = EquivOneRepMax = "—";
        AbsEquivPercentile = EquivHeight = EquivBodyweight = EquivFfmi = "—";
        EquivStars = EquivBoundaries = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Static formatting helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string FormatPct(double pct)
    {
        if (pct >= 99.5) return ">99%";
        if (pct <= 0.5)  return "<1%";
        return $"{(int)System.Math.Round(pct)}%";
    }

    private static string LevelToStars(StrengthLevel level) => level switch
    {
        StrengthLevel.Elite        => "★★★★★",
        StrengthLevel.Advanced     => "★★★★",
        StrengthLevel.Intermediate => "★★★",
        StrengthLevel.Novice       => "★★",
        StrengthLevel.Beginner     => "★",
        _                          => "☆"
    };

    private static string FormatBoundaries(StandardsRow row, string unit)
        => $"Beg {row.Beginner:F0}  Nov {row.Novice:F0}  Int {row.Intermediate:F0}  " +
           $"Adv {row.Advanced:F0}  Elite {row.Elite:F0}  [{unit}]";

    private static string CmToFeetInches(double cm)
    {
        double totalIn = cm / 2.54;
        int feet = (int)(totalIn / 12);
        double rem = totalIn - feet * 12;
        return $"{feet}'{rem:F0}\"";
    }
}
