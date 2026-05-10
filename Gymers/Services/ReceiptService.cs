using Gymers.Models;
using Microsoft.Maui.Storage;

namespace Gymers.Services;

public sealed class ReceiptService
{
    public Task<string> GenerateAsync(Payment payment, Member? member)
    {
        var dir = Path.Combine(FileSystem.CacheDirectory, "receipts");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"gymers-receipt-{payment.ReceiptNumber}.pdf");
        new ReceiptDocument(payment, member).WritePdf(path);
        return Task.FromResult(path);
    }
}
