namespace Gymers.Models;

public record WorkoutPlan(
    string Id,
    string Name,
    string TrainerId,
    string Level,
    int    SessionsPerWeek,
    int    DurationWeeks,
    string Summary,
    int    OrderRank);
