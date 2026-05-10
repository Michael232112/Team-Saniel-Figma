# Gymers Mobile App — Reports + Export Slice — Design

**Date:** 2026-05-10
**Status:** Approved, pre-implementation

**Goal:** Add a Reports tab where staff/admin can pick a period (Week / Month / All) and share three pre-canned reports — Revenue, Attendance, Member Roster — as **PDF** or **CSV** through the system share sheet. Reports re-render deterministically from SQLite, so any historical state can be re-exported. Mac Catalyst is the verification target; iOS-target builds must remain green.

**Currency convention:** the existing Receipt PDF and on-screen UI use `$` and US-style decimals throughout (see `ReceiptDocument.cs`). Reports follow the same convention — never `₱` or any other symbol — so the visual story stays consistent across receipts and reports.

**Tech Stack:** .NET 10, MAUI, C# 12, XAML. UIKit/Foundation/CoreGraphics for PDF (built into the iOS + Mac Catalyst SDKs — no NuGet). Plain `StreamWriter` for CSV.

**Predecessor:** `docs/superpowers/specs/2026-05-10-receipt-pdf-design.md` (proved the UIGraphicsPdfRenderer + Share.Default round-trip end-to-end on Mac Catalyst).

---

## 1. Scope

In scope:

- A new top-level **Reports** tab (5th tab on the TabBar), available to both `admin` and `staff` roles.
- Period selector: `Week` (last 7 days), `Month` (last 30 days), `All` (everything in DataStore). Three-segment control at the top of the page.
- Three report cards rendered as `ListRow`-styled tiles, each with a one-line summary that recomputes when the period changes:
  1. **Revenue** — total ₱ amount and payment count for the period.
  2. **Attendance** — total check-in count and unique-member count for the period.
  3. **Member Roster** — total active vs. expired count (period-independent snapshot).
- Each card has two trailing buttons: **Share PDF** and **Share CSV**. Tapping either generates the file under `FileSystem.CacheDirectory/reports/` and hands it to `Microsoft.Maui.ApplicationModel.DataTransfer.Share`.
- PDF supports multi-page output (Roster commonly exceeds one page).
- Filenames embed kind, period, and date so the share sheet's title and the saved filename are self-describing: `gymers-report-revenue-month-20260510.pdf`.

Out of scope:

- Custom date ranges or date pickers — three presets are enough for the demo.
- Charts of any kind — PDF tables only.
- In-app preview before sharing — share sheet's native preview is enough.
- Per-member drill-downs (e.g., "show all payments for Marcus") — separate slice.
- Saved / scheduled / emailed reports — share sheet covers Mail / AirDrop / Notes natively.
- Report history list (re-share previous runs) — every tap re-generates from current SQLite state.
- Role gating on Reports — both `admin` and `staff` see it, matching how every other tab works today.
- New models, new tables, new sample data — reports operate on existing `DataStore` collections.

---

## 2. Library and triggering

### Library: **`UIKit.UIGraphicsPdfRenderer` + plain `StreamWriter`**

PDF: same library that ships receipts (commit `4ba6343`). Native UIKit, ships with the OS, no NuGet, no SkiaSharp. Already proven to work on `net10.0-ios` and `net10.0-maccatalyst`. Multi-page is a matter of calling `ctx.BeginPage()` between pages — same `UIGraphicsPdfRendererContext` API, no extra dep.

CSV: a ~30-line `CsvWriter` helper using `StreamWriter` with UTF-8 (no BOM — keeps macOS Numbers / Preview happy; Excel-on-Windows will still open it). Quotes any field containing `,`, `"`, `\r`, or `\n`; doubles internal quotes. No new NuGet (`CsvHelper` would be overkill for three fixed schemas).

### Trigger: **Per-card share button**

Each card has two buttons (`Share PDF`, `Share CSV`). Buttons fire dedicated handlers on `ReportsPage` that call `ReportService.GeneratePdfAsync(kind, period)` or `GenerateCsvAsync(kind, period)`, then `Share.Default.RequestAsync(...)`.

We could have used a single `Share` button with a popup picker — rejected because the two-button form is one tap shorter and the cards have room.

---

## 3. UI layout

```
┌────────────────────────────────────┐
│ Reports                            │   ← page title (matches Members / Payments)
│                                    │
│ Period:  [ Week | Month | All ]    │   ← segmented control, "Month" default
│                                    │
│ ┌─ Revenue ───────────────────────┐│
│ │ $24,500 from 18 payments        ││   ← summary recomputes on period change
│ │  [Share PDF]   [Share CSV]      ││
│ └─────────────────────────────────┘│
│ ┌─ Attendance ────────────────────┐│
│ │ 142 check-ins, 47 unique members││
│ │  [Share PDF]   [Share CSV]      ││
│ └─────────────────────────────────┘│
│ ┌─ Member Roster ─────────────────┐│
│ │ 64 active, 7 expired            ││   ← period selector hidden / greyed; snapshot
│ │  [Share PDF]   [Share CSV]      ││
│ └─────────────────────────────────┘│
└────────────────────────────────────┘
```

Visual reuse:

- Page chrome: copy `MembersPage.xaml`'s header pattern (page title + body padding) — fastest path to consistency.
- Cards: hand-built `Border` (matches the brand teal/navy/grey palette already used on Dashboard cards). They are NOT `ListRow` instances — `ListRow` is a tap-on-the-row primitive; we want a card with its own internal buttons. Mirroring its visual style (corner radius, padding, divider) is enough.
- Period selector: `RadioButton`s in a horizontal `HorizontalStackLayout`, styled as a segmented control via `ControlTemplate` (the simplest cross-platform path on MAUI 10 — no third-party segmented control needed).
- Buttons: standard MAUI `Button` with the existing teal accent color from `App.xaml` resources (`TealAccent`) — same buttons used in `PaymentsPage` for "Record".

Roster card: the period control still works (it's page-level), but Roster's summary line and exports ignore the period. We do *not* grey out the period selector when looking at the Roster card — it would imply the selector is per-card, which it isn't. We just make the selector's effect on Roster a no-op and document it once in the body footer text: "Member Roster is always a current snapshot."

---

## 4. Report contents

### 4.1 Revenue (period-filtered)

Filter: `Payments.Where(p => p.At >= from && p.At < to)` ordered newest-first.

PDF columns: `Date · Receipt # · Member · Method · Amount`. Last column right-aligned. Bottom row: bold total.

CSV columns: `date,receipt,member_name,member_id,method,amount`. Date as ISO `yyyy-MM-dd HH:mm:ss`. Amount as plain decimal (`99.00`), no currency symbol.

Member name lookup: `DataStore.Members.FirstOrDefault(m => m.Id == p.MemberId)?.Name ?? "(removed)"`.

### 4.2 Attendance (period-filtered)

Filter: `CheckIns.Where(c => c.At >= from && c.At < to)` ordered newest-first.

PDF columns: `Date · Time · Member · Member ID`. Bottom row: bold total check-ins + unique member count.

CSV columns: `date,time,member_name,member_id`. Date as `yyyy-MM-dd`, time as `HH:mm:ss`.

### 4.3 Member Roster (period-independent)

All `Members`, ordered by `Name`.

PDF columns: `ID · Name · Tier · Status · Expires`. Bottom row: bold active vs. expired counts.

CSV columns: `id,name,tier,status,expires`. Expires as ISO `yyyy-MM-dd`.

---

## 5. Architecture

### Components

- **`ReportPeriod`** — enum `{ Week, Month, All }`, plus an extension method:
  ```csharp
  public static (DateTime From, DateTime To) Range(this ReportPeriod p, DateTime now);
  ```
  `Week` → `(now.AddDays(-7), now.AddSeconds(1))`. `Month` → `(now.AddDays(-30), now.AddSeconds(1))`. `All` → `(DateTime.MinValue, DateTime.MaxValue)`. The `+1s` upper bound is so a payment recorded "right now" passes a strict `< to` predicate without timestamp-equality hazards.

- **`ReportKind`** — enum `{ Revenue, Attendance, Roster }`.

- **`ReportService`** — DI singleton. Three public methods:
  ```csharp
  Task<string> GeneratePdfAsync(ReportKind kind, ReportPeriod period);
  Task<string> GenerateCsvAsync(ReportKind kind, ReportPeriod period);
  string Summarize(ReportKind kind, ReportPeriod period);  // sync, returns the one-line summary text
  ```
  Service depends on `DataStore`. Internally creates the cache dir, builds a `ReportDocument` (PDF) or invokes `CsvWriter.Write*` (CSV), returns the absolute path. `Task<string>` return is preserved for symmetry with `ReceiptService` even though the work is synchronous on the UI thread.

- **`ReportDocument`** — pure layout class. Constructor takes `(ReportKind kind, ReportPeriod period, IReadOnlyList<Member> members, IReadOnlyList<Payment> payments, IReadOnlyList<CheckIn> checkIns, DateTime generatedAt)`. `WritePdf(string path)` constructs a `UIGraphicsPdfRenderer` and writes one or more pages depending on row count. Page break logic: track `y`; before drawing a row, if `y + rowHeight > PageHeight - Margin - footerReserve`, call `ctx.BeginPage()`, redraw header band, reset `y`. Same color tokens as `ReceiptDocument` so the brand stays consistent.

- **`CsvWriter`** — static helper. Three methods, one per kind:
  ```csharp
  static void WriteRevenue(string path, IEnumerable<(Payment, Member?)> rows);
  static void WriteAttendance(string path, IEnumerable<(CheckIn, Member?)> rows);
  static void WriteRoster(string path, IEnumerable<Member> rows);
  ```
  Common `Quote(string)` private: returns `"…"` if the value contains `,`, `"`, `\r`, or `\n`; doubles internal `"`. Always writes `\n` line endings (CSV RFC 4180 says CRLF, but every consumer we care about — Numbers, Excel-modern, Sheets, Preview — handles LF fine and the file is smaller).

- **`ReportsPage`** — `ContentPage`. Constructor takes `(DataStore, ReportService)`. Holds a `ReportPeriod` field defaulting to `Month`. On period change: re-call `Summarize` for each kind, update three summary `Label`s. On a button tap: call the appropriate `GenerateXxxAsync`, then `Share.Default.RequestAsync(new ShareFileRequest { File = new ShareFile(path), Title = $"Gymers — {kindLabel} Report ({periodLabel})" })`. Errors render via the same red-status-label pattern used on `PaymentsPage`.

- **`AppShell.xaml`** — appends a 5th `ShellContent` to the existing `<TabBar>`:
  ```xml
  <ShellContent Route="Reports" ContentTemplate="{DataTemplate pages:ReportsPage}" />
  ```
  No icon properties (matching the existing four tabs, which also don't set icons in the shell).

### File path

`{FileSystem.CacheDirectory}/reports/gymers-report-{kind}-{period}-{yyyyMMdd}.{pdf|csv}`

- Cache dir means the OS can reclaim it; we never assume long-term presence.
- Filename encodes everything the user might need to identify the file after it's saved.
- File is unconditionally overwritten on every tap (taps within the same calendar day collide on filename — that's fine; the data is regenerated from current SQLite state, and the user expects "Share PDF" to mean "give me the current report", not "give me this morning's report").

### DI wiring

In `MauiProgram.cs`:

```csharp
builder.Services.AddSingleton<ReportService>();
builder.Services.AddTransient<ReportsPage>();
```

`ReportsPage` is registered transient (matches the existing pattern for the other four pages).

---

## 6. File layout

| File                                                     | Action |
| -------------------------------------------------------- | ------ |
| `Gymers/MauiProgram.cs`                                  | Modify — register `ReportService` and `ReportsPage` |
| `Gymers/AppShell.xaml`                                   | Modify — append Reports `ShellContent` to TabBar |
| `Gymers/Models/ReportPeriod.cs`                          | Create — enum + `Range` extension |
| `Gymers/Models/ReportKind.cs`                            | Create — enum |
| `Gymers/Services/ReportService.cs`                       | Create |
| `Gymers/Services/ReportDocument.cs`                      | Create — multi-page PDF layout |
| `Gymers/Services/CsvWriter.cs`                           | Create — CSV helper |
| `Gymers/Pages/ReportsPage.xaml`                          | Create |
| `Gymers/Pages/ReportsPage.xaml.cs`                       | Create |
| `docs/status/build_status_docx.py`                       | Modify — move "Reports export" to completed |
| `docs/status/gymers-mobile-app-status-update.html`       | Modify — mirror status doc change |

Eleven files. Zero new packages. No model edits to existing records, no DB schema change.

---

## 7. Error handling

Five failure modes, all surfaced via a red `StatusLabel` on `ReportsPage` (same pattern as `PaymentsPage`):

1. **Empty result set** (e.g., Week period with no payments) → still generate; PDF has a header + body line `No data for this period.`; CSV has the header row only. No error UI — empty is a valid answer.
2. **Member-not-found while expanding payment/check-in row** → render `(removed)` in the name slot, `—` for ID. No error UI.
3. **PDF write failure** (disk full, sandbox anomaly) → `UIGraphicsPdfRenderer.WritePdf` returns `NSError`; `ReportDocument` rethrows as `InvalidOperationException`; page handler catches and shows `Couldn't generate report: {ex.Message}`.
4. **CSV write failure** (same root causes) → caught the same way.
5. **Share sheet fails to open** (no available targets — extremely unusual) → same red status label.

User-cancelled share is not an error: `Share.Default.RequestAsync` completes normally on cancel.

---

## 8. Verification

Build matrix:

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Both must end with `0 Warning(s) 0 Error(s)`.

**Mandatory: actually launch the app on Mac Catalyst before claiming green.** Adding a new tab + DI registration is exactly the kind of change that can break startup without breaking the build. Smoke-test sequence (already a project rule per memory):

1. `pkill -f "Gymers/bin/Debug/net10.0-maccatalyst" 2>/dev/null; sleep 1`
2. `open Gymers/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Gymers.app`
3. Wait 4 seconds, confirm the process is alive.

Mac Catalyst manual sweep (single launch session):

1. **Tab visibility** — sign in `admin / admin123`. TabBar shows five tabs; tap Reports. Page title renders, period selector defaults to `Month`, all three cards show non-`—` summary text.
2. **Period switching** — switch to `Week`. Revenue and Attendance summaries update; Roster summary unchanged. Switch to `All`. Numbers grow. Switch back to `Month`.
3. **Revenue PDF** — tap Revenue → Share PDF. Share sheet title `Gymers — Revenue Report (Month)`. Drag PDF to Finder, open in Preview. Header band, period subtitle, table of payments newest-first, bold total at the bottom. Multi-page if the row count exceeds the per-page limit (~25 rows).
4. **Revenue CSV** — same card → Share CSV. Filename ends `.csv`. Open in Numbers / Preview. Header row plus one row per payment. Numbers parse as numbers (no currency symbol).
5. **Attendance PDF + CSV** — same checks for the Attendance card with `Month` period. Confirm unique-member count in the bottom summary matches a manual count.
6. **Roster PDF** — tap Roster → Share PDF. Period-independent — all 64+ members in alphabetical order. Confirm pagination kicks in (≥ 2 pages). Bold counts at the bottom of the last page.
7. **Roster CSV** — same card → Share CSV. Open and confirm one row per member, expires column ISO-formatted.
8. **Empty state** — switch period to `Week`, but first manually wipe recent rows (or pick a fresh database where `Week` would be empty). Tap Revenue → Share PDF → "No data for this period." line in the body. CSV has header row only.
9. **Member-removed fallback** — `sqlite3 gymers.db3 "DELETE FROM Members WHERE Id='M-005';"`, relaunch, generate Revenue PDF → that member's payments appear with `(removed)` in the name column. No crash.
10. **Restart-survival** — quit (`pkill`) and relaunch. Reports tab still works; numbers still match what SQLite holds.
11. **Regression sweep** — Login still works; Members search/check-in still works; Payments record-and-share-receipt still works (the new `ReportService` registration doesn't shadow `ReceiptService`); Dashboard still renders.

iOS-target sim verification is skipped (sim is unusable on this hardware). iOS-target builds must remain green; that's covered by the build matrix.

---

## 9. Status doc

After verification:

- Move the **"Reports export"** bullet from Ongoing Tasks to a new row in `completed_rows` in `docs/status/build_status_docx.py`.
- Mirror the change in `docs/status/gymers-mobile-app-status-update.html`.
- Regenerate the .docx via `python3 docs/status/build_status_docx.py`.

Suggested completed-row text:

> "Reports + export: a Reports tab generates Revenue, Attendance, and Member Roster reports for a chosen period (Week / Month / All). Each report can be shared as a multi-page PDF (UIKit's UIGraphicsPdfRenderer) or a CSV (plain UTF-8, RFC 4180–style quoting with LF line endings), via the system share sheet — save to Files, email, AirDrop, or open in Numbers."

---

## 10. Self-review notes (for the implementer)

- **`UIGraphicsPdfRenderer.WritePdf` runs on the UI thread.** Same constraint as the receipt slice. The button click handler is already on the UI thread, so no marshaling. If you ever wrap a generation call in `Task.Run`, marshal back to the main thread for the actual draw.
- **Multi-page pagination requires re-drawing the header on each page.** Inside `ReportDocument.DrawTable`, before every row check `if (y + rowHeight > PageHeight - Margin - footerReserve) { ctx.BeginPage(); DrawPageHeader(ctx); y = headerBottom; }`. `footerReserve` is the height needed by the bold totals row at the bottom of the report (~40pt) — reserve it on **every** page even though the totals only render on the last one. Reserving uniformly avoids tracking which page is "last" mid-loop, costs at most one extra page break in pathological cases, and keeps the layout function pure.
- **CSV must use invariant culture for numbers and dates.** `decimal.ToString("0.00", CultureInfo.InvariantCulture)`, `DateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)`. Locale-dependent output breaks evaluator imports.
- **Don't write a UTF-8 BOM.** `new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))`. Default `StreamWriter` constructors emit a BOM; Numbers handles it but Preview shows the BOM as a stray glyph.
- **Quoting rule:** quote any field that contains `,`, `"`, `\r`, or `\n`. Inside a quoted field, every `"` becomes `""`. No other escaping. Don't quote-everything-by-default — clutters the file.
- **TabBar 5-tab cap.** iOS bottom TabBar comfortably handles 5 tabs (the 6th forces a "More" overflow). 5 is the max we want — don't add a 6th in the next slice without rethinking the IA.
- **`RadioButton` segmented styling on MAUI 10.** Uses a `ControlTemplate` that wraps the radio in a `Border` and toggles colors via `VisualStateManager`. Reference: existing teal/pale palette in `App.xaml`. If the styling fight gets long, ship with three plain `Button`s and visual selection state — both pass evaluator scrutiny; prefer whatever ships fastest.
- **Don't generate immediately on tab-load.** Summaries (`ReportService.Summarize`) are cheap and run on tab open / period change. PDF/CSV generation only runs on explicit button tap. Pre-generating "just in case" wastes cycles and confuses the cache (stale files for unchanged data).
- **Filename collisions within the same day are intentional.** Re-tapping "Share PDF" on the same period twice on the same day overwrites the file. The user expects the freshest data, not a versioned trail. If we ever need a trail, add a timestamp suffix — but not in this slice.
- **Smoke-test before claiming the slice green.** New DI registration + new tab + new page = three places startup can crash. `dotnet build` won't catch any of them.
- **Don't extend `ListRow` for the cards.** `ListRow` is for tap-on-the-row interactions and would force buttons into a slot that doesn't exist. Hand-built `Border` cards are simpler and don't pollute `ListRow` with two-button affordances no other consumer needs.
