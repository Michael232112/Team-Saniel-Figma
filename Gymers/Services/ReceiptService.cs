using Gymers.Models;
using Microsoft.Maui.Storage;
using QuestPDF.Fluent;

namespace Gymers.Services;

public sealed class ReceiptService
{
    public async Task<string> GenerateAsync(Payment payment, Member? member)
    {
        var dir = Path.Combine(FileSystem.CacheDirectory, "receipts");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"gymers-receipt-{payment.ReceiptNumber}.pdf");
        var doc  = new ReceiptDocument(payment, member);

        await Task.Run(() => doc.GeneratePdf(path));
        return path;
    }
}
