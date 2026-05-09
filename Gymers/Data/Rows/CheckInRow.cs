using SQLite;

namespace Gymers.Data.Rows;

public class CheckInRow
{
    [PrimaryKey] public int Id { get; set; }
    public string MemberId { get; set; } = "";
    public long AtTicks { get; set; }
}
