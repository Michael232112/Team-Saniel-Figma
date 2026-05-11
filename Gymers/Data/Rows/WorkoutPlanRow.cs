using SQLite;

namespace Gymers.Data.Rows;

public class WorkoutPlanRow
{
    [PrimaryKey] public string Id              { get; set; } = "";
    public string             Name            { get; set; } = "";
    public string             TrainerId       { get; set; } = "";
    public string             Level           { get; set; } = "";
    public int                SessionsPerWeek { get; set; }
    public int                DurationWeeks   { get; set; }
    public string             Summary         { get; set; } = "";
    public int                OrderRank       { get; set; }
}
