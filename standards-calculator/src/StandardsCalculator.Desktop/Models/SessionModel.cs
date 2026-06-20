using StandardsCalculator.Core.Math;
using StandardsCalculator.Core.Models;

namespace StandardsCalculator.Desktop.Models;

/// <summary>
/// Holds data loaded for a single exercise session (after Page 1 selection).
/// Created once on exercise selection, passed to ResultsViewModel for all calculations.
/// </summary>
public sealed class SessionModel
{
    public string   Exercise    { get; }
    public Unit     Unit        { get; }
    public string   DataDir     { get; }

    public StandardsTable MaleTable   { get; }
    public StandardsTable FemaleTable { get; }

    /// <summary>
    /// LMS surface fitted to the male table (null if too few rows to fit).
    /// Computed once on session creation for instant research-mode switching.
    /// </summary>
    public LmsModel.LmsTableFit? MaleLmsFit   { get; }
    public LmsModel.LmsTableFit? FemaleLmsFit { get; }

    public SessionModel(string exercise, Unit unit, string dataDir,
                        StandardsTable maleTable, StandardsTable femaleTable)
    {
        Exercise    = exercise;
        Unit        = unit;
        DataDir     = dataDir;
        MaleTable   = maleTable;
        FemaleTable = femaleTable;
        MaleLmsFit   = LmsModel.Fit(maleTable);
        FemaleLmsFit = LmsModel.Fit(femaleTable);
    }

    public StandardsTable TableFor(Gender gender)
        => gender == Gender.Male ? MaleTable : FemaleTable;

    public LmsModel.LmsTableFit? LmsFitFor(Gender gender)
        => gender == Gender.Male ? MaleLmsFit : FemaleLmsFit;
}
