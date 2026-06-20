namespace StandardsCalculator.Core.Models;

/// <summary>
/// All scraped data for one exercise + gender + unit combination.
/// ByBodyweight rows keyed by bodyweight, ByAge rows keyed by age.
/// </summary>
public class StandardsTable
{
    public string   Exercise    { get; set; } = "";
    public Gender   Gender      { get; set; }
    public Unit     Unit        { get; set; }
    public DateTime ScrapedAt   { get; set; }

    /// <summary>Rows sorted ascending by bodyweight.</summary>
    public List<StandardsRow> ByBodyweight { get; set; } = [];

    /// <summary>Rows sorted ascending by age.</summary>
    public List<StandardsRow> ByAge        { get; set; } = [];
}
