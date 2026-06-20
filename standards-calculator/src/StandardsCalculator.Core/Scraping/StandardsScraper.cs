using AngleSharp;
using AngleSharp.Dom;
using StandardsCalculator.Core.Models;
using System.Text.Json;

namespace StandardsCalculator.Core.Scraping;

/// <summary>
/// Scrapes strengthlevel.com/strength-standards/{exercise}/{unit} and extracts
/// the by-bodyweight and by-age tables for both Male and Female.
///
/// Usage:
///   var scraper = new StandardsScraper(dataDir);
///   await scraper.ScrapeAndSaveAsync("deadlift", Unit.Lb);
///   await scraper.ScrapeAllAsync(Unit.Lb);
/// </summary>
public class StandardsScraper
{
    private readonly string _dataDir;
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters    = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public StandardsScraper(string dataDir, HttpClient? http = null)
    {
        _dataDir = dataDir;
        Directory.CreateDirectory(dataDir);

        _http = http ?? new HttpClient();
        // Polite browser-like headers to reduce chance of bot detection
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Scrape one exercise, save male+female JSON files.</summary>
    public async Task ScrapeAndSaveAsync(string exercise, Unit unit,
                                          CancellationToken ct = default)
    {
        string unitStr = unit == Unit.Lb ? "lb" : "kg";
        string url = $"https://strengthlevel.com/strength-standards/{exercise}/{unitStr}";

        Console.WriteLine($"Scraping: {url}");
        string html = await _http.GetStringAsync(url, ct);

        var (male, female) = ParsePage(html, exercise, unit);

        await SaveAsync(male,   exercise, Gender.Male,   unit, ct);
        await SaveAsync(female, exercise, Gender.Female, unit, ct);

        Console.WriteLine($"  ✓ {exercise}.{unitStr}: {male.ByBodyweight.Count} BW rows (M), {female.ByBodyweight.Count} BW rows (F)");
    }

    /// <summary>Scrape all known exercises. Politely throttled at ~1 req/sec.</summary>
    public async Task ScrapeAllAsync(Unit unit, CancellationToken ct = default)
    {
        foreach (string exercise in KnownExercises)
        {
            try
            {
                await ScrapeAndSaveAsync(exercise, unit, ct);
                await Task.Delay(1100, ct); // ~1 req/sec
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ {exercise}: {ex.Message}");
            }
        }
    }

    // ── Parsing ──────────────────────────────────────────────────────────────

    internal static (StandardsTable Male, StandardsTable Female) ParsePage(
        string html, string exercise, Unit unit)
    {
        using var context = BrowsingContext.New(Configuration.Default);
        using var doc     = context.OpenAsync(req => req.Content(html)).GetAwaiter().GetResult();

        // The page has: male tables, then female tables.
        // Each section has a by-bodyweight table and a by-age table.
        // We identify them by their <thead> column headers.
        var tables = doc.QuerySelectorAll("table").ToList();

        var maleBw   = new List<StandardsRow>();
        var maleAge  = new List<StandardsRow>();
        var femBw    = new List<StandardsRow>();
        var femAge   = new List<StandardsRow>();

        // Heuristic: tables with a "BW" or "Age" column in their first header cell
        // We collect all 5-column standards tables in document order, assigning them
        // male-BW, male-age, female-BW, female-age.
        var standardsTables = tables
            .Where(IsStandardsTable)
            .ToList();

        // Assign in order: [0]=male BW, [1]=male age, [2]=female BW, [3]=female age
        // (some exercises may have fewer age rows; tolerate missing tables)
        if (standardsTables.Count >= 1) maleBw  = ParseRows(standardsTables[0]);
        if (standardsTables.Count >= 2) maleAge = ParseRows(standardsTables[1]);
        if (standardsTables.Count >= 3) femBw   = ParseRows(standardsTables[2]);
        if (standardsTables.Count >= 4) femAge  = ParseRows(standardsTables[3]);

        var male = new StandardsTable
        {
            Exercise    = exercise,
            Gender      = Gender.Male,
            Unit        = unit,
            ScrapedAt   = DateTime.UtcNow,
            ByBodyweight = maleBw,
            ByAge        = maleAge
        };
        var female = new StandardsTable
        {
            Exercise    = exercise,
            Gender      = Gender.Female,
            Unit        = unit,
            ScrapedAt   = DateTime.UtcNow,
            ByBodyweight = femBw,
            ByAge        = femAge
        };
        return (male, female);
    }

    private static bool IsStandardsTable(IElement table)
    {
        // A standards table has exactly 6 columns (BW/Age + 5 levels)
        var ths = table.QuerySelectorAll("thead th, thead td").ToList();
        return ths.Count == 6;
    }

    private static List<StandardsRow> ParseRows(IElement table)
    {
        var rows = new List<StandardsRow>();
        foreach (var tr in table.QuerySelectorAll("tbody tr"))
        {
            var cells = tr.QuerySelectorAll("td").ToList();
            if (cells.Count < 6) continue;

            // First cell is BW or age; remaining 5 are the levels
            if (!TryParseNum(cells[0].TextContent, out double key)) continue;
            if (!TryParseNum(cells[1].TextContent, out double beg)) continue;
            if (!TryParseNum(cells[2].TextContent, out double nov)) continue;
            if (!TryParseNum(cells[3].TextContent, out double mid)) continue;
            if (!TryParseNum(cells[4].TextContent, out double adv)) continue;
            if (!TryParseNum(cells[5].TextContent, out double eli)) continue;

            rows.Add(new StandardsRow(key, beg, nov, mid, adv, eli));
        }
        return rows;
    }

    private static bool TryParseNum(string text, out double value)
    {
        text = text.Trim().Replace(",", "");
        return double.TryParse(text, System.Globalization.NumberStyles.Any,
                               System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    // ── Persistence ──────────────────────────────────────────────────────────

    private async Task SaveAsync(StandardsTable table, string exercise,
                                  Gender gender, Unit unit, CancellationToken ct)
    {
        string genderStr = gender == Gender.Male ? "m" : "f";
        string unitStr   = unit   == Unit.Lb     ? "lb" : "kg";
        string path      = Path.Combine(_dataDir, $"{exercise}.{unitStr}.{genderStr}.json");
        string json      = JsonSerializer.Serialize(table, JsonOpts);
        await File.WriteAllTextAsync(path, json, ct);
    }

    // ── Exercise list ────────────────────────────────────────────────────────

    public static readonly string[] KnownExercises =
    [
        // Barbell
        "bench-press", "squat", "deadlift", "shoulder-press", "barbell-curl",
        "bent-over-row", "incline-bench-press", "front-squat", "hex-bar-deadlift",
        "hip-thrust", "romanian-deadlift", "power-clean", "military-press",
        "sumo-deadlift", "clean-and-jerk", "ez-bar-curl", "lying-tricep-extension",
        "close-grip-bench-press", "snatch", "preacher-curl", "seated-shoulder-press",
        "barbell-shrug", "t-bar-row", "clean", "push-press",
        "smith-machine-bench-press", "decline-bench-press",
        // Bodyweight
        "pull-ups", "push-ups", "dips", "chin-ups", "crunches", "sit-ups",
        "muscle-ups", "bodyweight-squat", "one-arm-push-ups",
        "neutral-grip-pull-ups", "diamond-push-ups",
        // Dumbbell
        "dumbbell-bench-press", "dumbbell-curl", "incline-dumbbell-bench-press",
        "dumbbell-shoulder-press", "dumbbell-lateral-raise", "dumbbell-row",
        "hammer-curl", "seated-dumbbell-shoulder-press",
        "dumbbell-bulgarian-split-squat", "goblet-squat", "dumbbell-fly",
        "dumbbell-shrug",
        // Machine / Cable
        "sled-leg-press", "leg-extension", "horizontal-leg-press", "chest-press",
        "hack-squat", "machine-shoulder-press", "machine-chest-fly",
        "seated-leg-curl", "lying-leg-curl", "machine-calf-raise",
        "hip-adduction", "lat-pulldown", "tricep-pushdown", "seated-cable-row"
    ];
}
