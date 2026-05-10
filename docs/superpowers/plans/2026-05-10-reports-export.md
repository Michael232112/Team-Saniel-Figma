# Gymers Mobile App — Reports + Export — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Reports tab where staff/admin pick a period (Week / Month / All) and share three pre-canned reports — Revenue, Attendance, Member Roster — as **multi-page PDF** or **CSV** through the system share sheet. Reports re-render deterministically from SQLite.

**Architecture:** Add `ReportService` DI singleton that builds a `ReportDocument` (a `UIGraphicsPdfRenderer`-based pure-layout class that supports multi-page) for PDFs and a `CsvWriter` helper for CSVs, writing to `FileSystem.CacheDirectory/reports/`, then handing the file to `Microsoft.Maui.ApplicationModel.DataTransfer.Share`. A new `ReportsPage` (with period selector + three cards, each with `Share PDF` + `Share CSV` buttons) is added as the 5th tab in `AppShell` and as a 5th pill in the custom `BottomTabBar` ContentView used by every page.

**Tech Stack:** .NET 10, MAUI, C# 12, XAML. UIKit / Foundation / CoreGraphics (built into iOS + Mac Catalyst SDKs — no NuGet). Plain `StreamWriter` for CSV.

**Spec:** `docs/superpowers/specs/2026-05-10-reports-export-design.md` (commit `1bfdc44`).

---

## Files Touched

| File                                                     | Action |
| -------------------------------------------------------- | ------ |
| `Gymers/Models/ReportPeriod.cs`                          | Create — enum + `Range` extension |
| `Gymers/Models/ReportKind.cs`                            | Create — enum |
| `Gymers/Services/CsvWriter.cs`                           | Create |
| `Gymers/Services/ReportDocument.cs`                      | Create — multi-page PDF layout |
| `Gymers/Services/ReportService.cs`                       | Create |
| `Gymers/Pages/ReportsPage.xaml`                          | Create |
| `Gymers/Pages/ReportsPage.xaml.cs`                       | Create |
| `Gymers/AppShell.xaml`                                   | Modify — append `Reports` `ShellContent` to TabBar |
| `Gymers/Controls/BottomTabBar.xaml`                      | Modify — add 5th pill + grid column |
| `Gymers/Controls/BottomTabBar.xaml.cs`                   | Modify — add `Reports` to `AppTab` enum, pill colors, nav handler |
| `Gymers/MauiProgram.cs`                                  | Modify — register `ReportService` + `ReportsPage` in DI |
| `docs/status/build_status_docx.py`                       | Modify — move "Reports export" bullet to completed |
| `docs/status/gymers-mobile-app-status-update.html`       | Modify — mirror status doc change |

Thirteen files. Zero new NuGet packages. No model record edits, no DB schema change.

---

## Run helper (referenced by every task)

When a task says "build and run," do this from the repo root.

**Build (Mac Catalyst, primary verification target):**
```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
```

**Build (iOS, must also stay green):**
```bash
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

**Quit any running instance and relaunch:**
```bash
pkill -f "Gymers/bin/Debug/net10.0-maccatalyst" 2>/dev/null; sleep 1
open Gymers/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Gymers.app
```

**Find the SQLite file (created by the predecessor SQLite slice):**
```bash
find ~/Library/Containers -name "gymers.db3" 2>/dev/null
```

**Find the reports cache directory (after first export):**
```bash
find ~/Library/Containers -path "*/reports/gymers-report-*" 2>/dev/null
```

**Wipe the reports cache (forces fresh generation):**
```bash
find ~/Library/Containers -path "*/reports/gymers-report-*" -delete 2>/dev/null
```

**Why Mac Catalyst, not iOS sim:** the iOS simulator is unusable on this hardware. Mac Catalyst runs the same MAUI code paths natively and exposes the iOS UIKit surface (so `UIGraphicsPdfRenderer` works). iOS-target builds must still succeed (it's the primary deploy target), but verification happens on Mac Catalyst.

---

## Task 1: Foundation — `ReportPeriod` + `ReportKind` enums

After this task two enums and a single extension method exist as compiled types. Nothing references them yet. Build still green; no UI behavior change.

**Files:**
- Create: `Gymers/Models/ReportPeriod.cs`
- Create: `Gymers/Models/ReportKind.cs`

- [ ] **Step 1: Create `Gymers/Models/ReportPeriod.cs`**

```csharp
namespace Gymers.Models;

public enum ReportPeriod
{
    Week,
    Month,
    All
}

public static class ReportPeriodExtensions
{
    public static (DateTime From, DateTime To) Range(this ReportPeriod period, DateTime now) =>
        period switch
        {
            ReportPeriod.Week  => (now.AddDays(-7), now.AddSeconds(1)),
            ReportPeriod.Month => (now.AddDays(-30), now.AddSeconds(1)),
            ReportPeriod.All   => (DateTime.MinValue, DateTime.MaxValue),
            _                  => throw new ArgumentOutOfRangeException(nameof(period))
        };

    public static string Label(this ReportPeriod period) =>
        period switch
        {
            ReportPeriod.Week  => "Week",
            ReportPeriod.Month => "Month",
            ReportPeriod.All   => "All",
            _                  => throw new ArgumentOutOfRangeException(nameof(period))
        };

    public static string Slug(this ReportPeriod period) =>
        period.ToString().ToLowerInvariant();
}
```

What this does:
- `Range(now)` returns the half-open `[From, To)` window the report should filter on. `+1s` upper bound prevents an equality-edge skip when a payment is recorded "right now."
- `All` uses `DateTime.MinValue` / `MaxValue` so a `>= From && < To` predicate naturally matches every record. We never push these to SQLite; filtering happens against the in-memory `ObservableCollection` from `DataStore`, where extreme `DateTime` values are safe.
- `Label` is for the human-readable string in PDF headers and status text.
- `Slug` is for filenames (`week`, `month`, `all`).

- [ ] **Step 2: Create `Gymers/Models/ReportKind.cs`**

```csharp
namespace Gymers.Models;

public enum ReportKind
{
    Revenue,
    Attendance,
    Roster
}

public static class ReportKindExtensions
{
    public static string Label(this ReportKind kind) =>
        kind switch
        {
            ReportKind.Revenue    => "Revenue",
            ReportKind.Attendance => "Attendance",
            ReportKind.Roster     => "Member Roster",
            _                     => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    public static string Slug(this ReportKind kind) =>
        kind switch
        {
            ReportKind.Revenue    => "revenue",
            ReportKind.Attendance => "attendance",
            ReportKind.Roster     => "roster",
            _                     => throw new ArgumentOutOfRangeException(nameof(kind))
        };
}
```

`Slug` is used in filenames (`gymers-report-revenue-month-20260510.pdf`); `Label` is used in PDF headers and the share-sheet title.

- [ ] **Step 3: Build both targets**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: each ends with `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 4: Smoke-test the app**

Use the run helper to relaunch on Mac Catalyst. Verify the app behaves identically — Login → Dashboard → Members search → Payments record → tap a payment row to share its receipt → Attendance check-in all work. The new enums are unused; this step only confirms nothing broke.

- [ ] **Step 5: Commit**

```bash
git add Gymers/Models/ReportPeriod.cs Gymers/Models/ReportKind.cs
git commit -m "feat(reports): add ReportPeriod + ReportKind enums

Foundation for the reports + export slice. Range(now) returns a
half-open [From, To) window for in-memory filtering; Label/Slug
extensions feed PDF headers, status text, and filenames."
```

---

## Task 2: `CsvWriter` — RFC 4180–style quoting helper

After this task a static `CsvWriter` class exists with three public methods (one per `ReportKind`) plus a private quoting helper. Nothing references it yet. Build still green; no UI behavior change.

**Files:**
- Create: `Gymers/Services/CsvWriter.cs`

- [ ] **Step 1: Create `Gymers/Services/CsvWriter.cs`**

```csharp
using System.Globalization;
using System.Text;
using Gymers.Models;

namespace Gymers.Services;

public static class CsvWriter
{
    static readonly char[] MustQuote = { ',', '"', '\r', '\n' };

    public static void WriteRevenue(string path, IEnumerable<(Payment Payment, Member? Member)> rows)
    {
        using var w = OpenWriter(path);
        w.WriteLine("date,receipt,member_name,member_id,method,amount");
        foreach (var (p, m) in rows)
        {
            w.WriteLine(string.Join(',',
                Quote(p.At.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                Quote(p.ReceiptNumber.ToString(CultureInfo.InvariantCulture)),
                Quote(m?.Name ?? "(removed)"),
                Quote(p.MemberId),
                Quote(p.Method),
                Quote(p.Amount.ToString("0.00", CultureInfo.InvariantCulture))));
        }
    }

    public static void WriteAttendance(string path, IEnumerable<(CheckIn CheckIn, Member? Member)> rows)
    {
        using var w = OpenWriter(path);
        w.WriteLine("date,time,member_name,member_id");
        foreach (var (c, m) in rows)
        {
            w.WriteLine(string.Join(',',
                Quote(c.At.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Quote(c.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture)),
                Quote(m?.Name ?? "(removed)"),
                Quote(c.MemberId)));
        }
    }

    public static void WriteRoster(string path, IEnumerable<Member> rows)
    {
        using var w = OpenWriter(path);
        w.WriteLine("id,name,tier,status,expires");
        foreach (var m in rows)
        {
            w.WriteLine(string.Join(',',
                Quote(m.Id),
                Quote(m.Name),
                Quote(m.Tier.ToString()),
                Quote(m.Status),
                Quote(m.Expires.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))));
        }
    }

    static StreamWriter OpenWriter(string path) =>
        new(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            NewLine = "\n"
        };

    static string Quote(string value)
    {
        if (value.IndexOfAny(MustQuote) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
```

What this does:
- Three public writers, one per `ReportKind`. Each takes the absolute path and an enumerable of pre-joined `(domain, member?)` tuples — joining is the caller's job (the service has the `DataStore` reference).
- `OpenWriter` configures UTF-8 **without** BOM (Numbers, Excel, Sheets, Preview all read this correctly; the BOM shows up as a stray glyph in Preview's plain-text mode) and `\n` line endings (smaller, every consumer we care about handles LF fine).
- `Quote` is RFC 4180–style: only fields containing `,`, `"`, `\r`, or `\n` get wrapped; internal `"` is doubled. Fields without those characters are written bare.
- All number/date formatting is `CultureInfo.InvariantCulture` so the file is portable across locales.
- Member-removed fallback: `m?.Name ?? "(removed)"` — never crashes on a stale `MemberId`.

- [ ] **Step 2: Build both targets**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: `0 Warning(s) 0 Error(s)`.

- [ ] **Step 3: Smoke-test the app**

Relaunch on Mac Catalyst. Verify the app behaves identically (no consumers of `CsvWriter` yet). This step only confirms nothing broke.

- [ ] **Step 4: Commit**

```bash
git add Gymers/Services/CsvWriter.cs
git commit -m "feat(reports): add CsvWriter helper

Three static writers (Revenue / Attendance / Roster) emit UTF-8
(no BOM) CSV with LF endings and RFC 4180-style quoting. All
number and date formatting uses InvariantCulture for portability.
Not yet wired into anything."
```

---

## Task 3: `ReportDocument` — multi-page PDF layout

After this task `ReportDocument` exists as a compiled class with a `WritePdf(path)` method. It produces a multi-page PDF for any of the three `ReportKind` values. Nothing references it yet. Build still green; no UI behavior change.

**Files:**
- Create: `Gymers/Services/ReportDocument.cs`

- [ ] **Step 1: Create `Gymers/Services/ReportDocument.cs`**

```csharp
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
```

What this does:
- One public method (`WritePdf`), one internal dispatcher (`DrawDocument`), one drawing routine per kind (`DrawRevenue` / `DrawAttendance` / `DrawRoster`), and shared primitives (`BeginPageWithHeader`, `DrawHeaderBand`, `DrawRow`, `DrawTotalsLine`, `DrawDivider`, `Truncate`, `Attrs`).
- Pagination loop is identical across kinds: before every row, check `if (y + RowHeight > PageHeight - Margin - FooterReserve)`, and if so, start a new page with the header band redrawn and the column header re-emitted. `FooterReserve` is held back on **every** page even though totals only render at the end — this keeps the layout function stateless and avoids tracking "is this the last page" mid-loop.
- Empty-result handling: a single grey "No data for this period." line plus a zero-totals row. Still emits one page with the full header band.
- Member-removed fallback: `m?.Name ?? "(removed)"` everywhere a member is dereferenced.
- All number / date formatting uses `CultureInfo.InvariantCulture` so PDFs are locale-stable.
- `Truncate` keeps long names from overflowing the next column; we lose the tail rather than the layout.

- [ ] **Step 2: Build both targets**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: `0 Warning(s) 0 Error(s)`.

If you see CS0104 ambiguous-reference errors on `IContainer` or `Colors`, you've imported too much — `ReportDocument` does NOT use any QuestPDF types. Verify the `using` block at the top matches exactly what's in Step 1 (System.Globalization, CoreGraphics, Foundation, Gymers.Models, UIKit — nothing else).

- [ ] **Step 3: Smoke-test the app**

Relaunch on Mac Catalyst. App behavior unchanged — `ReportDocument` is unused. This step only confirms nothing broke.

- [ ] **Step 4: Commit**

```bash
git add Gymers/Services/ReportDocument.cs
git commit -m "feat(reports): add ReportDocument multi-page PDF layout

Pure-layout class over UIGraphicsPdfRenderer. One renderer per
ReportKind (Revenue / Attendance / Roster), shared header band
and pagination loop. Reserves footer space uniformly so the
totals row always lands on the last page without mid-loop
'is this the last page' tracking. Not yet wired in."
```

---

## Task 4: `ReportService` — file write + DI registration

After this task `ReportService` is registered in DI and can be injected into pages. It writes the PDF and CSV files to `FileSystem.CacheDirectory/reports/`, computes the per-card summary line, and returns absolute paths. Build still green; nothing calls it yet.

**Files:**
- Create: `Gymers/Services/ReportService.cs`
- Modify: `Gymers/MauiProgram.cs`

- [ ] **Step 1: Create `Gymers/Services/ReportService.cs`**

```csharp
using System.Globalization;
using Gymers.Data;
using Gymers.Models;
using Microsoft.Maui.Storage;

namespace Gymers.Services;

public sealed class ReportService
{
    readonly DataStore _data;

    public ReportService(DataStore data) => _data = data;

    public Task<string> GeneratePdfAsync(ReportKind kind, ReportPeriod period)
    {
        var path = BuildPath(kind, period, "pdf");
        var doc  = new ReportDocument(
            kind, period, DateTime.Now,
            _data.Members.ToList(),
            _data.Payments.ToList(),
            _data.CheckIns.ToList());
        doc.WritePdf(path);
        return Task.FromResult(path);
    }

    public Task<string> GenerateCsvAsync(ReportKind kind, ReportPeriod period)
    {
        var path = BuildPath(kind, period, "csv");
        var now  = DateTime.Now;

        switch (kind)
        {
            case ReportKind.Revenue:
            {
                var (from, to) = period.Range(now);
                var rows = _data.Payments
                    .Where(p => p.At >= from && p.At < to)
                    .OrderByDescending(p => p.At)
                    .Select(p => (p, _data.Members.FirstOrDefault(m => m.Id == p.MemberId)));
                CsvWriter.WriteRevenue(path, rows);
                break;
            }
            case ReportKind.Attendance:
            {
                var (from, to) = period.Range(now);
                var rows = _data.CheckIns
                    .Where(c => c.At >= from && c.At < to)
                    .OrderByDescending(c => c.At)
                    .Select(c => (c, _data.Members.FirstOrDefault(m => m.Id == c.MemberId)));
                CsvWriter.WriteAttendance(path, rows);
                break;
            }
            case ReportKind.Roster:
            {
                var rows = _data.Members.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase);
                CsvWriter.WriteRoster(path, rows);
                break;
            }
        }

        return Task.FromResult(path);
    }

    public string Summarize(ReportKind kind, ReportPeriod period)
    {
        var now = DateTime.Now;
        switch (kind)
        {
            case ReportKind.Revenue:
            {
                var (from, to) = period.Range(now);
                var rows  = _data.Payments.Where(p => p.At >= from && p.At < to).ToList();
                var total = rows.Sum(p => p.Amount);
                return rows.Count == 0
                    ? "No payments in this period."
                    : $"${total.ToString("0.00", CultureInfo.InvariantCulture)} from {rows.Count} payment{(rows.Count == 1 ? "" : "s")}";
            }
            case ReportKind.Attendance:
            {
                var (from, to) = period.Range(now);
                var rows   = _data.CheckIns.Where(c => c.At >= from && c.At < to).ToList();
                var unique = rows.Select(c => c.MemberId).Distinct().Count();
                return rows.Count == 0
                    ? "No check-ins in this period."
                    : $"{rows.Count} check-in{(rows.Count == 1 ? "" : "s")}, {unique} unique member{(unique == 1 ? "" : "s")}";
            }
            case ReportKind.Roster:
            {
                int active = _data.Members.Count(m => string.Equals(m.Status, "Active", StringComparison.OrdinalIgnoreCase));
                int other  = _data.Members.Count - active;
                return $"{active} active, {other} other";
            }
            default:
                return "—";
        }
    }

    static string BuildPath(ReportKind kind, ReportPeriod period, string ext)
    {
        var dir = Path.Combine(FileSystem.CacheDirectory, "reports");
        Directory.CreateDirectory(dir);
        var stamp = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var name  = $"gymers-report-{kind.Slug()}-{period.Slug()}-{stamp}.{ext}";
        return Path.Combine(dir, name);
    }
}
```

What this does:
- `GeneratePdfAsync` snapshots `Members`, `Payments`, `CheckIns` into plain `List<>`s and hands them to `ReportDocument`. Snapshotting avoids any `ObservableCollection` mutation racing with the draw loop. Same logic snapshot is implicit per kind; we always copy all three because the snapshots are cheap (≤ a few hundred records in the demo).
- `GenerateCsvAsync` builds the per-kind row enumerable and delegates to `CsvWriter`.
- `Summarize` returns the one-line text that lights up the card on screen — same filtering logic as the document, but only the summary numbers.
- `BuildPath` is shared. `kind.Slug()` and `period.Slug()` come from Task 1's extensions. `yyyyMMdd` stamp on the filename means same-day re-taps overwrite the same file; cross-day exports keep separate filenames.
- `Task<string>` return is preserved on the `*Async` methods for symmetry with `ReceiptService` even though both are synchronous internally — UIKit drawing must run on the UI thread (the calling button handler is already there), and CSV writing is too cheap to warrant `Task.Run`.

- [ ] **Step 2: Register `ReportService` as a DI singleton in `MauiProgram.cs`**

Open `Gymers/MauiProgram.cs`. Find the existing service registrations:

```csharp
builder.Services.AddSingleton<DataStore>();
builder.Services.AddSingleton<ReceiptService>();
```

Add a new line immediately below `ReceiptService`:

```csharp
builder.Services.AddSingleton<ReportService>();
```

So the singleton block reads:

```csharp
builder.Services.AddSingleton<DataStore>();
builder.Services.AddSingleton<ReceiptService>();
builder.Services.AddSingleton<ReportService>();
```

(Page DI registration for `ReportsPage` lands in Task 5 once that class exists.)

- [ ] **Step 3: Build both targets**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: `0 Warning(s) 0 Error(s)`.

- [ ] **Step 4: Smoke-test the app**

Relaunch on Mac Catalyst. **This is critical** — adding a DI registration is exactly the kind of change that can crash startup without breaking the build (the lesson from the QuestPDF pivot, encoded in the project memory). Sign in with `admin / admin123`, click through Dashboard / Members / Payments / Attendance, tap a payment row to verify the receipt share sheet still works.

If the app fails to launch, the most likely cause is a missing constructor parameter resolution for `ReportService` — verify `DataStore` is registered above it (it is — that registration already exists).

- [ ] **Step 5: Commit**

```bash
git add Gymers/Services/ReportService.cs Gymers/MauiProgram.cs
git commit -m "feat(reports): add ReportService + DI registration

Snapshots DataStore collections, builds the PDF via
ReportDocument or the CSV via CsvWriter, returns the absolute
path to the file under FileSystem.CacheDirectory/reports/.
Summarize() returns the per-card one-line summary text.
Registered as a DI singleton. No page consumes it yet."
```

---

## Task 5: `ReportsPage` + 5th tab + page DI

After this task there is a fifth pill in the BottomTabBar labeled "Reports", tapping it navigates to a new page where the user picks a period and shares each report as PDF or CSV via the system share sheet. Errors surface via a red status label. This is the slice's user-facing payoff.

**Files:**
- Create: `Gymers/Pages/ReportsPage.xaml`
- Create: `Gymers/Pages/ReportsPage.xaml.cs`
- Modify: `Gymers/AppShell.xaml`
- Modify: `Gymers/Controls/BottomTabBar.xaml`
- Modify: `Gymers/Controls/BottomTabBar.xaml.cs`
- Modify: `Gymers/MauiProgram.cs`

- [ ] **Step 1: Create `Gymers/Pages/ReportsPage.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:c="clr-namespace:Gymers.Controls"
             x:Class="Gymers.Pages.ReportsPage"
             BackgroundColor="{StaticResource BgApp}"
             Shell.NavBarIsVisible="False">

    <Grid RowDefinitions="Auto,*,Auto">

        <c:TopAppBar Grid.Row="0" Title="Reports"
                     TrailingIconGlyph="{x:Static c:Icons.Bell}" />

        <ScrollView Grid.Row="1" Padding="24,16">
            <VerticalStackLayout Spacing="16">

                <Label Style="{StaticResource H2Section}" Text="Period" />

                <HorizontalStackLayout Spacing="8">
                    <Button x:Name="WeekButton"  Text="Week"  WidthRequest="88" />
                    <Button x:Name="MonthButton" Text="Month" WidthRequest="88" />
                    <Button x:Name="AllButton"   Text="All"   WidthRequest="88" />
                </HorizontalStackLayout>

                <Label Style="{StaticResource H2Section}" Text="Reports" />

                <Border Style="{StaticResource Card}">
                    <VerticalStackLayout Spacing="8">
                        <Label Style="{StaticResource H3Card}" Text="Revenue" />
                        <Label x:Name="RevenueSummary"
                               Style="{StaticResource Caption}"
                               Text="—" />
                        <HorizontalStackLayout Spacing="8">
                            <Button x:Name="RevenuePdfButton" Text="Share PDF"
                                    BackgroundColor="{StaticResource NavyDeep}"
                                    TextColor="White" />
                            <Button x:Name="RevenueCsvButton" Text="Share CSV"
                                    BackgroundColor="Transparent"
                                    TextColor="{StaticResource NavyDeep}"
                                    BorderColor="{StaticResource NavyDeep}"
                                    BorderWidth="1" />
                        </HorizontalStackLayout>
                    </VerticalStackLayout>
                </Border>

                <Border Style="{StaticResource Card}">
                    <VerticalStackLayout Spacing="8">
                        <Label Style="{StaticResource H3Card}" Text="Attendance" />
                        <Label x:Name="AttendanceSummary"
                               Style="{StaticResource Caption}"
                               Text="—" />
                        <HorizontalStackLayout Spacing="8">
                            <Button x:Name="AttendancePdfButton" Text="Share PDF"
                                    BackgroundColor="{StaticResource NavyDeep}"
                                    TextColor="White" />
                            <Button x:Name="AttendanceCsvButton" Text="Share CSV"
                                    BackgroundColor="Transparent"
                                    TextColor="{StaticResource NavyDeep}"
                                    BorderColor="{StaticResource NavyDeep}"
                                    BorderWidth="1" />
                        </HorizontalStackLayout>
                    </VerticalStackLayout>
                </Border>

                <Border Style="{StaticResource Card}">
                    <VerticalStackLayout Spacing="8">
                        <Label Style="{StaticResource H3Card}" Text="Member Roster" />
                        <Label x:Name="RosterSummary"
                               Style="{StaticResource Caption}"
                               Text="—" />
                        <HorizontalStackLayout Spacing="8">
                            <Button x:Name="RosterPdfButton" Text="Share PDF"
                                    BackgroundColor="{StaticResource NavyDeep}"
                                    TextColor="White" />
                            <Button x:Name="RosterCsvButton" Text="Share CSV"
                                    BackgroundColor="Transparent"
                                    TextColor="{StaticResource NavyDeep}"
                                    BorderColor="{StaticResource NavyDeep}"
                                    BorderWidth="1" />
                        </HorizontalStackLayout>
                    </VerticalStackLayout>
                </Border>

                <Label x:Name="StatusLabel"
                       Style="{StaticResource BodySm}"
                       HorizontalTextAlignment="Center"
                       IsVisible="False" />

                <Label Style="{StaticResource Caption}"
                       Text="Member Roster is always a current snapshot — the period selector does not change it." />

            </VerticalStackLayout>
        </ScrollView>

        <c:BottomTabBar Grid.Row="2" ActiveTab="Reports" />
    </Grid>
</ContentPage>
```

What this does:
- Mirrors the structure of `MembersPage.xaml` and `PaymentsPage.xaml`: `TopAppBar`, scroll body, `BottomTabBar`. `Shell.NavBarIsVisible="False"` matches the others.
- Period selector is three plain MAUI `<Button>` elements in a `HorizontalStackLayout`. We deliberately do NOT use `c:SecondaryButton` — that custom control's root is just a `Label` (no paintable surface), so `BackgroundColor` writes wouldn't render. Plain `Button` exposes a real `BackgroundColor` and `TextColor`, which the code-behind toggles in `RefreshPeriodButtons` to indicate the active period (`PaleBlue` background + `NavyHeading` text when active; transparent + `TextMuted` when inactive). The pattern mirrors `BottomTabBar`'s active-pill paint.
- Three report cards built from `Border + Card style + VerticalStackLayout`, mirroring how `PaymentsPage` builds its "Record Payment" card.
- Each card has a `H3Card` title, a `Caption`-styled summary `Label` (recomputed on period change), and two action buttons. PDF is the primary action — `BackgroundColor=NavyDeep`, `TextColor=White`. CSV is the secondary action — `BackgroundColor=Transparent`, `BorderColor=NavyDeep`, `TextColor=NavyDeep`. Both are plain MAUI `<Button>` with native `Clicked` events. (We do NOT use `c:PrimaryButton` here because it has a fixed gradient brush that would make two side-by-side primary actions look identical and visually heavy.)
- `StatusLabel` follows the same `BodySm + IsVisible=False` pattern as the receipt slice's error surface.
- Trailing icon on the `TopAppBar` is `Bell` (the only otherwise-unused icon in `Icons.cs`). It's purely cosmetic — there's no notification logic.
- Active tab is `Reports` — see Step 4 for the corresponding `AppTab` enum value.

**Color resource availability check:** verified against `Gymers/Resources/Styles/Colors.xaml`. The keys used here (`NavyDeep`, `NavyHeading`, `PaleBlue`, `TextMuted`, `Danger`, `BgApp`) all exist. There is no `Teal` resource — receipts hardcode `#0F766E` in PDF land; reports follow the brand navy in UI land.

- [ ] **Step 2: Create `Gymers/Pages/ReportsPage.xaml.cs`**

```csharp
using Gymers.Data;
using Gymers.Models;
using Gymers.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace Gymers.Pages;

public partial class ReportsPage : ContentPage
{
    readonly DataStore     _data;
    readonly ReportService _reports;

    ReportPeriod _period = ReportPeriod.Month;

    public ReportsPage(DataStore data, ReportService reports)
    {
        _data    = data;
        _reports = reports;
        InitializeComponent();

        WeekButton.Clicked  += (_, _) => SetPeriod(ReportPeriod.Week);
        MonthButton.Clicked += (_, _) => SetPeriod(ReportPeriod.Month);
        AllButton.Clicked   += (_, _) => SetPeriod(ReportPeriod.All);

        RevenuePdfButton.Clicked    += (_, _) => SharePdf(ReportKind.Revenue);
        RevenueCsvButton.Clicked    += (_, _) => ShareCsv(ReportKind.Revenue);
        AttendancePdfButton.Clicked += (_, _) => SharePdf(ReportKind.Attendance);
        AttendanceCsvButton.Clicked += (_, _) => ShareCsv(ReportKind.Attendance);
        RosterPdfButton.Clicked     += (_, _) => SharePdf(ReportKind.Roster);
        RosterCsvButton.Clicked     += (_, _) => ShareCsv(ReportKind.Roster);

        SetPeriod(_period);
    }

    void SetPeriod(ReportPeriod period)
    {
        _period = period;
        RefreshPeriodButtons();
        RefreshSummaries();
    }

    void RefreshPeriodButtons()
    {
        var pale  = (Color)Application.Current!.Resources["PaleBlue"];
        var navy  = (Color)Application.Current.Resources["NavyHeading"];
        var muted = (Color)Application.Current.Resources["TextMuted"];

        Paint(WeekButton,  _period == ReportPeriod.Week);
        Paint(MonthButton, _period == ReportPeriod.Month);
        Paint(AllButton,   _period == ReportPeriod.All);

        void Paint(Button b, bool active)
        {
            b.BackgroundColor = active ? pale  : Colors.Transparent;
            b.TextColor       = active ? navy  : muted;
        }
    }

    void RefreshSummaries()
    {
        RevenueSummary.Text    = _reports.Summarize(ReportKind.Revenue,    _period);
        AttendanceSummary.Text = _reports.Summarize(ReportKind.Attendance, _period);
        RosterSummary.Text     = _reports.Summarize(ReportKind.Roster,     _period);
    }

    async void SharePdf(ReportKind kind)
    {
        try
        {
            HideStatus();
            var path = await _reports.GeneratePdfAsync(kind, _period);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"Gymers — {kind.Label()} Report ({_period.Label()})",
                File  = new ShareFile(path)
            });
        }
        catch (Exception ex)
        {
            ShowError($"Couldn't generate PDF: {ex.Message}");
        }
    }

    async void ShareCsv(ReportKind kind)
    {
        try
        {
            HideStatus();
            var path = await _reports.GenerateCsvAsync(kind, _period);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"Gymers — {kind.Label()} Report ({_period.Label()}) [CSV]",
                File  = new ShareFile(path)
            });
        }
        catch (Exception ex)
        {
            ShowError($"Couldn't generate CSV: {ex.Message}");
        }
    }

    void ShowError(string text)
    {
        StatusLabel.Text = text;
        StatusLabel.TextColor = (Color)Application.Current!.Resources["Danger"];
        StatusLabel.IsVisible = true;
    }

    void HideStatus() => StatusLabel.IsVisible = false;
}
```

What this does:
- Constructor takes `DataStore` and `ReportService`; DI provides both because `MauiProgram` registers them (Task 4 + the pre-existing `DataStore` line).
- `SetPeriod` updates the field, recolors the period buttons, and recomputes summaries — invoked initially with `Month` and on each period button tap.
- `RefreshPeriodButtons` paints the active button with the `PaleBlue` resource — same color the BottomTabBar uses for active pills, so the visual idiom is consistent.
- `SharePdf` / `ShareCsv` are mirror images: try, generate, share; catch and show error in the red `StatusLabel`. They share no helper because the file extension and share-title strings differ enough that an abstraction would be more code.
- `Share.Default.RequestAsync` handles user-cancel without throwing, so no special cancel branch.

- [ ] **Step 3: Add the `Reports` ShellContent to `AppShell.xaml`**

Open `Gymers/AppShell.xaml`. Find the `<TabBar>` block:

```xml
<TabBar>
    <ShellContent Route="Dashboard"  ContentTemplate="{DataTemplate pages:DashboardPage}" />
    <ShellContent Route="Members"    ContentTemplate="{DataTemplate pages:MembersPage}" />
    <ShellContent Route="Payments"   ContentTemplate="{DataTemplate pages:PaymentsPage}" />
    <ShellContent Route="Attendance" ContentTemplate="{DataTemplate pages:AttendancePage}" />
</TabBar>
```

Add a fifth `ShellContent` line at the end:

```xml
<ShellContent Route="Reports"    ContentTemplate="{DataTemplate pages:ReportsPage}" />
```

The full `<TabBar>` block becomes:

```xml
<TabBar>
    <ShellContent Route="Dashboard"  ContentTemplate="{DataTemplate pages:DashboardPage}" />
    <ShellContent Route="Members"    ContentTemplate="{DataTemplate pages:MembersPage}" />
    <ShellContent Route="Payments"   ContentTemplate="{DataTemplate pages:PaymentsPage}" />
    <ShellContent Route="Attendance" ContentTemplate="{DataTemplate pages:AttendancePage}" />
    <ShellContent Route="Reports"    ContentTemplate="{DataTemplate pages:ReportsPage}" />
</TabBar>
```

- [ ] **Step 4: Add the `Reports` value to `AppTab` and the 5th pill handler in `BottomTabBar.xaml.cs`**

Replace the entire contents of `Gymers/Controls/BottomTabBar.xaml.cs` with:

```csharp
namespace Gymers.Controls;

public enum AppTab { Dashboard, Members, Payments, Attendance, Reports }

public partial class BottomTabBar : ContentView
{
    public static readonly BindableProperty ActiveTabProperty =
        BindableProperty.Create(nameof(ActiveTab), typeof(AppTab), typeof(BottomTabBar), AppTab.Dashboard,
            propertyChanged: (b, _, _) => ((BottomTabBar)b).ApplyActive());

    public AppTab ActiveTab
    {
        get => (AppTab)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    public BottomTabBar()
    {
        InitializeComponent();
        ApplyActive();
    }

    void ApplyActive()
    {
        var pale         = (Color)Application.Current!.Resources["PaleBlue"];
        var navyHeading  = (Color)Application.Current.Resources["NavyHeading"];
        var muted        = (Color)Application.Current.Resources["TextMuted"];

        DashboardPill.BackgroundColor  = ActiveTab == AppTab.Dashboard  ? pale : Colors.Transparent;
        MembersPill.BackgroundColor    = ActiveTab == AppTab.Members    ? pale : Colors.Transparent;
        PaymentsPill.BackgroundColor   = ActiveTab == AppTab.Payments   ? pale : Colors.Transparent;
        AttendancePill.BackgroundColor = ActiveTab == AppTab.Attendance ? pale : Colors.Transparent;
        ReportsPill.BackgroundColor    = ActiveTab == AppTab.Reports    ? pale : Colors.Transparent;

        DashboardGlyph.TextColor  = DashboardLabel.TextColor  = ActiveTab == AppTab.Dashboard  ? navyHeading : muted;
        MembersGlyph.TextColor    = MembersLabel.TextColor    = ActiveTab == AppTab.Members    ? navyHeading : muted;
        PaymentsGlyph.TextColor   = PaymentsLabel.TextColor   = ActiveTab == AppTab.Payments   ? navyHeading : muted;
        AttendanceGlyph.TextColor = AttendanceLabel.TextColor = ActiveTab == AppTab.Attendance ? navyHeading : muted;
        ReportsGlyph.TextColor    = ReportsLabel.TextColor    = ActiveTab == AppTab.Reports    ? navyHeading : muted;
    }

    async void OnDashboardTapped(object? sender, TappedEventArgs e)  => await Shell.Current.GoToAsync("//Dashboard");
    async void OnMembersTapped(object? sender, TappedEventArgs e)    => await Shell.Current.GoToAsync("//Members");
    async void OnPaymentsTapped(object? sender, TappedEventArgs e)   => await Shell.Current.GoToAsync("//Payments");
    async void OnAttendanceTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("//Attendance");
    async void OnReportsTapped(object? sender, TappedEventArgs e)    => await Shell.Current.GoToAsync("//Reports");
}
```

Three things changed:
- `AppTab` enum gained a `Reports` value. (`ReportsPage.xaml` uses `ActiveTab="Reports"` — it relies on this.)
- `ApplyActive` paints the new `ReportsPill` and `ReportsGlyph` / `ReportsLabel` named children.
- New `OnReportsTapped` handler navigates the shell to `//Reports`.

- [ ] **Step 5: Add the 5th pill to `BottomTabBar.xaml`**

Open `Gymers/Controls/BottomTabBar.xaml`. Two edits.

**Edit A — change the `Grid.ColumnDefinitions` to add a fifth column:**

Find this line:

```xml
<Grid ColumnDefinitions="*,*,*,*" Padding="16,12,16,32">
```

Replace with:

```xml
<Grid ColumnDefinitions="*,*,*,*,*" Padding="16,12,16,32">
```

**Edit B — add the `Reports` pill block after the existing Attendance pill block.**

The existing `Attendance` pill ends at the `</Border>` immediately before the closing `</Grid>` tag (its opening `<Border x:Name="AttendancePill" Grid.Column="3"`). Immediately after that closing `</Border>`, before the `</Grid>`, add:

```xml
<!-- Reports -->
<Border x:Name="ReportsPill" Grid.Column="4" StrokeThickness="0" Padding="16,6">
    <Border.StrokeShape>
        <RoundRectangle CornerRadius="999" />
    </Border.StrokeShape>
    <VerticalStackLayout Spacing="4" HorizontalOptions="Center">
        <Label x:Name="ReportsGlyph" FontFamily="{StaticResource FontLucide}"
               FontSize="18" HorizontalTextAlignment="Center"
               Text="{x:Static c:Icons.Bell}" />
        <Label x:Name="ReportsLabel" Style="{StaticResource LabelTab}"
               HorizontalTextAlignment="Center" Text="Reports" />
    </VerticalStackLayout>
    <Border.GestureRecognizers>
        <TapGestureRecognizer Tapped="OnReportsTapped" />
    </Border.GestureRecognizers>
</Border>
```

The full `<Grid>` should now contain five `<Border>` pill blocks (Dashboard, Members, Payments, Attendance, Reports), in that column order. Re-read the file to confirm structure before the build step.

`Bell` is reused as the Reports icon — none of the existing nine icons in `Icons.cs` is a perfect fit for "reports" and `Bell` is otherwise unreferenced. Cosmetic only; the team can swap to a real Lucide `bar-chart-3` glyph later by adding the codepoint to `Icons.cs`.

- [ ] **Step 6: Register `ReportsPage` in DI**

Open `Gymers/MauiProgram.cs`. Find the page registrations:

```csharp
builder.Services.AddTransient<Pages.LoginPage>();
builder.Services.AddTransient<Pages.DashboardPage>();
builder.Services.AddTransient<Pages.MembersPage>();
builder.Services.AddTransient<Pages.PaymentsPage>();
builder.Services.AddTransient<Pages.AttendancePage>();
```

Add a sixth line at the end:

```csharp
builder.Services.AddTransient<Pages.ReportsPage>();
```

So the page registration block reads:

```csharp
builder.Services.AddTransient<Pages.LoginPage>();
builder.Services.AddTransient<Pages.DashboardPage>();
builder.Services.AddTransient<Pages.MembersPage>();
builder.Services.AddTransient<Pages.PaymentsPage>();
builder.Services.AddTransient<Pages.AttendancePage>();
builder.Services.AddTransient<Pages.ReportsPage>();
```

- [ ] **Step 7: Build both targets**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: `0 Warning(s) 0 Error(s)`.

- [ ] **Step 8: Smoke-test app launch**

Use the run helper. Sign in `admin / admin123`. Verify:
- The bottom tab bar shows **five** pills: Dashboard, Members, Payments, Attendance, Reports.
- The `Bell` icon appears above "Reports."
- Tapping any of the four existing tabs still navigates correctly. Their pages still render their old content.

If the app fails to launch, the most likely cause is the BottomTabBar grid mismatch — re-verify that there are exactly five `<Border>` pill blocks inside a `ColumnDefinitions="*,*,*,*,*"` grid.

- [ ] **Step 9: Manual verify — Reports tab basic flow**

Tap the **Reports** pill. Verify:
- Page title reads "Reports."
- Period section shows three buttons (`Week`, `Month`, `All`). The `Month` button has the active `PaleBlue` background and `NavyHeading` text color; the other two have transparent background and muted text.
- Three cards render below "Reports" header: Revenue, Attendance, Member Roster. Each shows a non-`—` summary line (using the seed data the SQLite layer has already loaded).
- Tap `Week`. Active button changes; Revenue and Attendance summaries update; Roster summary unchanged.
- Tap `All`. Numbers grow. Tap `Month` to return.

- [ ] **Step 10: Manual verify — Revenue PDF + CSV**

With period = `Month`, on the Revenue card:

- Tap **Share PDF**. The macOS share sheet appears with title `Gymers — Revenue Report (Month)` and a PDF preview. Drag the PDF to Finder (Mac Catalyst's share sheet defaults don't include "Save to Files"; AirDrop / Mail / drag-out all work). Open in Preview. Verify:
  - Header band: `GYMERS` wordmark + "REVENUE" right-aligned + "Period: Month · Generated …" subtitle.
  - Column header row: `Date · Receipt · Member · Method · Amount`.
  - Body rows newest-first.
  - Bottom totals row: `Total: N payments · $X.XX` in bold.
- Tap **Share CSV**. Title reads `Gymers — Revenue Report (Month) [CSV]`. Save and open in Numbers (or `cat` from terminal). First line is `date,receipt,member_name,member_id,method,amount`. Subsequent lines are one per payment. Numbers parse cleanly (no `$` symbol, ISO dates).

- [ ] **Step 11: Manual verify — Attendance + Roster**

- Attendance card → Share PDF. Confirm header reads "ATTENDANCE", columns `Date · Time · Member · Member ID`, totals `Total: N check-ins · M unique members`.
- Attendance card → Share CSV. Header `date,time,member_name,member_id`.
- Roster card → Share PDF. Period subtitle reads `Snapshot · Generated …` (not "Period: Month"). Confirm pagination kicks in if member count is large enough; the seed has 6 members, so it's a single page — the pagination logic stays untested in seed conditions, which is acceptable for v1. Verify the "Member Roster is always a current snapshot" footnote is visible at the bottom of the page UI.
- Roster card → Share CSV. Header `id,name,tier,status,expires`.

- [ ] **Step 12: Manual verify — empty state**

Switch period to `Week`. With seed data, the Week window may legitimately have records (depending on seed timestamps). To force the empty state, manually wipe payments:

```bash
DB=$(find ~/Library/Containers -name "gymers.db3" 2>/dev/null | head -1)
sqlite3 "$DB" "DELETE FROM PaymentRow WHERE At < datetime('now', '-7 days');"
```

(Or simply switch to `Week` and use the seed as-is — most seeds are dated old enough that `Week` is empty.)

Relaunch. Tap Reports → period `Week` → Revenue card.
- Summary line reads `No payments in this period.`
- Tap Share PDF → opens. PDF body shows `No data for this period.` and a `Total: 0 payments · $0.00` totals row.
- Tap Share CSV → opens. CSV contains the header row only.

- [ ] **Step 13: Manual verify — member-removed fallback**

Quit the app:

```bash
pkill -f "Gymers/bin/Debug/net10.0-maccatalyst" 2>/dev/null; sleep 1
```

Delete one member that has at least one payment in the seed (`M-005` is a safe choice; if the seed mapping differs, pick another):

```bash
DB=$(find ~/Library/Containers -name "gymers.db3" 2>/dev/null | head -1)
sqlite3 "$DB" "DELETE FROM MemberRow WHERE Id = 'M-005';"
```

Relaunch. Sign in. Reports tab → period `All` → Revenue → Share PDF. The deleted member's payments now render with `(removed)` in the Member column. No crash. Same for Share CSV (the row's `member_name` field reads `(removed)`).

- [ ] **Step 14: Manual verify — receipt regression**

Confirm the predecessor slice still works:
- Payments tab → tap any row → share sheet opens with title `Gymers Receipt #…` → PDF preview correct.

If receipts broke after the new DI registration, the most likely cause is a typo in the `MauiProgram.cs` edit; re-verify.

- [ ] **Step 15: Restore the deleted member (cleanup)**

Wipe the DB so the next launch re-seeds:

```bash
DB=$(find ~/Library/Containers -name "gymers.db3" 2>/dev/null | head -1)
rm "$DB"
```

- [ ] **Step 16: Commit**

```bash
git add Gymers/Pages/ReportsPage.xaml Gymers/Pages/ReportsPage.xaml.cs \
        Gymers/AppShell.xaml \
        Gymers/Controls/BottomTabBar.xaml Gymers/Controls/BottomTabBar.xaml.cs \
        Gymers/MauiProgram.cs
git commit -m "feat(reports): ship Reports tab with PDF + CSV export

New 5th tab (BottomTabBar pill + AppShell ShellContent + DI
registration) routes to ReportsPage. Period selector
(Week / Month / All) recomputes per-card summaries; each card
exports to PDF or CSV via the system share sheet. Roster is a
period-independent snapshot."
```

---

## Task 6: Status doc — flip "Reports export" to Completed

After this task the status doc reflects the new shipped feature. The .docx is regenerated.

**Files:**
- Modify: `docs/status/build_status_docx.py`
- Modify: `docs/status/gymers-mobile-app-status-update.html`

- [ ] **Step 1: Move the bullet in `build_status_docx.py`**

Open `docs/status/build_status_docx.py`. Find the line in the `Ongoing Tasks` block:

```python
bullet("Reports export: KPI and attendance data are visible in the app, but a dedicated Reports screen with export/download is not yet implemented."),
```

Delete that line.

Find the existing `completed_rows` list assignment (search for `completed_rows = [`). Add a new row at the end of the list, immediately after the existing receipt PDF row:

```python
["Reports + export",
 "Completed",
 "A Reports tab generates Revenue, Attendance, and Member Roster reports for a chosen period (Week / Month / All). Each report can be shared as a multi-page PDF (UIKit's UIGraphicsPdfRenderer) or a CSV (plain UTF-8, RFC 4180-style quoting with LF line endings), via the system share sheet — save to Files, email, AirDrop, or open in Numbers."],
```

Also update the `Overall Status` paragraph (line ~114) to mention the Reports tab. Find:

```python
p("The Gymers project is a working .NET MAUI iOS app with a Mac Catalyst secondary target for fast local verification. The five core screens for the demo workflow (Login, Dashboard, Members, Payments, Attendance) are implemented and persist their state in a SQLite-backed DataStore. Tapping any row in Recent Payments now generates a one-page PDF receipt via UIKit's native PDF renderer and opens the system share sheet for save / email / print. The build succeeds with 0 warnings and 0 errors on both iOS and Mac Catalyst. Exportable reports and the trainer/workout/equipment/reports modules from the original scope are deferred to the next iteration."),
```

Replace with:

```python
p("The Gymers project is a working .NET MAUI iOS app with a Mac Catalyst secondary target for fast local verification. Six core screens (Login, Dashboard, Members, Payments, Attendance, Reports) are implemented and persist their state in a SQLite-backed DataStore. Tapping any row in Recent Payments generates a one-page PDF receipt via UIKit's native PDF renderer; the new Reports tab generates Revenue, Attendance, and Member Roster reports as multi-page PDF or CSV, all via the system share sheet. The build succeeds with 0 warnings and 0 errors on both iOS and Mac Catalyst. The trainer / workout / equipment modules from the original scope are deferred to the next iteration."),
```

- [ ] **Step 2: Mirror the change in `gymers-mobile-app-status-update.html`**

Open `docs/status/gymers-mobile-app-status-update.html`. Make the matching edits:

1. **Update the Overall Status `<p>`** under the `summary` div near the top of `<body>` to use the same revised paragraph from Step 1.
2. **Remove the `<li>` for "Reports export"** under the Ongoing Tasks heading.
3. **Add a new `<tr>` to the Completed Features `<table>`**, immediately after the receipt PDF row:

```html
<tr>
    <td>Reports + export</td>
    <td class="status-done">Completed</td>
    <td>A Reports tab generates Revenue, Attendance, and Member Roster reports for a chosen period (Week / Month / All). Each report can be shared as a multi-page PDF (UIKit's UIGraphicsPdfRenderer) or a CSV (plain UTF-8, RFC 4180-style quoting with LF line endings), via the system share sheet — save to Files, email, AirDrop, or open in Numbers.</td>
</tr>
```

(Re-read the existing rows first to confirm whether the `Status` cell uses `class="status-done"` or a plain `<td>`. Match the existing convention exactly.)

- [ ] **Step 3: Regenerate the .docx**

```bash
python3 docs/status/build_status_docx.py
```

Expected: a single line printing the absolute path to the regenerated `.docx`. (The `.docx` itself is gitignored — only the `.py` and `.html` are tracked.)

- [ ] **Step 4: Sanity-check the regenerated doc**

Open `docs/status/Gymers-Mobile-App-Status-Update.docx` and verify:
- Overall Status paragraph mentions Reports.
- Completed Features table has rows for both Receipt PDF and Reports + export.
- Ongoing Tasks bullet list no longer contains the Reports item.
- Other rows / bullets unchanged.

- [ ] **Step 5: Commit**

```bash
git add docs/status/build_status_docx.py docs/status/gymers-mobile-app-status-update.html
git commit -m "docs(status): mark Reports + export as completed

Slice landed. Reports tab generates Revenue, Attendance, and
Member Roster as PDF or CSV via the system share sheet. Status
doc updated: bullet moved from Ongoing to Completed, overall
status paragraph rewritten to mention the new tab."
```

---

## Task 7: Final Verification Walk

After Task 6 is committed, run the full demo end-to-end in one launch session — exercising payment record, receipt re-issue, all three reports, and the predecessor SQLite persistence — to prove the slice is complete and orthogonal regressions haven't crept in.

- [ ] **Step 1: Wipe the DB and report cache, then rebuild fresh**

```bash
DB=$(find ~/Library/Containers -name "gymers.db3" 2>/dev/null | head -1)
[ -n "$DB" ] && rm "$DB" && echo "Removed $DB" || echo "No DB to remove"
find ~/Library/Containers -path "*/reports/gymers-report-*" -delete 2>/dev/null
find ~/Library/Containers -path "*/receipts/gymers-receipt-*.pdf" -delete 2>/dev/null
pkill -f "Gymers/bin/Debug/net10.0-maccatalyst" 2>/dev/null; sleep 1
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
open Gymers/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Gymers.app
```

- [ ] **Step 2: First-launch seed verification**

Sign in `admin / admin123`. Verify:
- Five tabs visible in the BottomTabBar: Dashboard, Members, Payments, Attendance, Reports.
- **Members** — 6 seeded rows.
- **Payments** — 5 seeded rows newest-first; each row has a lime trailing chevron (receipt slice).
- **Attendance** — 6 seeded check-ins newest-first.
- **Reports** — three cards with non-`—` summaries; period defaults to `Month`.

- [ ] **Step 3: Receipt regression — seed re-issue**

Payments → tap `Receipt #1042` (Marcus Sterling, $99.00, Card). Share sheet opens with title `Gymers Receipt #1042`. Drag PDF out, open, verify fields. (The receipt slice still works.)

- [ ] **Step 4: Reports — Revenue end-to-end**

Reports → period `Month` → Revenue card.
- Summary line non-empty.
- Share PDF → share sheet title `Gymers — Revenue Report (Month)`. Open PDF; verify header band, column headers, body rows newest-first, bold totals row.
- Share CSV → share sheet title `Gymers — Revenue Report (Month) [CSV]`. Open CSV; verify header row + one row per payment, ISO dates, amounts as `0.00`.

- [ ] **Step 5: Reports — Attendance end-to-end**

Reports → Attendance.
- Share PDF → header reads "ATTENDANCE", columns `Date · Time · Member · Member ID`, totals `Total: N check-ins · M unique members`.
- Share CSV → header `date,time,member_name,member_id`.

- [ ] **Step 6: Reports — Roster end-to-end**

Reports → Roster.
- Share PDF → period subtitle reads `Snapshot · Generated …`. Members in alphabetical order. Totals `Total: N active · M other`.
- Share CSV → header `id,name,tier,status,expires`. Expires column ISO `yyyy-MM-dd`.

- [ ] **Step 7: Reports — period switching changes summaries**

Switch to `Week` (assuming seed leaves Week empty): Revenue summary reads `No payments in this period.`, Attendance similar, Roster unchanged.

Switch to `All`: numbers grow. Switch back to `Month`.

- [ ] **Step 8: Mutation + restart-survival**

Record a new payment: Payments → `Aisha Khan / 75 / cash` → success. Top row updates.

Reports → period `Month` → Revenue summary refreshes (note: the page only refreshes summaries on period change; tap `Month` again to force a refresh, or tap `Week` then `Month`). New payment is now reflected.

Quit and relaunch:

```bash
pkill -f "Gymers/bin/Debug/net10.0-maccatalyst" 2>/dev/null; sleep 1
open Gymers/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Gymers.app
```

Sign in. Reports → period `Month` → Revenue summary still includes the recorded payment (predecessor SQLite slice + this slice both confirmed).

- [ ] **Step 9: Validation regression sweep**

Confirm every other validation flow still works exactly as before. Each must behave identically to pre-slice:
- Login: empty fields → red `Enter username and password.` Wrong creds → red `Invalid credentials for the selected role.`
- Members: search `lena` → only Lena. Search `zzz` → empty-state notice.
- Payments: empty member → red name error. `Lena Park / 0 / card` → red amount error. `Lena Park / 25 / Crypto` → red method error.
- Attendance: empty search → red error.

If any regressed, the slice has accidentally broken something orthogonal — investigate before continuing.

- [ ] **Step 10: iOS target builds clean**

The slice's verification target is Mac Catalyst, but iOS-target builds must remain green:

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: `0 Warning(s) 0 Error(s)`.

- [ ] **Step 11: No-op — final verification is now complete**

If every box above is checked, the slice is done. The Task 6 commit is the final commit; no additional commit for verification.

---

## Self-review notes (for the implementer)

- **`UIGraphicsPdfRenderer.WritePdf` runs on the UI thread.** Same constraint as the receipt slice. Button click handlers are already on the UI thread, so no marshaling. If you ever wrap a generation call in `Task.Run`, marshal back to the main thread for the actual draw.
- **`Microsoft.iOS` bindings use Pascal case `Pdf`, not `PDF`** — the type is `UIGraphicsPdfRenderer`, not `UIGraphicsPDFRenderer`. Same for `UIGraphicsPdfRendererContext` and `UIGraphicsPdfRendererFormat`. Don't follow Apple's Objective-C casing.
- **Multi-page header redraw is mandatory.** Every `BeginPage()` starts a blank page; if you forget to call `DrawHeaderBand` after, the second page has no header. The pagination loop in `ReportDocument` handles this — don't restructure it without re-reading the receipt slice's failure mode notes.
- **`FooterReserve` is held back on every page.** This means a Roster with exactly enough members to fill one page might paginate to two pages, with the second page holding only the totals row. That's fine — strictly correct, no UX harm. If you see the totals row clipping the bottom margin on a borderline case, increase `FooterReserve` slightly (it's currently `60f`).
- **CSV must use invariant culture.** `decimal.ToString("0.00", CultureInfo.InvariantCulture)`, `DateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)`. Locale-dependent output breaks evaluator imports.
- **Don't write a UTF-8 BOM.** `new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))`. Default `StreamWriter(string)` constructors emit a BOM; Numbers handles it but Preview shows the BOM as a stray glyph.
- **Quoting rule:** quote any field that contains `,`, `"`, `\r`, or `\n`. Inside a quoted field, every `"` becomes `""`. No other escaping. Don't quote-everything-by-default — clutters the file.
- **Don't bind summaries to `INotifyPropertyChanged`.** They're imperative `Label.Text` writes triggered by period changes. We don't subscribe to `DataStore.Payments.CollectionChanged` here — the page is short-lived (transient DI lifetime, fresh instance on every tab navigation), so any data added since last navigation is reflected on next open.
- **`TabBar` 5-tab cap.** iOS bottom TabBar comfortably handles 5 pills (the 6th would force a "More" overflow). 5 is the max we want — don't add a 6th in the next slice without rethinking the IA.
- **`Bell` icon is a placeholder.** Lucide has `bar-chart-3`, `file-text`, `pie-chart` etc. that better signal "reports", but adding a real codepoint requires looking it up in the lucide-static info.json and verifying the glyph renders. `Bell` ships as-is; treat as a future polish swap.
- **Filename collisions within the same day are intentional.** Re-tapping `Share PDF` on the same period twice on the same day overwrites the file. The user expects "current data," not "this morning's snapshot." The cache directory is OS-managed; don't add a versioning scheme.
- **Smoke-test before claiming the slice green.** New DI registration + new tab + new page = three places startup can crash. `dotnet build` won't catch any of them. The receipt slice's QuestPDF crash slipped through 4 commits because no one launched the app between tasks; don't repeat that.
- **No tests.** Verification is manual, matching the receipt slice. Don't add xUnit. The UIKit + StreamWriter primitives are battle-tested; we'd be re-testing them.
- **iOS sim:** don't try to verify on the iOS simulator. It's unusable on this hardware. Mac Catalyst is the verification target. iOS-target builds must still succeed (Step 10 of Task 7 covers that).
