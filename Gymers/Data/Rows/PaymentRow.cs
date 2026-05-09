using SQLite;

namespace Gymers.Data.Rows;

public class PaymentRow
{
    [PrimaryKey] public int Id { get; set; }
    public string MemberId { get; set; } = "";
    public string AmountText { get; set; } = "0";
    public string Method { get; set; } = "";
    public int ReceiptNumber { get; set; }
    public long AtTicks { get; set; }
}
