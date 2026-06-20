using StandardsCalculator.Core.Data;
using StandardsCalculator.Core.Math;
using StandardsCalculator.Core.Models;
using StandardsCalculator.Core.Scraping;
using StandardsCalculator.Core.Services;

// ── Resolve data directory (relative to the executable or repo root) ──────────
string exeDir  = AppContext.BaseDirectory;
string dataDir = FindDataDir(exeDir);
var repo       = new StandardsRepository(dataDir);
var calc       = new StrengthCalculator(repo);
var equivCalc  = new EquivalencyCalculator(calc);

// ── CLI argument handling ─────────────────────────────────────────────────────
// `args` is already the implicit top-level args parameter (string[])
if (args.Contains("--scrape"))
{
    int idx      = Array.IndexOf(args, "--scrape");
    string? what = idx + 1 < args.Length && !args[idx + 1].StartsWith('-')
        ? args[idx + 1] : "all";
    string unitArg  = args.Contains("--unit") && Array.IndexOf(args, "--unit") + 1 < args.Length
        ? args[Array.IndexOf(args, "--unit") + 1].ToLower() : "lb";
    var scrapeUnit = unitArg == "kg" ? Unit.Kg : Unit.Lb;
    var scraper = new StandardsScraper(dataDir);

    if (what == "all")
    {
        Console.WriteLine($"Scraping all exercises ({unitArg})… this takes about 90 seconds.");
        await scraper.ScrapeAllAsync(scrapeUnit);
    }
    else
    {
        await scraper.ScrapeAndSaveAsync(what, scrapeUnit);
    }
    Console.WriteLine("Done.");
    return;
}

// ── Graph mode ────────────────────────────────────────────────────────────────
if (args.Contains("--graph"))
{
    int idx       = Array.IndexOf(args, "--graph");
    string graphEx  = idx + 1 < args.Length && !args[idx + 1].StartsWith('-')
        ? args[idx + 1].ToLower().Replace(' ', '-') : "deadlift";
    string graphUnitArg = args.Contains("--unit") && Array.IndexOf(args, "--unit") + 1 < args.Length
        ? args[Array.IndexOf(args, "--unit") + 1].ToLower() : "lb";
    var graphUnit  = graphUnitArg == "kg" ? Unit.Kg : Unit.Lb;

    Console.WriteLine($"Generating chart for: {graphEx} ({graphUnitArg})");
    var maleTable   = await repo.GetAsync(graphEx, Gender.Male,   graphUnit);
    var femaleTable = await repo.GetAsync(graphEx, Gender.Female, graphUnit);

    if (maleTable.ByBodyweight.Count == 0 || femaleTable.ByBodyweight.Count == 0)
    {
        Console.WriteLine("Error: no bodyweight rows found for this exercise.");
        Console.WriteLine("This may be a reps-based exercise (pull-ups, etc.) not yet supported.");
        return;
    }

    string html     = ChartGenerator.Build(maleTable, femaleTable, graphUnit);
    string chartsDir = Path.Combine(Directory.GetParent(dataDir)?.FullName ?? ".", "charts");
    Directory.CreateDirectory(chartsDir);
    string outPath  = Path.Combine(chartsDir, $"{graphEx}.{graphUnitArg}.html");
    await File.WriteAllTextAsync(outPath, html);

    Console.WriteLine($"Chart saved: {outPath}");

    // Open in default browser (macOS: open, Windows: start, Linux: xdg-open)
    try
    {
        string opener = OperatingSystem.IsMacOS()   ? "open"      :
                        OperatingSystem.IsWindows()  ? "start"     : "xdg-open";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = opener,
            Arguments       = outPath,
            UseShellExecute = true
        });
    }
    catch { Console.WriteLine("(Could not auto-open browser — open the file manually.)"); }
    return;
}

// ── Diagnostics mode ──────────────────────────────────────────────────────────
if (args.Contains("--diagnostics"))
{
    int diagIdx  = Array.IndexOf(args, "--diagnostics");
    string diagEx = diagIdx + 1 < args.Length && !args[diagIdx + 1].StartsWith('-')
        ? args[diagIdx + 1].ToLower().Replace(' ', '-') : "deadlift";
    string diagUnitArg = args.Contains("--unit") && Array.IndexOf(args, "--unit") + 1 < args.Length
        ? args[Array.IndexOf(args, "--unit") + 1].ToLower() : "lb";
    var diagUnit = diagUnitArg == "kg" ? Unit.Kg : Unit.Lb;

    Console.WriteLine($"\n── LMS/PCHIP Diagnostics: {diagEx} ({diagUnitArg}) ──────────────────\n");

    StandardsTable? maleTab = null, femTab = null;
    try { maleTab = await repo.GetAsync(diagEx, Gender.Male,   diagUnit); } catch { }
    try { femTab  = await repo.GetAsync(diagEx, Gender.Female, diagUnit); } catch { }

    void PrintLmsSurface(StandardsTable? tab, string label)
    {
        if (tab is null || tab.ByBodyweight.Count < 2)
        {
            Console.WriteLine($"  {label}: no data");
            return;
        }
        var surface = LmsModel.Fit(tab);
        if (surface is null) { Console.WriteLine($"  {label}: fit failed"); return; }

        Console.WriteLine($"  {label} — per-bodyweight LMS fit (PCHIP passes exactly through all anchors):");
        Console.WriteLine($"  {"BW",-6} {"L",8} {"M",8} {"S",8} {"RMSE",8}  (L=Box-Cox power; RMSE=z-score residual at 5 anchors)");
        foreach (var fit in surface.RowFits)
            Console.WriteLine($"  {fit.Bodyweight,-6:F0} {fit.L,8:F4} {fit.M,8:F1} {fit.S,8:F4} {fit.FitRmse,8:F4}");
        Console.WriteLine($"  Overall RMSE: {surface.OverallRmse:F4}  (near 0 = distribution almost normal; L≈0 = lognormal)");
        Console.WriteLine();
    }

    PrintLmsSurface(maleTab,  "Male");
    PrintLmsSurface(femTab,   "Female");

    // Age-adjustment diagnostic
    Console.WriteLine("  Age-adjustment multiplicative assumption diagnostic (Male table):");
    if (maleTab?.ByAge.Count >= 2)
    {
        foreach (int testAge in new[] { 16, 20, 25, 30, 40, 50, 60 })
        {
            var diag = AgeAdjustment.ComputeDiagnostic(maleTab.ByAge, testAge);
            Console.WriteLine($"    age {testAge,2}: {diag.Summary()}");
        }
    }
    else Console.WriteLine("  (no age data)");

    Console.WriteLine();
    return;
}

bool verifyLive = args.Contains("--verify-live");

// ── Interactive flow ──────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════╗");
Console.WriteLine("║       Strength Standards Calculator                  ║");
Console.WriteLine("║  Data: strengthlevel.com  |  Math: see methods.md   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");
Console.WriteLine();

// Gender
Gender gender = PromptEnum<Gender>(
    "Gender [M/F]: ",
    s => s.Trim().ToUpper() switch
    {
        "M" or "MALE"   => Gender.Male,
        "F" or "FEMALE" => Gender.Female,
        _               => throw new FormatException("Enter M or F")
    });

// Age
int age = PromptInt("Age: ", 10, 120);

// Unit
string unitStr = PromptChoice("Unit [lb/kg, default lb]: ", ["lb", "kg", ""], "lb");
Unit unit      = unitStr == "kg" ? Unit.Kg : Unit.Lb;
string unitLabel = unit == Unit.Lb ? "lb" : "kg";

// Exercise
Console.WriteLine("Available exercises (local data):");
var available = repo.AvailableExercises().ToList();
if (available.Count == 0)
{
    Console.WriteLine("  (none — run: dotnet run -- --scrape deadlift)");
}
else
{
    for (int i = 0; i < available.Count; i++)
    {
        Console.Write($"  {available[i],-28}");
        if ((i + 1) % 3 == 0) Console.WriteLine();
    }
    if (available.Count % 3 != 0) Console.WriteLine();
}
Console.WriteLine();

string exercise = PromptString(
    "Exercise (slug, e.g. deadlift): ",
    s => s.Trim().ToLower().Replace(' ', '-'),
    "deadlift");

// Weight lifted
double weightLifted = PromptDouble($"Weight lifted ({unitLabel}): ", 1, 2000);

// Reps
int reps = PromptInt("Repetitions [default 1]: ", 1, 100, defaultVal: 1);

// Bodyweight
double bodyweight = PromptDouble($"Your bodyweight ({unitLabel}): ", 50, 600);

// ── Body composition (optional — for gender equivalency) ──────────────────────
Console.WriteLine();
Console.WriteLine("Body composition for gender equivalency (Enter to skip):");
string? heightInput = PromptOptional("  Height (e.g. 6'1, 6-1, 185, 1.85): ");
string? bfInput     = null;
if (!string.IsNullOrWhiteSpace(heightInput))
    bfInput = PromptOptional("  Body-fat %  (e.g. 13.5): ");

// Parse optional composition inputs now so we can bail early on bad input
double heightCm = 0, bodyFatPct = 0;
bool doEquivalency = !string.IsNullOrWhiteSpace(heightInput) && !string.IsNullOrWhiteSpace(bfInput);
if (doEquivalency)
{
    try
    {
        heightCm   = Anthropometrics.ParseHeightCm(heightInput!);
        bodyFatPct = double.Parse(bfInput!.Trim(),
                                  System.Globalization.CultureInfo.InvariantCulture);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ✗ Could not parse body composition: {ex.Message}");
        doEquivalency = false;
    }
}

// ── Calculate ─────────────────────────────────────────────────────────────────
Console.WriteLine();
StrengthResult result;
try
{
    result = await calc.CalculateAsync(new LiftInput(
        Gender:       gender,
        Age:          age,
        Exercise:     exercise,
        WeightLifted: weightLifted,
        Reps:         reps,
        Bodyweight:   bodyweight,
        Unit:         unit));
}
catch (FileNotFoundException ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine("Run: dotnet run -- --scrape all");
    return;
}

// ── Output ────────────────────────────────────────────────────────────────────
PrintStrengthResult(result, unitLabel, exercise);

// ── Live verification (optional) ─────────────────────────────────────────────
if (verifyLive)
{
    Console.WriteLine();
    Console.WriteLine("── Live Verification ────────────────────────────────");
    var verifier = new LiveVerifier();
    var report   = await verifier.VerifyAsync(result.Input, result);
    if (report.Success)
    {
        Console.WriteLine($"  Local level:       {report.LocalLevel}");
        Console.WriteLine($"  Local percentile:  {report.LocalPercentile:F1}%");
        Console.WriteLine($"  Live level:        {report.LiveLevelText}");
        Console.WriteLine($"  Live percentile:   {report.LivePercentileText}");
        Console.WriteLine($"  URL: {report.Url}");
    }
    else
    {
        Console.WriteLine($"  Could not parse live result: {report.Error ?? report.LiveLevelText}");
        Console.WriteLine($"  Verify manually: {report.Url}");
    }
}

// ── Phase-2 equivalency ───────────────────────────────────────────────────────
if (!doEquivalency) return;

EquivalencyResult equiv;
try
{
    equiv = await equivCalc.CalculateAsync(result, new AnthroProfile(heightCm, bodyFatPct));
}
catch (Exception ex)
{
    Console.WriteLine($"Equivalency calculation failed: {ex.Message}");
    return;
}

PrintEquivalencyResult(equiv, result, unitLabel);

// ═════════════════════════════════════════════════════════════════════════════
// Helper methods
// ═════════════════════════════════════════════════════════════════════════════

static void PrintStrengthResult(StrengthResult r, string unitLabel, string exercise,
                                 string? headerSuffix = null)
{
    string stars = r.Level switch
    {
        StrengthLevel.Elite        => "★★★★★",
        StrengthLevel.Advanced     => "★★★★",
        StrengthLevel.Intermediate => "★★★",
        StrengthLevel.Novice       => "★★",
        StrengthLevel.Beginner     => "★",
        _                          => "☆"
    };

    string genderStr = r.Input.Gender == Gender.Male ? "male" : "female";
    string exDisplay = System.Globalization.CultureInfo.CurrentCulture.TextInfo
                           .ToTitleCase(exercise.Replace('-', ' '));
    string title = headerSuffix is null
        ? $"══ {exDisplay} Results ═══════════════════════════════════════"
        : $"══ {exDisplay} Results ({headerSuffix}) ══════════════════════";

    Console.WriteLine();
    Console.WriteLine(title);
    Console.WriteLine();
    Console.WriteLine($"  Strength Level:  {r.Level}");
    Console.WriteLine($"  {stars}");
    Console.WriteLine();
    Console.WriteLine($"  Stronger than {Pct(r.Percentile)} of {genderStr} lifters");
    Console.WriteLine($"  at age {r.Input.Age}, weighing {r.Input.Bodyweight:F0} {unitLabel}.");
    Console.WriteLine();
    Console.WriteLine($"  1RM (Epley):          {r.OneRepMax:F1} {unitLabel}");
    Console.WriteLine($"  Bodyweight multiple:  {r.BwMultiple:F2}×");
    Console.WriteLine($"  Age coefficient:      {r.AgeCoefficient:F3}  (1.000 = at peak age)");
    Console.WriteLine();
    Console.WriteLine("  How we calculated it:");
    Console.WriteLine($"  Step 1 – Compare vs all same-gender lifters:");
    Console.WriteLine($"           stronger than {Pct(r.UnadjustedPercentile)}");
    Console.WriteLine($"  Step 2 – After age adjustment (×{r.AgeCoefficient:F3}):");
    Console.WriteLine($"           stronger than {Pct(r.AgeAdjustedPercentile)}");
    Console.WriteLine($"  Step 3 – Bodyweight-relative (final):");
    Console.WriteLine($"           stronger than {Pct(r.Percentile)}");
    Console.WriteLine();
    Console.WriteLine($"  Strength Level Boundaries at {r.Input.Bodyweight:F0} {unitLabel} bodyweight:");
    Console.WriteLine($"  {"BW",-6} {"Beg.",-8} {"Nov.",-8} {"Int.",-8} {"Adv.",-8} {"Elite",-8}");
    Console.WriteLine($"  {r.BoundaryRow.Key,-6:F0} " +
                      $"{r.BoundaryRow.Beginner,-8:F0} " +
                      $"{r.BoundaryRow.Novice,-8:F0} " +
                      $"{r.BoundaryRow.Intermediate,-8:F0} " +
                      $"{r.BoundaryRow.Advanced,-8:F0} " +
                      $"{r.BoundaryRow.Elite,-8:F0}");
}

static void PrintEquivalencyResult(EquivalencyResult eq, StrengthResult sr, string unitLabel)
{
    string selfGender  = sr.Input.Gender == Gender.Male ? "Male"   : "Female";
    string otherGender = sr.Input.Gender == Gender.Male ? "Female" : "Male";
    string equivHeight = CmToFeetInches(eq.EquivHeightCm);

    Console.WriteLine();
    Console.WriteLine($"══ {otherGender} Equivalency ═══════════════════════════════════");
    Console.WriteLine();
    Console.WriteLine($"  ── Your composition ({selfGender}) ─────────────────────────");
    Console.WriteLine($"  FFMI:              {eq.SelfFfmi:F2}");
    Console.WriteLine($"  Normalized FFMI:   {eq.SelfNormFfmi:F2}");
    Console.WriteLine($"  Height percentile: {Pct(eq.SelfHeightPercentile)}");
    Console.WriteLine();
    Console.WriteLine($"  ── Equivalent {otherGender} lifter ──────────────────────────");
    Console.WriteLine($"  Height:            {eq.EquivHeightCm:F1} cm  ({equivHeight})");
    Console.WriteLine($"  Height percentile: {Pct(eq.EquivHeightPercentile)}");
    Console.WriteLine($"  Normalized FFMI:   {eq.EquivNormFfmi:F2}");
    Console.WriteLine($"  Bodyweight:        {eq.EquivBodyweight:F1} {unitLabel}  " +
                      $"(assumed {eq.EquivAssumedBodyFatPct:F0}% body fat)");

    if (eq.AbsoluteEquivalent is { } abs)
    {
        string exDisplay = System.Globalization.CultureInfo.CurrentCulture.TextInfo
                               .ToTitleCase(sr.Input.Exercise.Replace('-', ' '));
        Console.WriteLine();
        Console.WriteLine($"  ── Same absolute lift ({sr.Input.WeightLifted:F0} {unitLabel}) for the equiv. {otherGender.ToLower()} ─");
        Console.WriteLine($"  Level:       {abs.Level}");
        Console.WriteLine($"  Percentile:  {Pct(abs.Percentile)}");

        if (eq.PercentileEquivalent is { } pctEquiv)
        {
            // Full result block for the equivalent lifter at the percentile-matched lift
            string suffix = $"Equivalent {otherGender}";
            PrintStrengthResult(pctEquiv, unitLabel, sr.Input.Exercise, suffix);
        }
    }
    else
    {
        Console.WriteLine();
        Console.WriteLine($"  (No {otherGender.ToLower()} data for '{sr.Input.Exercise}'. Run --scrape all.)");
    }

    Console.WriteLine();
    Console.WriteLine("  See methods.md for all formulas and assumptions.");
}

/// <summary>Format a percentile value as a percentage string, e.g. "82%" or ">99.9%".</summary>
static string Pct(double pct)
{
    if (pct >= 99.5) return ">99%";   // anything that rounds to 100 displayed as >99%
    if (pct <= 0.5)  return "<1%";
    return $"{(int)System.Math.Round(pct)}%";
}


static string CmToFeetInches(double cm)
{
    double totalInches = cm / 2.54;
    int    feet        = (int)(totalInches / 12);
    double rem         = totalInches - feet * 12;
    return $"{feet}'{rem:F0}\"";
}

// ── Prompt helpers ────────────────────────────────────────────────────────────

static T PromptEnum<T>(string prompt, Func<string, T> parser) where T : struct
{
    while (true)
    {
        Console.Write(prompt);
        string? raw = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(raw)) continue;
        try   { return parser(raw); }
        catch (FormatException ex) { Console.WriteLine($"  ✗ {ex.Message}"); }
    }
}

static int PromptInt(string prompt, int min, int max, int? defaultVal = null)
{
    while (true)
    {
        Console.Write(prompt);
        string? raw = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(raw) && defaultVal.HasValue) return defaultVal.Value;
        if (int.TryParse(raw, out int v) && v >= min && v <= max)  return v;
        Console.WriteLine($"  ✗ Enter a number between {min} and {max}.");
    }
}

static double PromptDouble(string prompt, double min, double max)
{
    while (true)
    {
        Console.Write(prompt);
        string? raw = Console.ReadLine()?.Trim();
        if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out double v)
            && v >= min && v <= max)
            return v;
        Console.WriteLine($"  ✗ Enter a number between {min} and {max}.");
    }
}

static string PromptChoice(string prompt, string[] choices, string defaultVal)
{
    while (true)
    {
        Console.Write(prompt);
        string? raw = Console.ReadLine()?.Trim().ToLower();
        if (string.IsNullOrEmpty(raw))      return defaultVal;
        if (choices.Contains(raw))          return raw;
        Console.WriteLine($"  ✗ Choose: {string.Join(", ", choices.Where(c => c.Length > 0))}.");
    }
}

static string PromptString(string prompt, Func<string, string> normalise, string defaultVal)
{
    while (true)
    {
        Console.Write(prompt);
        string? raw = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(raw)) return defaultVal;
        string v = normalise(raw);
        if (!string.IsNullOrEmpty(v))       return v;
    }
}

static string? PromptOptional(string prompt)
{
    Console.Write(prompt);
    string? raw = Console.ReadLine()?.Trim();
    return string.IsNullOrWhiteSpace(raw) ? null : raw;
}

static string FindDataDir(string start)
{
    string? dir = start;
    while (dir is not null)
    {
        string candidate = Path.Combine(dir, "data", "standards");
        if (Directory.Exists(candidate)) return candidate;
        if (File.Exists(Path.Combine(dir, "StandardsCalculator.sln")))
            return Path.Combine(dir, "data", "standards");
        dir = Directory.GetParent(dir)?.FullName;
    }
    return Path.Combine(start, "data", "standards");
}
