using SQLite;

namespace Gymers.Data.Rows;

public class TrainerRow
{
    [PrimaryKey] public string Id                { get; set; } = "";
    public string             Name              { get; set; } = "";
    public string             Title             { get; set; } = "";
    public string             RatingText        { get; set; } = "";
    public int                SessionsCompleted { get; set; }
}
