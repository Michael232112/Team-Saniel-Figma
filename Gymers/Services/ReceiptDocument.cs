using System.Globalization;
using Gymers.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using IContainer = QuestPDF.Infrastructure.IContainer;
using Colors = QuestPDF.Helpers.Colors;

namespace Gymers.Services;

public sealed class ReceiptDocument : IDocument
{
    static readonly string Teal      = "#0F766E";
    static readonly string Navy      = "#18212F";
    static readonly string MutedGrey = "#667085";
    static readonly string Divider   = "#E2E8F0";

    readonly Payment _payment;
    readonly Member? _member;

    public ReceiptDocument(Payment payment, Member? member)
    {
        _payment = payment;
        _member  = member;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title    = $"Gymers Receipt #{_payment.ReceiptNumber}",
        Author   = "Gymers",
        Subject  = "Payment Receipt",
        Producer = "Gymers Mobile App"
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(48);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(t => t.FontSize(11).FontColor(Navy));

            page.Header().Element(ComposeHeader);
            page.Content().PaddingVertical(16).Element(ComposeBody);
            page.Footer().Element(ComposeFooter);
        });
    }

    void ComposeHeader(IContainer header)
    {
        header.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("GYMERS")
                        .FontSize(28).Bold().FontColor(Teal);
                    c.Item().Text("Gym Management System")
                        .FontSize(11).FontColor(MutedGrey);
                });
                row.ConstantItem(120).AlignRight().Text("RECEIPT")
                    .FontSize(20).Bold().FontColor(Navy);
            });
            col.Item().PaddingTop(12).LineHorizontal(1).LineColor(Divider);
        });
    }

    void ComposeBody(IContainer body)
    {
        var memberName = _member?.Name ?? "(member removed)";
        var memberId   = _member?.Id   ?? "—";
        var memberTier = _member is null ? "—" : $"{_member.Tier} tier";

        body.Column(col =>
        {
            col.Spacing(20);

            col.Item().Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.Span("Receipt #").FontColor(MutedGrey);
                    t.Span(_payment.ReceiptNumber.ToString(CultureInfo.InvariantCulture))
                        .Bold().FontColor(Navy);
                });
                row.RelativeItem().AlignRight().Text(
                    _payment.At.ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture))
                    .FontColor(MutedGrey);
            });

            col.Item().Column(c =>
            {
                c.Item().PaddingBottom(4).Text("Member")
                    .FontSize(10).Bold().FontColor(Teal);
                c.Item().Text(memberName).FontSize(14).Bold();
                c.Item().Text($"ID: {memberId} · {memberTier}")
                    .FontSize(11).FontColor(MutedGrey);
            });

            col.Item().Column(c =>
            {
                c.Item().PaddingBottom(4).Text("Payment")
                    .FontSize(10).Bold().FontColor(Teal);
                c.Item().Row(r =>
                {
                    r.ConstantItem(120).Text("Amount").FontColor(MutedGrey);
                    r.RelativeItem().Text($"${_payment.Amount.ToString("0.00", CultureInfo.InvariantCulture)}")
                        .Bold();
                });
                c.Item().Row(r =>
                {
                    r.ConstantItem(120).Text("Method").FontColor(MutedGrey);
                    r.RelativeItem().Text(_payment.Method);
                });
            });

            col.Item().LineHorizontal(1).LineColor(Divider);
        });
    }

    void ComposeFooter(IContainer footer)
    {
        footer.Column(col =>
        {
            col.Item().Text("Thank you for being a Gymers member.")
                .FontSize(11).FontColor(Navy);
            col.Item().Text("This receipt was issued by the Gymers app.")
                .FontSize(10).FontColor(MutedGrey);
        });
    }
}
