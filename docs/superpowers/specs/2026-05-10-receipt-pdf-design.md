# Gymers Mobile App — Receipt PDF Generation Slice — Design

**Date:** 2026-05-10
**Status:** Approved & implemented (library pivoted same day — see §2)

**Goal:** Tap a row in Recent Payments → generate a one-page PDF receipt → open the system share sheet so the user can save, email, AirDrop, or print it. The receipt re-renders deterministically from SQLite, so any historical payment (including seeds) can be re-issued. Mac Catalyst is the verification target; iOS-target builds must remain green.

**Tech Stack:** .NET 10, MAUI, C# 12, XAML. UIKit/Foundation/CoreGraphics (built into the iOS + Mac Catalyst SDKs — no NuGet).

**Predecessor:** `docs/superpowers/specs/2026-05-09-sqlite-persistence-design.md` (made payments persistent so re-issuing past receipts is meaningful).

---

## 1. Scope

In scope:

- Generate a one-page PDF receipt for a single `Payment` + its `Member`.
- Trigger from tapping any row in Recent Payments on `PaymentsPage`.
- Hand the resulting file to the system share sheet (`Microsoft.Maui.ApplicationModel.DataTransfer.Share`).
- Cache the file under `FileSystem.CacheDirectory/receipts/` so the OS can reclaim it; we never depend on the file outliving the share-sheet interaction.

Out of scope:

- Email-from-app (system share sheet covers this natively).
- Bulk receipt export, PDF history list, or "re-print last receipt" shortcuts.
- In-app PDF preview before sharing — share sheet's native preview is enough.
- Logo image asset — text wordmark only.
- Currency localization — always `$xx.xx`, matching the rest of the UI.
- Receipt for check-ins, member cards, or anything other than a single Payment.
- Auto-generation immediately after `RecordPayment` succeeds — explicit, retryable, predictable wins.

---

## 2. Library and triggering

### Library: **`UIKit.UIGraphicsPdfRenderer` (built into iOS + Mac Catalyst SDK)**

- Native UIKit PDF renderer — ships with the OS, no NuGet, no SkiaSharp.
- Works on both `net10.0-ios` and `net10.0-maccatalyst` because Mac Catalyst exposes the iOS UIKit surface natively.
- Renders text via `Foundation.NSString.DrawString` + `UIStringAttributes`, lines via `CoreGraphics.CGContext`.
- Output is a real PDF (PDF 1.4) with selectable text in Preview / iOS Files / Mail.
- Drawing must run on the UI thread (`UIKit Consistency error` otherwise) — caller already on the UI thread, no marshaling needed.

**History:** Originally specified as **QuestPDF 2026.5.0 (Community license)** — chosen for its fluent layout API. Discovered at runtime that QuestPDF's SkiaSharp transitive dep ships only `osx-x64`, `osx-arm64`, `linux-*`, `win-*` runtimes — no `maccatalyst-arm64` slice. The static initializer on `QuestPDF.Settings` throws `'Your runtime is not supported by QuestPDF. Detected: other-arm64'`, crashing the app at startup before the Login page renders. Build was green because the cctor only fires when QuestPDF.Settings is accessed. iOS would hit the same wall at runtime. Replaced with UIGraphicsPdfRenderer same-day; commit `4ba6343`.

Other rejected:

- `PdfSharpCore` — same Apple-platform packaging risk as QuestPDF.
- `iTextSharp` — AGPL; bad fit even for school projects.
- Hand-rolled PDF byte writer — pointless complexity.

### Trigger: **Tap a payment row**

- Each row in Recent Payments becomes tappable. The trailing chevron — already a `ListRow` feature gated by a `TrailingChevron` boolean — gets flipped on for payment rows to signal interactivity.
- `Members` and `Attendance` rows are unaffected (they don't subscribe to the new event).

Rejected: auto-generate-on-record. If PDF generation fails, the payment is already in SQLite; the partial-success UX is confusing. Explicit means a failed generate is just a retry, not a "is my payment recorded?" panic.

---

## 3. Receipt layout

One A4 page (595 × 842 pt), single-column. Brand tokens lifted from the existing app: teal accent `#0F766E`, navy heading `#18212F`, muted body grey `#667085`, divider `#E2E8F0`. UIKit system font (San Francisco on Mac Catalyst, San Francisco on iOS) — no custom font registration; receipts are documents, not branded UI.

```
┌────────────────────────────────────────────────┐
│  GYMERS                              RECEIPT   │   ← header band, teal accent
│  Gym Management System                         │
│ ────────────────────────────────────────────── │
│                                                │
│  Receipt #1043          May 9, 2026 · 7:42 PM  │   ← bold receipt #, right-aligned date
│                                                │
│  Member                                        │   ← subhead, teal
│   Marcus Sterling                              │
│   ID: M-001 · Premium tier                     │
│                                                │
│  Payment                                       │
│   Amount         $99.00                        │
│   Method         Card                          │
│                                                │
│ ────────────────────────────────────────────── │
│  Thank you for being a Gymers member.          │
│  This receipt was issued by the Gymers app.    │
└────────────────────────────────────────────────┘
```

Only data in the receipt: receipt number, timestamp, member name, member id, member tier, amount, method. All read directly from `Payment` and `Member` records — no DB hits beyond what `DataStore` already exposes.

---

## 4. Architecture

### Components

- **`ReceiptService`** — DI singleton. One public method:
  ```csharp
  Task<string> GenerateAsync(Payment p, Member? m);
  ```
  Returns the absolute path of the written PDF wrapped in a completed `Task` (kept `Task<string>` so callers can `await` consistently with the rest of the data layer). Internally creates the cache directory, builds a `ReceiptDocument`, calls `doc.WritePdf(path)` synchronously on the UI thread (UIKit drawing must run on the UI thread; the caller `OnRowTapped` is already there).

- **`ReceiptDocument`** — pure layout class. Constructor takes `(Payment, Member?)`. `WritePdf(string path)` constructs a `UIGraphicsPdfRenderer` over a 595×842 CGRect and calls its `WritePdf(NSUrl, Action<UIGraphicsPdfRendererContext>, out NSError)` overload. Inside the draw closure: `BeginPage()`, then header / body / footer drawing via `NSString.DrawString` + `CGContext.StrokePath` for dividers.

- **`PaymentsPage`** — gains an `OnRowTapped(object?, EventArgs)` handler wired to the `ListRow.Tapped` event when each row is built imperatively in `Render()`. The handler resolves the `Payment` from the row's `CommandParameter`, looks up the member (may be null), calls `ReceiptService.GenerateAsync`, then `Share.Default.RequestAsync(new ShareFileRequest { File = new ShareFile(path), Title = $"Gymers Receipt #{p.ReceiptNumber}" })`.

- **`ListRow`** — extended (small, additive) with a `Tapped` event (plain `EventHandler`, not generic) and a `CommandParameter` BindableProperty. Inner `Border` wraps a `TapGestureRecognizer` that re-fires `Tapped`. Other consumers ignore the event and don't set `CommandParameter`, so behavior is unchanged for Members and Attendance rows.

### File path

`{FileSystem.CacheDirectory}/receipts/gymers-receipt-{ReceiptNumber}.pdf`

- Cache dir means the OS can reclaim it; we never assume long-term presence.
- Receipt number in the filename makes the share sheet's title and the saved filename match what the user saw on screen — no UUID confusion.
- File is unconditionally overwritten on each tap (no append / no dedupe / no race-window concerns; `UIGraphicsPdfRenderer.WritePdf` opens with `FileMode.Create` semantics).

### DI wiring

In `MauiProgram.cs`:

```csharp
builder.Services.AddSingleton<ReceiptService>();
```

`PaymentsPage` already takes `DataStore` via constructor injection; we add `ReceiptService` to its constructor.

---

## 5. File layout

| File | Action |
| ---- | ------ |
| `Gymers/MauiProgram.cs` | Modify — register `ReceiptService` |
| `Gymers/Services/ReceiptService.cs` | Create |
| `Gymers/Services/ReceiptDocument.cs` | Create |
| `Gymers/Controls/ListRow.xaml` | Modify — add inner `TapGestureRecognizer` |
| `Gymers/Controls/ListRow.xaml.cs` | Modify — add `Tapped` event + `CommandParameter` BindableProperty |
| `Gymers/Pages/PaymentsPage.xaml.cs` | Modify — wire row taps to `OnRowTapped`, flip `TrailingChevron=true`, set `CommandParameter` per row |

Six files. Zero new packages. No model or DB changes. (csproj is unmodified — the originally-added QuestPDF reference was removed when the library was swapped.)

---

## 6. Error handling

Three failure modes, all surfaced via the existing `StatusLabel` red-error pattern on `PaymentsPage`:

1. **Member not found for the row's `MemberId`** (e.g., a deleted member) → render the receipt with `(member removed)` in the name slot, `—` for ID and tier. No error UI; the receipt is still valid history.
2. **PDF write failure** (rare; disk full, sandbox permission anomaly) → `UIGraphicsPdfRenderer.WritePdf` returns an `NSError` via its `out` parameter; `ReceiptDocument` rethrows as `InvalidOperationException` with the localized description, caught by `PaymentsPage.OnRowTapped`'s try/catch → red status label `Couldn't generate receipt: {ex.Message}`.
3. **Share sheet fails to open** (no available share targets — extremely unusual) → same red status label pattern via the same try/catch.

Cancelled-by-user is not an error: the share API completes normally regardless.

---

## 7. Verification

Build matrix:

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Both must end with `0 Warning(s) 0 Error(s)`. (The 19 warnings observed during the QuestPDF-era builds are gone after the library swap.)

**Mandatory: actually launch the app on Mac Catalyst before claiming green.** A green build does not catch runtime cctor failures (the lesson learned from the QuestPDF pivot). Smoke-test sequence:

1. `pkill -f "Gymers/bin/Debug/net10.0-maccatalyst" 2>/dev/null; sleep 1`
2. `open Gymers/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Gymers.app`
3. Wait 4 seconds, confirm the process is alive (`ps`-check or screenshot).

Mac Catalyst manual sweep (single launch session, after wiping any previous receipts cache):

1. **Seed receipt re-issue** — sign in `admin / admin123`, Payments tab, tap row for `Receipt #1042` (Marcus Sterling, $99.00, Card). Share sheet opens with title `Gymers Receipt #1042`. Drag the PDF document icon to Finder (Mac Catalyst's share sheet doesn't surface "Save to Files" by default; AirDrop / Mail / Notes / drag-out all work) → open the PDF in Preview → fields render: Receipt #1042, Marcus Sterling, ID M-001, Premium tier, $99.00, Card, correct timestamp.
2. **Live receipt** — record a new payment (`Aisha Khan / 50 / cash`). Tap the new top row. Share sheet shows `Gymers Receipt #1043`. Preview PDF → all fields correct.
3. **Re-tap idempotency** — tap the same row twice in quick succession. Second share sheet opens with the same file. No crash, no duplicate file.
4. **Member-removed fallback** — manually delete one member row from `gymers.db3` via `sqlite3` CLI. Relaunch app. Tap a payment whose member was deleted. Receipt renders with `(member removed)` placeholder; no crash.
5. **Validation regression sweep** — record-payment validation (empty member, bad amount, bad method) and search/check-in flows still work exactly as before. Receipt logic doesn't touch any of those code paths, but we verify quickly.
6. **Restart-survival** — quit (`pkill`) and relaunch. Top payment row from step 2 is still there (SQLite persistence from the predecessor slice); tap it again to re-issue from a fresh process.

iOS-target sim verification is skipped (sim is unusable on this hardware). iOS-target builds must remain green; that's covered by the build matrix above.

---

## 8. Status doc

After verification:

- Move "Receipt PDF generation" bullet from Ongoing Tasks to a new row in `completed_rows` in `docs/status/build_status_docx.py`.
- Mirror the change in `docs/status/gymers-mobile-app-status-update.html`.
- Regenerate the .docx via `python3 docs/status/build_status_docx.py`.

Suggested completed-row text:

> "Receipt PDF generation: tapping any row in Recent Payments renders a one-page PDF receipt via UIKit's UIGraphicsPdfRenderer (built into iOS + Mac Catalyst), saves under FileSystem.CacheDirectory/receipts/, and opens the system share sheet for save/email/AirDrop/print. Re-issues are deterministic from SQLite, so any historical payment can be re-printed."

---

## 9. Self-review notes (for the implementer)

- **`UIGraphicsPdfRenderer.WritePdf` must run on the UI thread.** UIKit drawing primitives (`NSString.DrawString`, `UIFont`, `UIColor`) throw `UIKit Consistency error: you are calling a UIKit method that can only be invoked from the UI thread` if invoked from the thread pool. Don't wrap the call in `Task.Run`. The caller (`PaymentsPage.OnRowTapped`) is already on the UI thread, so no marshaling is needed. If you ever invoke the receipt flow from a non-UI context, use `MainThread.InvokeOnMainThreadAsync`.
- **Microsoft.iOS bindings use Pascal case `Pdf`, not `PDF`** — the type is `UIGraphicsPdfRenderer`, not `UIGraphicsPDFRenderer` (despite Apple's Objective-C naming). Same for `UIGraphicsPdfRendererContext` and `UIGraphicsPdfRendererFormat`.
- **`UIGraphicsPdfRenderer.WritePdf(NSUrl, Action<...>, out NSError)`** — the bound overload requires the `out NSError`, not the closure-only form. Check the error and rethrow as a managed exception so the page-level try/catch can surface it.
- **`Share.Default.RequestAsync` must be awaited from the UI thread.** Same concern, opposite direction — if you offload the PDF render to a thread pool, you must marshal the share call back. Easiest is to keep both on the UI thread.
- **`TapGestureRecognizer` on a `Border`** can ghost-fire on Mac Catalyst if the Border has zero stroke. Existing rows have `StrokeThickness="0"` and tap fine in informal testing; if it misbehaves, set `InputTransparent="False"` on the inner Grid.
- **Timestamp formatting:** use `p.At.ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture)`. Avoid current-culture formatting — receipts must be locale-stable for evaluators. The middle character is U+00B7, not a bullet or hyphen.
- **Decimal formatting:** always `decimal.ToString("0.00", CultureInfo.InvariantCulture)`. Never `:F2` without culture, never `ToString("C")`.
- **Smoke test the app — for real — before marking the slice green.** A successful `dotnet build` does not exercise static initializers. The QuestPDF crash slipped through 4 commits because no one launched the app between Task 1 and Task 5. From now on, every task that adds startup-running code (DI registration, license-set, etc.) must be followed by an actual `open …Gymers.app` and a 4-second alive-check, even if the plan flags the smoke test as optional.
- **Don't add a Reports tab in this slice.** That's the next slice. PDF generation here is per-row only; bulk export is a separate problem.
