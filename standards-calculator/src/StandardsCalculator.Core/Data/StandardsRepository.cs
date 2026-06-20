using StandardsCalculator.Core.Models;
using StandardsCalculator.Core.Scraping;
using System.Text.Json;

namespace StandardsCalculator.Core.Data;

/// <summary>
/// Loads and caches StandardsTable objects from the local data/standards/ directory.
/// Falls back to on-demand scraping if a file is missing.
/// </summary>
public class StandardsRepository
{
    private readonly string _dataDir;
    private readonly Dictionary<string, StandardsTable> _cache = [];

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public StandardsRepository(string dataDir)
    {
        _dataDir = dataDir;
    }

    /// <summary>
    /// Retrieve the standards table for the given exercise, gender, and unit.
    /// Loads from disk if available; scrapes live if not (unless scraping is disabled).
    /// </summary>
    public async Task<StandardsTable> GetAsync(
        string exercise, Gender gender, Unit unit,
        bool allowScrape = true,
        CancellationToken ct = default)
    {
        string key     = CacheKey(exercise, gender, unit);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        string path = FilePath(exercise, gender, unit);
        if (File.Exists(path))
        {
            var table = Load(path);
            _cache[key] = table;
            return table;
        }

        if (!allowScrape)
            throw new FileNotFoundException(
                $"Standards data not found for '{exercise}' ({gender}, {unit}). " +
                $"Run with --scrape {exercise} to download it.", path);

        // Scrape on demand
        Console.WriteLine($"[Data] No local data for '{exercise}'. Scraping now…");
        var scraper = new StandardsScraper(_dataDir);
        await scraper.ScrapeAndSaveAsync(exercise, unit, ct);

        // Reload
        var loaded = Load(path);
        _cache[key] = loaded;
        return loaded;
    }

    /// <summary>Check if local data exists for an exercise.</summary>
    public bool HasLocal(string exercise, Gender gender, Unit unit)
        => File.Exists(FilePath(exercise, gender, unit));

    /// <summary>List all locally available exercise slugs (any gender/unit).</summary>
    public IEnumerable<string> AvailableExercises()
    {
        if (!Directory.Exists(_dataDir)) return [];
        return Directory.GetFiles(_dataDir, "*.json")
                        .Select(p => Path.GetFileName(p).Split('.')[0])
                        .Distinct()
                        .OrderBy(s => s);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private StandardsTable Load(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<StandardsTable>(json, JsonOpts)
            ?? throw new InvalidDataException($"Could not deserialize {path}");
    }

    private string FilePath(string exercise, Gender gender, Unit unit)
    {
        string g = gender == Gender.Male ? "m" : "f";
        string u = unit   == Unit.Lb     ? "lb" : "kg";
        return Path.Combine(_dataDir, $"{exercise}.{u}.{g}.json");
    }

    private static string CacheKey(string exercise, Gender gender, Unit unit)
        => $"{exercise}|{gender}|{unit}";
}
