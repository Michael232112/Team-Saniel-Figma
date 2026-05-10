using System.Globalization;
using CoreGraphics;
using Foundation;
using Gymers.Models;
using UIKit;

namespace Gymers.Services;

public sealed class ReportDocument
{
    static readonly UIColor Teal      = UIColor.FromRGB(0x0F, 0x76, 0x6E);
    static readonly UIColor Navy      = UIColor.FromRGB(0x18, 0x21, 0x2F);
    static readonly UIColor MutedGrey = UIColor.FromRGB(0x66, 0x70, 0x85);
    static readonly UIColor Divider   = UIColor.FromRGB(0xE2, 0xE8, 0xF0);

    const float PageWidth     = 595f;
    const float PageHeight    = 842f;
    const float Margin        = 48f;
    const float HeaderHeight  = 88f;   // top band: GYMERS / kind / period subtitle / divider
    const float FooterReserve = 60f;   // space kept blank for the totals row
    const float RowHeight     = 22f;

    readonly ReportKind   _kind;
    readonly ReportPeriod _period;
    readonly DateTime     _generatedAt;
    readonly IReadOnlyList<Member>   _members;
    readonly IReadOnlyList<Payment>  _payments;
    readonly IReadOnlyList<CheckIn>  _checkIns;

    public ReportDocument(
        ReportKind kind,
        ReportPeriod period,
        DateTime generatedAt,
        IReadOnlyList<Member> members,
        IReadOnlyList<Payment> payments,
        IReadOnlyList<CheckIn> checkIns)
    {
        _kind        = kind;
        _period      = period;
        _generatedAt = generatedAt;
        _members     = members;
        _payments    = payments;
        _checkIns    = checkIns;
    }

    public void WritePdf(string path)
    {
        var bounds   = new CGRect(0, 0, PageWidth, PageHeight);
        var renderer = new UIGraphicsPdfRenderer(bounds, new UIGraphicsPdfRendererFormat());
        renderer.WritePdf(NSUrl.FromFilename(path), DrawDocument, out var error);
        if (error is not null)
            throw new InvalidOperationException(error.LocalizedDescription);
    }

    void DrawDocument(UIGraphicsPdfRendererContext ctx)
    {
        switch (_kind)
        {
            case ReportKind.Revenue:    DrawRevenue(ctx);    break;
            case ReportKind.Attendance: DrawAttendance(ctx); break;
            case ReportKind.Roster:     DrawRoster(ctx);     break;
        }
    }

    // ---- Revenue ----

    void DrawRevenue(UIGraphicsPdfRendererContext ctx)
    {
        var (from, to) = _period.Range(_generatedAt);
        var rows = _payments
            .Where(p => p.At >= from && p.At < to)
            .OrderByDescending(p => p.At)
            .Select(p => (Payment: p, Member: _members.FirstOrDefault(m => m.Id == p.MemberId)))
            .ToList();

        decimal total = rows.Sum(r => r.Payment.Amount);

        BeginPageWithHeader(ctx);
        var y = Margin + HeaderHeight;

        // Column header
        DrawRow(y, ("Date", 0), ("Receipt", 110), ("Member", 180), ("Method", 340), ("Amount", 420));
        y += RowHeight;
        DrawDivider(ctx, y - 4);

        if (rows.Count == 0)
        {
            new NSString("No data for this period.").DrawString(
                new CGPoint(Margin, y + 8),
                Attrs(UIFont.SystemFontOfSize(11), MutedGrey));
            DrawTotalsLine(ctx, y + 32, $"Total: 0 payments · $0.00");
            return;
        }

        foreach (var (p, m) in rows)
        {
            if (y + RowHeight > PageHeight - Margin - FooterReserve)
            {
                BeginPageWithHeader(ctx);
                y = Margin + HeaderHeight;
                DrawRow(y, ("Date", 0), ("Receipt", 110), ("Member", 180), ("Method", 340), ("Amount", 420));
                y += RowHeight;
                DrawDivider(ctx, y - 4);
            }

            DrawRow(y,
                (p.At.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), 0),
                ("#" + p.ReceiptNumber.ToString(CultureInfo.InvariantCulture), 110),
                (Truncate(m?.Name ?? "(removed)", 22), 180),
                (p.Method, 340),
                ("$" + p.Amount.ToString("0.00", CultureInfo.InvariantCulture), 420));
            y += RowHeight;
        }

        DrawTotalsLine(ctx, y + 16,
            $"Total: {rows.Count} payment{(rows.Count == 1 ? "" : "s")} · $" +
            total.ToString("0.00", CultureInfo.InvariantCulture));
    }

    // ---- Attendance ----

    void DrawAttendance(UIGraphicsPdfRendererContext ctx)
    {
        var (from, to) = _period.Range(_generatedAt);
        var rows = _checkIns
            .Where(c => c.At >= from && c.At < to)
            .OrderByDescending(c => c.At)
            .Select(c => (CheckIn: c, Member: _members.FirstOrDefault(m => m.Id == c.MemberId)))
            .ToList();

        int unique = rows.Select(r => r.CheckIn.MemberId).Distinct().Count();

        BeginPageWithHeader(ctx);
        var y = Margin + HeaderHeight;

        DrawRow(y, ("Date", 0), ("Time", 110), ("Member", 200), ("Member ID", 380));
        y += RowHeight;
        DrawDivider(ctx, y - 4);

        if (rows.Count == 0)
        {
            new NSString("No data for this period.").DrawString(
                new CGPoint(Margin, y + 8),
                Attrs(UIFont.SystemFontOfSize(11), MutedGrey));
            DrawTotalsLine(ctx, y + 32, "Total: 0 check-ins · 0 unique members");
            return;
        }

        foreach (var (c, m) in rows)
        {
            if (y + RowHeight > PageHeight - Margin - FooterReserve)
            {
                BeginPageWithHeader(ctx);
                y = Margin + HeaderHeight;
                DrawRow(y, ("Date", 0), ("Time", 110), ("Member", 200), ("Member ID", 380));
                y += RowHeight;
                DrawDivider(ctx, y - 4);
            }

            DrawRow(y,
                (c.At.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), 0),
                (c.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture), 110),
                (Truncate(m?.Name ?? "(removed)", 24), 200),
                (c.MemberId, 380));
            y += RowHeight;
        }

        DrawTotalsLine(ctx, y + 16,
            $"Total: {rows.Count} check-in{(rows.Count == 1 ? "" : "s")} · {unique} unique member{(unique == 1 ? "" : "s")}");
    }

    // ---- Roster ----

    void DrawRoster(UIGraphicsPdfRendererContext ctx)
    {
        var rows   = _members.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
        int active = rows.Count(m => string.Equals(m.Status, "Active", StringComparison.OrdinalIgnoreCase));
        int other  = rows.Count - active;

        BeginPageWithHeader(ctx);
        var y = Margin + HeaderHeight;

        DrawRow(y, ("ID", 0), ("Name", 80), ("Tier", 240), ("Status", 320), ("Expires", 410));
        y += RowHeight;
        DrawDivider(ctx, y - 4);

        if (rows.Count == 0)
        {
            new NSString("No members.").DrawString(
                new CGPoint(Margin, y + 8),
                Attrs(UIFont.SystemFontOfSize(11), MutedGrey));
            DrawTotalsLine(ctx, y + 32, "Total: 0 active · 0 other");
            return;
        }

        foreach (var m in rows)
        {
            if (y + RowHeight > PageHeight - Margin - FooterReserve)
            {
                BeginPageWithHeader(ctx);
                y = Margin + HeaderHeight;
                DrawRow(y, ("ID", 0), ("Name", 80), ("Tier", 240), ("Status", 320), ("Expires", 410));
                y += RowHeight;
                DrawDivider(ctx, y - 4);
            }

            DrawRow(y,
                (m.Id, 0),
                (Truncate(m.Name, 26), 80),
                (m.Tier.ToString(), 240),
                (m.Status, 320),
                (m.Expires.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), 410));
            y += RowHeight;
        }

        DrawTotalsLine(ctx, y + 16, $"Total: {active} active · {other} other");
    }

    // ---- Drawing primitives ----

    void BeginPageWithHeader(UIGraphicsPdfRendererContext ctx)
    {
        ctx.BeginPage();
        DrawHeaderBand(ctx);
    }

    void DrawHeaderBand(UIGraphicsPdfRendererContext ctx)
    {
        new NSString("GYMERS").DrawString(
            new CGPoint(Margin, Margin),
            Attrs(UIFont.BoldSystemFontOfSize(28), Teal));

        new NSString("Gym Management System").DrawString(
            new CGPoint(Margin, Margin + 36),
            Attrs(UIFont.SystemFontOfSize(11), MutedGrey));

        var titleAttrs = Attrs(UIFont.BoldSystemFontOfSize(20), Navy);
        var titleStr   = new NSString(_kind.Label().ToUpperInvariant());
        var titleSize  = titleStr.GetSizeUsingAttributes(titleAttrs);
        titleStr.DrawString(
            new CGPoint(PageWidth - Margin - titleSize.Width, Margin + 6),
            titleAttrs);

        var subtitle = _kind == ReportKind.Roster
            ? $"Snapshot · Generated {_generatedAt.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture)}"
            : $"Period: {_period.Label()} · Generated {_generatedAt.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture)}";
        var subAttrs = Attrs(UIFont.SystemFontOfSize(10), MutedGrey);
        var subSize  = new NSString(subtitle).GetSizeUsingAttributes(subAttrs);
        new NSString(subtitle).DrawString(
            new CGPoint(PageWidth - Margin - subSize.Width, Margin + 36),
            subAttrs);

        DrawDivider(null, Margin + HeaderHeight - 8);
    }

    void DrawRow(float y, params (string Text, float X)[] cells)
    {
        var attrs = Attrs(UIFont.SystemFontOfSize(10), Navy);
        foreach (var (text, x) in cells)
        {
            new NSString(text).DrawString(new CGPoint(Margin + x, y), attrs);
        }
    }

    void DrawTotalsLine(UIGraphicsPdfRendererContext ctx, float y, string text)
    {
        DrawDivider(ctx, y - 6);
        new NSString(text).DrawString(
            new CGPoint(Margin, y + 4),
            Attrs(UIFont.BoldSystemFontOfSize(11), Navy));
    }

    void DrawDivider(UIGraphicsPdfRendererContext? ctx, float y)
    {
        var cg = UIGraphics.GetCurrentContext();
        if (cg is null) return;
        cg.SetStrokeColor(Divider.CGColor);
        cg.SetLineWidth(1f);
        cg.MoveTo(Margin, y);
        cg.AddLineToPoint(PageWidth - Margin, y);
        cg.StrokePath();
    }

    static string Truncate(string s, int maxChars) =>
        s.Length <= maxChars ? s : s.Substring(0, maxChars - 1) + "…";

    static UIStringAttributes Attrs(UIFont font, UIColor color) => new()
    {
        Font            = font,
        ForegroundColor = color
    };
}
