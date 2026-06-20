using AngleSharp;
using StandardsCalculator.Core.Models;
using StandardsCalculator.Core.Scraping;

namespace StandardsCalculator.Core.Services;

/// <summary>
/// Hybrid verification: scrapes the live strengthlevel.com calculator page for a
/// given input and compares its output to the local calculation.
///
/// NOTE: strengthlevel.com does not expose a public JSON API. This verifier fetches
/// the results page and parses the rendered HTML. It is inherently fragile and is
/// provided as a best-effort cross-check, not a primary data path.
/// </summary>
public class LiveVerifier
{
    private readonly HttpClient _http;

    public LiveVerifier(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    /// <summary>
    /// Fetch the live result for the given inputs, return a diff report.
    /// Returns null and a descriptive error if the page could not be parsed.
    /// </summary>
    public async Task<LiveVerifyReport> VerifyAsync(LiftInput input,
                                                     StrengthResult localResult,
                                                     CancellationToken ct = default)
    {
        string unit   = input.Unit == Unit.Lb ? "lb" : "kg";
        string gender = input.Gender == Gender.Male ? "male" : "female";

        // Build the calculator URL — strengthlevel uses query params on the exercise page
        string url = $"https://strengthlevel.com/strength-standards/{input.Exercise}/{unit}" +
                     $"?gender={gender}" +
                     $"&bodyweight={input.Bodyweight:F0}" +
                     $"&unit={unit}" +
                     $"&age={input.Age}" +
                     $"&lift={input.WeightLifted:F0}" +
                     $"&reps={input.Reps}";

        try
        {
            string html = await _http.GetStringAsync(url, ct);

            using var ctx = BrowsingContext.New(Configuration.Default);
            using var doc = await ctx.OpenAsync(req => req.Content(html), ct);

            // Try to parse key output values from the rendered page
            // (selectors are best-effort; the page may render differently server-side)
            string? levelText      = doc.QuerySelector(".strength-level-title, .level-name, h2.level")?.TextContent?.Trim();
            string? percentileText = doc.QuerySelector(".percentile-value, .stronger-than")?.TextContent?.Trim();

            return new LiveVerifyReport
            {
                Url              = url,
                LocalLevel       = localResult.Level.ToString(),
                LocalPercentile  = localResult.Percentile,
                LiveLevelText    = levelText   ?? "(could not parse)",
                LivePercentileText = percentileText ?? "(could not parse)",
                RawHtmlLength    = html.Length,
                Success          = levelText != null || percentileText != null
            };
        }
        catch (Exception ex)
        {
            return new LiveVerifyReport
            {
                Url     = url,
                Error   = ex.Message,
                Success = false
            };
        }
    }
}

public class LiveVerifyReport
{
    public string  Url                 { get; init; } = "";
    public string  LocalLevel          { get; init; } = "";
    public double  LocalPercentile     { get; init; }
    public string  LiveLevelText       { get; init; } = "";
    public string  LivePercentileText  { get; init; } = "";
    public int     RawHtmlLength       { get; init; }
    public string? Error               { get; init; }
    public bool    Success             { get; init; }
}
