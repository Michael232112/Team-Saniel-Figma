namespace Gymers.Models;

public record Payment(
    int Id,
    string MemberId,
    decimal Amount,
    string Method,
    int ReceiptNumber,
    DateTime At);
