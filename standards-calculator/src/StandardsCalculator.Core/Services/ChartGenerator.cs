using StandardsCalculator.Core.Math;
using StandardsCalculator.Core.Models;

namespace StandardsCalculator.Core.Services;

/// <summary>
/// Generates a self-contained HTML file with an embedded Chart.js line chart
/// showing male and female strength curves (lift weight vs percentile) for one exercise.
///
/// Each curve is computed at the "median" bodyweight row for that sex's table
/// (middle index, approximating a typical lifter), then swept from the 1st
/// to 99th percentile using the piecewise z-mapping from PercentileModel.
///
/// The 5 published anchor points (5/20/50/80/95th percentile) are overlaid as
/// distinct scatter dots so the raw scraped data is clearly visible against
/// the smooth interpolation.
/// </summary>
public static class ChartGenerator
{
    // Percentile points to sample for the smooth curves
    private static readonly double[] CurvePercentiles =
        Enumerable.Range(1, 99).Select(i => (double)i).ToArray();

    // Anchor percentiles from the site
    private static readonly double[] AnchorPercentiles = [5, 20, 50, 80, 95];

    /// <summary>
    /// Build the chart HTML and return it as a string.
    /// </summary>
    /// <param name="male">Male standards table for the exercise.</param>
    /// <param name="female">Female standards table for the exercise.</param>
    /// <param name="unit">Display unit label.</param>
    public static string Build(StandardsTable male, StandardsTable female, Unit unit)
    {
        string unitLabel = unit == Unit.Lb ? "lb" : "kg";
        string exercise  = TitleCase(male.Exercise);

        var (maleRow, maleBw)     = MedianRow(male.ByBodyweight);
        var (femaleRow, femaleBw) = MedianRow(female.ByBodyweight);

        // Build curve datasets
        var maleCurve   = CurvePercentiles.Select(p => new { x = p, y = PercentileModel.PercentileToLift(p, maleRow)   }).ToList();
        var femaleCurve = CurvePercentiles.Select(p => new { x = p, y = PercentileModel.PercentileToLift(p, femaleRow) }).ToList();

        // Build anchor datasets (scraped boundaries)
        var maleAnchors   = AnchorPercentiles.Select(p => new { x = p, y = PercentileModel.PercentileToLift(p, maleRow)   }).ToList();
        var femaleAnchors = AnchorPercentiles.Select(p => new { x = p, y = PercentileModel.PercentileToLift(p, femaleRow) }).ToList();

        // Boundary annotation labels
        string[] levelLabels = ["Beginner (5th)", "Novice (20th)", "Intermediate (50th)", "Advanced (80th)", "Elite (95th)"];

        string mCurveJson   = ToJson(maleCurve.Select(p => (p.x, p.y)));
        string fCurveJson   = ToJson(femaleCurve.Select(p => (p.x, p.y)));
        string mAnchorJson  = ToJson(maleAnchors.Select(p => (p.x, p.y)));
        string fAnchorJson  = ToJson(femaleAnchors.Select(p => (p.x, p.y)));
        string mBoundaryJson  = ToJsonArray(maleAnchors.Select(p => p.y));
        string fBoundaryJson  = ToJsonArray(femaleAnchors.Select(p => p.y));

        double yMax = System.Math.Ceiling(maleCurve.Max(p => p.y) * 1.05 / 10) * 10;

        string scrapedAt = male.ScrapedAt.ToString("yyyy-MM-dd");

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>{{exercise}} Strength Standards</title>
  <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
      background: #0f0f0f; color: #e0e0e0;
      min-height: 100vh; padding: 24px;
    }
    h1 { font-size: 1.6rem; font-weight: 600; margin-bottom: 4px; }
    .subtitle { color: #888; font-size: 0.85rem; margin-bottom: 20px; }
    .chart-wrap { background: #1a1a1a; border-radius: 12px; padding: 24px; max-width: 960px; }
    .legend { display: flex; gap: 24px; margin-top: 16px; font-size: 0.82rem; color: #aaa; }
    .legend-item { display: flex; align-items: center; gap: 8px; }
    .swatch { width: 28px; height: 3px; border-radius: 2px; }
    .swatch.dot { width: 10px; height: 10px; border-radius: 50%; border: 2px solid; background: transparent; }
    .tables { display: flex; gap: 24px; margin-top: 20px; flex-wrap: wrap; max-width: 960px; }
    table { background: #1a1a1a; border-radius: 8px; padding: 16px; border-collapse: collapse; font-size: 0.83rem; }
    th { color: #888; font-weight: 500; padding: 4px 12px; text-align: right; border-bottom: 1px solid #333; }
    th:first-child { text-align: left; }
    td { padding: 4px 12px; text-align: right; }
    td:first-child { text-align: left; color: #aaa; }
    tr:hover td { background: #222; }
  </style>
</head>
<body>
  <h1>{{exercise}} — Strength Standards</h1>
  <p class="subtitle">
    Data scraped from strengthlevel.com on {{scrapedAt}}.
    Curves use piecewise z-interpolation through the 5 published anchor percentiles; extrapolated beyond Elite.<br>
    Male reference BW: {{maleBw:F0}} {{unitLabel}} · Female reference BW: {{femaleBw:F0}} {{unitLabel}} (median scraped row).
  </p>

  <div class="chart-wrap">
    <canvas id="chart" height="420"></canvas>
    <div class="legend">
      <div class="legend-item"><div class="swatch" style="background:#4f8ef7"></div> Male curve ({{maleBw:F0}} {{unitLabel}} BW)</div>
      <div class="legend-item"><div class="swatch dot" style="border-color:#4f8ef7"></div> Male anchor points (scraped)</div>
      <div class="legend-item"><div class="swatch" style="background:#f7674f"></div> Female curve ({{femaleBw:F0}} {{unitLabel}} BW)</div>
      <div class="legend-item"><div class="swatch dot" style="border-color:#f7674f"></div> Female anchor points (scraped)</div>
    </div>
  </div>

  <div class="tables">
    <table>
      <thead><tr>
        <th>Level (percentile)</th>
        <th>Male {{maleBw:F0}} {{unitLabel}}</th>
        <th>Female {{femaleBw:F0}} {{unitLabel}}</th>
        <th>Ratio M/F</th>
      </tr></thead>
      <tbody id="boundary-rows"></tbody>
    </table>
  </div>

  <script>
  const levelLabels = {{ToJsonStringArray(levelLabels)}};
  const mBoundaries = {{mBoundaryJson}};
  const fBoundaries = {{fBoundaryJson}};

  // Populate boundary table
  const tbody = document.getElementById('boundary-rows');
  levelLabels.forEach((lbl, i) => {
    const row = document.createElement('tr');
    const ratio = (mBoundaries[i] / fBoundaries[i]).toFixed(2);
    row.innerHTML = `<td>${lbl}</td><td>${Math.round(mBoundaries[i])}</td><td>${Math.round(fBoundaries[i])}</td><td>${ratio}×</td>`;
    tbody.appendChild(row);
  });

  const ctx = document.getElementById('chart').getContext('2d');
  new Chart(ctx, {
    type: 'scatter',
    data: {
      datasets: [
        {
          label: 'Male (curve)',
          data: {{mCurveJson}},
          type: 'line',
          borderColor: '#4f8ef7',
          backgroundColor: 'transparent',
          borderWidth: 2.5,
          pointRadius: 0,
          tension: 0.3,
          order: 2
        },
        {
          label: 'Male (anchors)',
          data: {{mAnchorJson}},
          type: 'scatter',
          borderColor: '#4f8ef7',
          backgroundColor: 'transparent',
          borderWidth: 2,
          pointRadius: 6,
          pointStyle: 'circle',
          order: 1
        },
        {
          label: 'Female (curve)',
          data: {{fCurveJson}},
          type: 'line',
          borderColor: '#f7674f',
          backgroundColor: 'transparent',
          borderWidth: 2.5,
          pointRadius: 0,
          tension: 0.3,
          order: 2
        },
        {
          label: 'Female (anchors)',
          data: {{fAnchorJson}},
          type: 'scatter',
          borderColor: '#f7674f',
          backgroundColor: 'transparent',
          borderWidth: 2,
          pointRadius: 6,
          pointStyle: 'circle',
          order: 1
        }
      ]
    },
    options: {
      responsive: true,
      animation: false,
      plugins: {
        legend: { display: false },
        tooltip: {
          callbacks: {
            label: ctx => `${ctx.dataset.label}: ${Math.round(ctx.parsed.y)} {{unitLabel}} @ ${ctx.parsed.x.toFixed(0)}th pct`
          }
        }
      },
      scales: {
        x: {
          type: 'linear',
          min: 0,
          max: 100,
          title: { display: true, text: 'Percentile', color: '#888' },
          grid: { color: '#222' },
          ticks: {
            color: '#888',
            stepSize: 10,
            callback: v => v === 0 ? '' : v + 'th'
          }
        },
        y: {
          min: 0,
          max: {{yMax}},
          title: { display: true, text: 'Lift ({{unitLabel}})', color: '#888' },
          grid: { color: '#222' },
          ticks: { color: '#888' }
        }
      }
    }
  });
  </script>
</body>
</html>
""";
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static (StandardsRow row, double bw) MedianRow(List<StandardsRow> rows)
    {
        if (rows.Count == 0) throw new InvalidOperationException("No bodyweight rows available.");
        var row = rows[rows.Count / 2];
        return (row, row.Key);
    }

    private static string ToJson(IEnumerable<(double x, double y)> points)
    {
        var items = points.Select(p => $"{{\"x\":{p.x:F1},\"y\":{p.y:F2}}}");
        return "[" + string.Join(",", items) + "]";
    }

    private static string ToJsonArray(IEnumerable<double> values)
        => "[" + string.Join(",", values.Select(v => v.ToString("F2"))) + "]";

    private static string ToJsonStringArray(IEnumerable<string> values)
        => "[" + string.Join(",", values.Select(v => $"\"{v}\"")) + "]";

    private static string TitleCase(string slug)
        => System.Globalization.CultureInfo.CurrentCulture.TextInfo
               .ToTitleCase(slug.Replace('-', ' '));
}
