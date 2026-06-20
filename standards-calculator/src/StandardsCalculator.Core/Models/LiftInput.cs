namespace StandardsCalculator.Core.Models;

/// <summary>All user inputs required for the core strength calculation.</summary>
public record LiftInput(
    Gender Gender,
    int    Age,
    string Exercise,       // slug matching the standards data key, e.g. "deadlift"
    double WeightLifted,   // in the chosen unit
    int    Reps,
    double Bodyweight,     // in the chosen unit
    Unit   Unit
);
