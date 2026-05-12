using SQLite;

namespace Gymers.Data.Rows;

public class EquipmentRow
{
    [PrimaryKey] public string Id { get; set; } = "";
    public string Name      { get; set; } = "";
    public string Category  { get; set; } = "";
    public string Status    { get; set; } = "";
    public string Location  { get; set; } = "";
    public int    OrderRank { get; set; }
}
