namespace StandardsCalculator.Core.Models;

/// <summary>Extra anthropometric inputs for Phase-2 equivalency.</summary>
public record AnthroProfile(
    double HeightCm,      // parsed from any format: "6'1", "6-1", "185.4", etc.
    double BodyFatPct     // 0–100 (e.g. 13.5)
);
