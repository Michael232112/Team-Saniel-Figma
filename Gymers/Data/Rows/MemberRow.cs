using SQLite;

namespace Gymers.Data.Rows;

public class MemberRow
{
    [PrimaryKey] public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; }
    public string Status { get; set; } = "";
    public string ExpiresIso { get; set; } = "";
}
