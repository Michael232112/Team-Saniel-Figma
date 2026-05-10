# Gymers Mobile App — Receipt PDF Generation Slice — Design

**Date:** 2026-05-10
**Status:** Approved (awaiting implementation plan)

**Goal:** Tap a row in Recent Payments → generate a one-page PDF receipt → open the system share sheet so the user can save, email, AirDrop, or print it. The receipt re-renders deterministically from SQLite, so any historical payment (including seeds) can be re-issued. Mac Catalyst is the verification target; iOS-target builds must remain green.

**Tech Stack:** .NET 10, MAUI, C# 12, XAML. Add `QuestPDF` package. iOS 26.2 + Mac Catalyst targets.

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

### Library: **QuestPDF 2026.5.0 (Community license)**

- Fluent C# layout API, no XSLT/HTML detour.
- Pure managed; works on iOS + Mac Catalyst with zero native deps.
- Free for individuals + projects under $1M revenue → school project clears trivially.
- License is set once at app startup in `MauiProgram.cs`:

  ```csharp
  QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
  ```

Rejected:

- `PdfSharpCore` — no track record on net10.0-maccatalyst; risk of native interop surprises.
- `iTextSharp` — AGPL; bad fit even for school projects.
- `UIGraphicsPDFRenderer` (UIKit) — iOS-only, breaks Mac Catalyst.
- Hand-rolled PDF — pointless complexity.

### Trigger: **Tap a payment row**

- Each row in Recent Payments becomes tappable. The trailing chevron — already a `ListRow` feature gated by a `TrailingChevron` boolean — gets flipped on for payment rows to signal interactivity.
- `Members` and `Attendance` rows are unaffected (they don't subscribe to the new event).

Rejected: auto-generate-on-record. If PDF generation fails, the payment is already in SQLite; the partial-success UX is confusing. Explicit means a failed generate is just a retry, not a "is my payment recorded?" panic.

---

## 3. Receipt layout

One A4 page, single-column. Brand tokens lifted from the existing app: teal accent `#0F766E`, navy heading `#18212F`, muted body grey, Manrope-equivalent (QuestPDF will fall back to its bundled Helvetica family — acceptable for v1; we're not embedding fonts).

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
  Task<string> GenerateAsync(Payment p, Member m);
  ```
  Returns the absolute path of the written PDF. Pure: same input → same bytes. Internally creates the cache directory, builds a `ReceiptDocument`, calls `QuestPDF`'s `GeneratePdf(path)` inside `Task.Run` (QuestPDF's API is sync; offloading keeps the UI thread free).

- **`ReceiptDocument`** — implements `QuestPDF.Infrastructure.IDocument`. Pure layout, no I/O. Constructor takes `(Payment, Member)`. Renders the layout above.

- **`PaymentsPage`** — gains a `OnReceipt(object?, EventArgs)` handler wired to the `ListRow.Tapped` event when a row is built imperatively in `Render()`. The handler resolves the `Payment` from a per-row `CommandParameter` (set when the row is built), looks up the member, calls `ReceiptService.GenerateAsync`, then `Share.Default.RequestAsync(new ShareFileRequest { File = new ShareFile(path), Title = $"Gymers Receipt #{p.ReceiptNumber}" })`.

- **`ListRow`** — extended (small, additive) with a `Tapped` event and a `CommandParameter` bindable property. Inner `Border` wraps a `TapGestureRecognizer` that fires `Tapped`. Other consumers ignore the event and don't set `CommandParameter`, so behavior is unchanged for Members and Attendance rows.

### File path

`{FileSystem.CacheDirectory}/receipts/gymers-receipt-{ReceiptNumber}.pdf`

- Cache dir means the OS can reclaim it; we never assume long-term presence.
- Receipt number in the filename makes the share sheet's title and the saved filename match what the user saw on screen — no UUID confusion.
- File is unconditionally overwritten on each tap (no append / no dedupe / no race-window concerns; QuestPDF writes the file atomically per page-flush).

### DI wiring

In `MauiProgram.cs`:

```csharp
builder.Services.AddSingleton<ReceiptService>();
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
```

`PaymentsPage` already takes `DataStore` via constructor injection; we add `ReceiptService` to its constructor.

---

## 5. File layout

| File | Action |
| ---- | ------ |
| `Gymers/Gymers.csproj` | Modify — add `<PackageReference Include="QuestPDF" Version="2026.5.0" />` |
| `Gymers/MauiProgram.cs` | Modify — set Community license, register `ReceiptService` |
| `Gymers/Services/ReceiptService.cs` | Create |
| `Gymers/Services/ReceiptDocument.cs` | Create |
| `Gymers/Controls/ListRow.xaml` | Modify — add inner `TapGestureRecognizer` |
| `Gymers/Controls/ListRow.xaml.cs` | Modify — add `Tapped` event + `CommandParameter` BindableProperty |
| `Gymers/Pages/PaymentsPage.xaml.cs` | Modify — wire row taps to `OnReceipt`, flip `TrailingChevron=true`, set `CommandParameter` per row |

Seven files. One new package. No model or DB changes.

---

## 6. Error handling

Three failure modes, all surfaced via the existing `StatusLabel` red-error pattern on `PaymentsPage`:

1. **Member not found for the row's `MemberId`** (e.g., a deleted member) → render the receipt with `(member removed)` in the name slot, `—` for ID and tier. No error UI; the receipt is still valid history.
2. **PDF write failure** (rare; disk full, sandbox permission anomaly) → red status label `Couldn't generate receipt: {ex.Message}`. If a partial file is left behind, the next tap unconditionally overwrites it, so there's no orphan-file recovery to worry about.
3. **Share sheet fails to open** (no available share targets — extremely unusual) → same red status label pattern. The share API throws a `FeatureNotSupportedException` we can let propagate to a try/catch around the awaited call.

Cancelled-by-user is not an error: the share API completes normally regardless.

---

## 7. Verification

Build matrix:

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Both must end with `0 Warning(s) 0 Error(s)`. The first build will pull QuestPDF + transitive deps; expect ~30s extra restore on first compile.

Mac Catalyst manual sweep (single launch session, after wiping any previous receipts cache):

1. **Seed receipt re-issue** — sign in, Payments tab, tap row for `Receipt #1042` (Marcus Sterling, $99.00, Card). Share sheet opens. Choose "Save to Files" → open the PDF in Preview → fields render: Receipt #1042, Marcus Sterling, ID M-001, Premium tier, $99.00, Card, correct timestamp.
2. **Live receipt** — record a new payment (`Diego Alvarez / 50 / bank`). Tap the new top row. Share sheet shows `Gymers Receipt #1043`. Preview PDF → all fields correct.
3. **Re-tap idempotency** — tap the same row twice in quick succession. Second share sheet opens with the same file. No crash, no duplicate file.
4. **Member-removed fallback** — manually delete one member row from `gymers.db3` via `sqlite3` CLI. Relaunch app. Tap a payment whose member was deleted. Receipt renders with `(member removed)` placeholder; no crash.
5. **Validation regression sweep** — record-payment validation (empty member, bad amount, bad method) and search/check-in flows still work exactly as before. Receipt logic doesn't touch any of those code paths, but we verify quickly.

iOS-target sim verification is skipped (sim is unusable on this hardware). iOS-target builds must remain green; that's covered by the build matrix above.

---

## 8. Status doc

After verification:

- Move "Receipt PDF generation" bullet from Ongoing Tasks to a new row in `completed_rows` in `docs/status/build_status_docx.py`.
- Mirror the change in `docs/status/gymers-mobile-app-status-update.html`.
- Regenerate the .docx via `python3 docs/status/build_status_docx.py`.

Suggested completed-row text:

> "Receipt PDF generation: tapping any row in Recent Payments generates a one-page PDF receipt via QuestPDF (Community license) under FileSystem.CacheDirectory/receipts/, then opens the system share sheet for save/email/print. Re-issues are deterministic from SQLite, so any historical payment can be re-printed."

---

## 9. Self-review notes (for the implementer)

- **QuestPDF license must be set before any document is generated.** Set it in `MauiProgram.CreateMauiApp()` *before* `builder.Services.AddSingleton<ReceiptService>()`. If you forget, QuestPDF throws on first `GeneratePdf` call with a license-required exception.
- **`Task.Run(() => doc.GeneratePdf(path))`** keeps the UI thread responsive. QuestPDF's API is sync; calling it directly on the UI thread will jank the share sheet animation on Mac Catalyst.
- **`Share.Default.RequestAsync` must be awaited from the UI thread.** Marshal back via `MainThread.InvokeOnMainThreadAsync` if you ever invoke the receipt flow from a non-UI context.
- **`TapGestureRecognizer` on a `Border`** can ghost-fire on Mac Catalyst if the Border has zero stroke. Existing rows already have `StrokeThickness="0"` and tap fine in other MAUI projects; if it misbehaves, set `InputTransparent="False"` on the inner Grid.
- **Timestamp formatting:** use `p.At.ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture)`. Avoid current-culture formatting — receipts must be locale-stable for evaluators.
- **Don't add a Reports tab in this slice.** That's the next slice. PDF generation here is per-row only; bulk export is a separate problem.
