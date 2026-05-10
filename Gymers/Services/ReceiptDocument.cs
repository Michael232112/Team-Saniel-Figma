using System.Globalization;
using CoreGraphics;
using Foundation;
using Gymers.Models;
using UIKit;

namespace Gymers.Services;

public sealed class ReceiptDocument
{
    static readonly UIColor Teal      = UIColor.FromRGB(0x0F, 0x76, 0x6E);
    static readonly UIColor Navy      = UIColor.FromRGB(0x18, 0x21, 0x2F);
    static readonly UIColor MutedGrey = UIColor.FromRGB(0x66, 0x70, 0x85);
    static readonly UIColor Divider   = UIColor.FromRGB(0xE2, 0xE8, 0xF0);

    const float PageWidth  = 595f;
    const float PageHeight = 842f;
    const float Margin     = 48f;

    readonly Payment _payment;
    readonly Member? _member;

    public ReceiptDocument(Payment payment, Member? member)
    {
        _payment = payment;
        _member  = member;
    }

    public void WritePdf(string path)
    {
        var bounds   = new CGRect(0, 0, PageWidth, PageHeight);
        var renderer = new UIGraphicsPdfRenderer(bounds, new UIGraphicsPdfRendererFormat());
        renderer.WritePdf(NSUrl.FromFilename(path), DrawPage, out var error);
        if (error is not null)
            throw new InvalidOperationException(error.LocalizedDescription);
    }

    void DrawPage(UIGraphicsPdfRendererContext ctx)
    {
        ctx.BeginPage();
        DrawHeader(ctx);
        DrawBody(ctx);
        DrawFooter();
    }

    void DrawHeader(UIGraphicsPdfRendererContext ctx)
    {
        new NSString("GYMERS").DrawString(
            new CGPoint(Margin, Margin),
            Attrs(UIFont.BoldSystemFontOfSize(28), Teal));

        new NSString("Gym Management System").DrawString(
            new CGPoint(Margin, Margin + 40),
            Attrs(UIFont.SystemFontOfSize(11), MutedGrey));

        var receiptAttrs = Attrs(UIFont.BoldSystemFontOfSize(20), Navy);
        var receiptStr   = new NSString("RECEIPT");
        var size         = receiptStr.GetSizeUsingAttributes(receiptAttrs);
        receiptStr.DrawString(
            new CGPoint(PageWidth - Margin - size.Width, Margin + 6),
            receiptAttrs);

        DrawDivider(ctx, y: Margin + 80);
    }

    void DrawBody(UIGraphicsPdfRendererContext ctx)
    {
        var greyAttrs     = Attrs(UIFont.SystemFontOfSize(11), MutedGrey);
        var navyBoldAttrs = Attrs(UIFont.BoldSystemFontOfSize(11), Navy);
        var navyAttrs     = Attrs(UIFont.SystemFontOfSize(11), Navy);

        var y = Margin + 110f;

        var prefix      = new NSString("Receipt #");
        var prefixSize  = prefix.GetSizeUsingAttributes(greyAttrs);
        prefix.DrawString(new CGPoint(Margin, y), greyAttrs);
        new NSString(_payment.ReceiptNumber.ToString(CultureInfo.InvariantCulture))
            .DrawString(new CGPoint(Margin + prefixSize.Width, y), navyBoldAttrs);

        var dateStr  = new NSString(_payment.At.ToString(
            "MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture));
        var dateSize = dateStr.GetSizeUsingAttributes(greyAttrs);
        dateStr.DrawString(
            new CGPoint(PageWidth - Margin - dateSize.Width, y),
            greyAttrs);

        y += 40;

        var memberName = _member?.Name ?? "(member removed)";
        var memberId   = _member?.Id   ?? "—";
        var memberTier = _member is null ? "—" : $"{_member.Tier} tier";

        new NSString("Member").DrawString(
            new CGPoint(Margin, y),
            Attrs(UIFont.BoldSystemFontOfSize(10), Teal));
        y += 18;
        new NSString(memberName).DrawString(
            new CGPoint(Margin, y),
            Attrs(UIFont.BoldSystemFontOfSize(14), Navy));
        y += 22;
        new NSString($"ID: {memberId} · {memberTier}")
            .DrawString(new CGPoint(Margin, y), greyAttrs);
        y += 36;

        new NSString("Payment").DrawString(
            new CGPoint(Margin, y),
            Attrs(UIFont.BoldSystemFontOfSize(10), Teal));
        y += 22;

        new NSString("Amount").DrawString(new CGPoint(Margin, y), greyAttrs);
        new NSString($"${_payment.Amount.ToString("0.00", CultureInfo.InvariantCulture)}")
            .DrawString(new CGPoint(Margin + 120, y), navyBoldAttrs);
        y += 20;

        new NSString("Method").DrawString(new CGPoint(Margin, y), greyAttrs);
        new NSString(_payment.Method)
            .DrawString(new CGPoint(Margin + 120, y), navyAttrs);
        y += 28;

        DrawDivider(ctx, y);
    }

    void DrawFooter()
    {
        var line1Y = PageHeight - Margin - 32;
        var line2Y = PageHeight - Margin - 16;

        new NSString("Thank you for being a Gymers member.").DrawString(
            new CGPoint(Margin, line1Y),
            Attrs(UIFont.SystemFontOfSize(11), Navy));
        new NSString("This receipt was issued by the Gymers app.").DrawString(
            new CGPoint(Margin, line2Y),
            Attrs(UIFont.SystemFontOfSize(10), MutedGrey));
    }

    void DrawDivider(UIGraphicsPdfRendererContext ctx, float y)
    {
        var cg = ctx.CGContext;
        cg.SetStrokeColor(Divider.CGColor);
        cg.SetLineWidth(1f);
        cg.MoveTo(Margin, y);
        cg.AddLineToPoint(PageWidth - Margin, y);
        cg.StrokePath();
    }

    static UIStringAttributes Attrs(UIFont font, UIColor color) => new()
    {
        Font            = font,
        ForegroundColor = color
    };
}
