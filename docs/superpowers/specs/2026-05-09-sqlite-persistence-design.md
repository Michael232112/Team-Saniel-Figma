# Gymers Mobile App — SQLite Persistence Slice — Design

**Date:** 2026-05-09
**Status:** Approved (awaiting implementation plan)

**Goal:** Replace the in-memory `DataStore` with a SQLite-backed store so demo state (members, payments, check-ins) survives app restart. Keep `DataStore`'s public surface intact for pages — only the *write* methods become async; reads stay synchronous against the in-memory mirror.

**Tech Stack:** .NET 10, MAUI, C# 12, XAML. Add `sqlite-net-pcl` package. iOS 26.2 simulator + Mac Catalyst targets (both must build clean).

**Predecessor:** `docs/superpowers/specs/2026-05-07-app-comes-alive-design.md` (the "make it real" slice that introduced `DataStore`).

---

## 1. Scope

In scope: persist the three tables already held by `DataStore`:

- **Members** — seeded once from `SampleData.Members`; never mutated by current UI.
- **Payments** — appended via `PaymentsPage` Record Payment form.
- **CheckIns** — appended via `AttendancePage` Check In flow.

Out of scope:

- Trainer / Workout / Equipment / Reports tables — no UI consumes them yet; separate future slice.
- Schema migrations / versioning — sample-data app, schema is stable for v1; revisit only when a real migration is needed.
- Bulk export / import.
- Encryption at rest — sample data; no real PII.
- Receipt PDF generation, Reports export — separate ongoing items in the status doc.

---

## 2. Library and storage

- **Library:** `sqlite-net-pcl` (de-facto choice for MAUI; attribute-based mapping; both `SQLiteConnection` (sync) and `SQLiteAsyncConnection` (async)).
- **DB file:** `gymers.db3` under `FileSystem.AppDataDirectory` (per-user, survives app updates, isolated from other apps).
- **Tables created idempotently** on every startup via `CreateTable<T>()`. No migration logic.

### NuGet

Add to `Gymers/Gymers.csproj`:

```xml
<PackageReference Include="sqlite-net-pcl" Version="*" />
```

(Pin version during implementation; latest stable as of 2026-05-09.)

---

## 3. File layout

```
Gymers/Data/
  DataStore.cs        Modify — public surface unchanged; writes become async
  GymersDb.cs         New    — wraps SQLite connections, table creation, CRUD, mapping
  Rows/
    MemberRow.cs      New    — sqlite-net DTO for Member
    PaymentRow.cs     New    — sqlite-net DTO for Payment
    CheckInRow.cs     New    — sqlite-net DTO for CheckIn
```

`Gymers/Models/` records (`Member`, `Payment`, `CheckIn`) stay immutable. The DTO types are SQLite-only and never escape `Data/`.

---

## 4. Schema

### Members

| Column      | SQLite type | Notes                              |
| ----------- | ----------- | ---------------------------------- |
| Id          | TEXT        | `[PrimaryKey]`                     |
| Name        | TEXT        |                                    |
| Tier        | INTEGER     | `MembershipTier` enum stored as int |
| Status      | TEXT        | "Active", "Expiring Soon", etc.    |
| ExpiresIso  | TEXT        | ISO 8601 date, e.g., `"2026-12-15"` |

### Payments

| Column        | SQLite type | Notes                         |
| ------------- | ----------- | ----------------------------- |
| Id            | INTEGER     | `[PrimaryKey]`                |
| MemberId      | TEXT        | references `Members.Id`       |
| AmountText    | TEXT        | lossless decimal as string    |
| Method        | TEXT        | "Card" / "Cash" / "Bank"      |
| ReceiptNumber | INTEGER     |                               |
| AtTicks       | INTEGER     | `DateTime.Ticks`              |

### CheckIns

| Column   | SQLite type | Notes              |
| -------- | ----------- | ------------------ |
| Id       | INTEGER     | `[PrimaryKey]`     |
| MemberId | TEXT        |                    |
| AtTicks  | INTEGER     | `DateTime.Ticks`   |

No indices for v1 — sample-data sizes are small (≤6–7 rows per table at seed) and all lookups happen in memory after bootstrap.

No foreign-key constraints — members aren't deleted from the UI; orphaned references can't occur in v1.

---

## 5. Mapping (records ↔ rows)

Records are immutable positional types; sqlite-net needs mutable POCOs with parameterless constructors. Resolve with one-way DTOs in `Gymers/Data/Rows/` and converters in `GymersDb`.

```csharp
// MemberRow.cs (new)
public class MemberRow
{
    [PrimaryKey] public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; }
    public string Status { get; set; } = "";
    public string ExpiresIso { get; set; } = "";
}
```

`PaymentRow` and `CheckInRow` follow the same pattern.

Conversion helpers (private in `GymersDb`):

- `Member ToRecord(MemberRow r) => new(r.Id, r.Name, (MembershipTier)r.Tier, r.Status, DateOnly.Parse(r.ExpiresIso));`
- `MemberRow ToRow(Member m) => new() { Id = m.Id, Name = m.Name, Tier = (int)m.Tier, Status = m.Status, ExpiresIso = m.Expires.ToString("yyyy-MM-dd") };`
- Similar for Payment (`Amount.ToString(CultureInfo.InvariantCulture)` ↔ `decimal.Parse(...)`) and CheckIn (`At.Ticks` ↔ `new DateTime(ticks)`).

`InvariantCulture` everywhere to avoid locale-driven decimal-separator drift.

---

## 6. Bootstrap (DataStore constructor)

`DataStore` is a DI singleton (already registered in `MauiProgram.cs`). On construction:

1. Open `SQLiteConnection` to `Path.Combine(FileSystem.AppDataDirectory, "gymers.db3")`.
2. `CreateTable<MemberRow>()`, `<PaymentRow>()`, `<CheckInRow>()` — idempotent.
3. If `MemberRow` table is empty → insert all rows from `SampleData.Members`, `.Payments`, `.CheckIns` (sync `Insert` calls).
4. Load all rows into the existing `ObservableCollection<Member|Payment|CheckIn>` properties (sorted by `At` desc for Payments and CheckIns to match current ordering).
5. Hold the connection open for the lifetime of the app (singleton).

All bootstrap is synchronous. Total latency on sample-data sizes is well under 100ms — covered by the MAUI splash screen.

---

## 7. Runtime mutations

Two methods change signature:

```csharp
// before
public Payment RecordPayment(Member m, decimal amount, string method)
public CheckIn RecordCheckIn(Member m)

// after
public Task<Payment> RecordPaymentAsync(Member m, decimal amount, string method)
public Task<CheckIn>  RecordCheckInAsync(Member m)
```

Implementation pattern (Payments shown; CheckIns is identical):

1. Compute `nextId` and `nextReceipt` from the in-memory `Payments` collection (same logic as today).
2. `var row = ToRow(payment);`
3. `await _db.AsyncConnection.InsertAsync(row);`
4. On the UI thread (`MainThread.BeginInvokeOnMainThread`), call `Payments.Insert(0, payment);`
5. Return the `Payment` record.

If the async insert throws, the collection is not updated and the exception propagates out of the `await`. The page's `OnRecord` / `OnCheckIn` handlers do not currently catch exceptions, so a runtime DB failure surfaces as an unhandled async-void exception. v1 accepts that — see §10.

Read methods (`FindMemberByName`, `SearchMembers`) and the `Members`/`Payments`/`CheckIns` accessors stay synchronous — they query the in-memory mirror.

---

## 8. Page changes

Two files:

### `Gymers/Pages/PaymentsPage.xaml.cs`

`OnRecord` becomes:

```csharp
async void OnRecord(object? sender, EventArgs e)
{
    // ...validation unchanged...
    var payment = await _data.RecordPaymentAsync(member, amount, method);
    // ...status label + reset unchanged...
}
```

### `Gymers/Pages/AttendancePage.xaml.cs`

`OnCheckIn` becomes:

```csharp
async void OnCheckIn(object? sender, EventArgs e)
{
    if (_selected is null) { ShowError("Select a member first."); return; }
    var member = _selected;
    var c = await _data.RecordCheckInAsync(member);
    _selected = null;
    MemberSearch.Text = "";
    Suggestions.IsVisible = false;
    ShowSuccess($"Checked in {member.Name} at {c.At:hh\\:mm tt}.");
}
```

`async void` is acceptable for `Clicked` event handlers; both methods already follow that pattern.

No XAML changes.

---

## 9. DI

No changes to `Gymers/MauiProgram.cs`. `AddSingleton<DataStore>()` continues to hold; the constructor is parameterless and self-bootstrapping.

(`GymersDb` is constructed inside `DataStore`'s ctor and not registered separately; it's an internal collaborator.)

---

## 10. Error handling

- **DB open failure** (e.g., disk full, sandbox issue): throw from the `DataStore` constructor — app crashes at startup. Acceptable for a demo with no recovery story.
- **Insert failure during runtime**: exception propagates as an unhandled async-void exception from `OnRecord` / `OnCheckIn`. The in-memory collection is not updated, so the UI stays consistent with the DB. v1 does not catch — the failure mode is rare on a local SQLite file with no concurrent writers, and the demo has no recovery story. If reliability later matters, wrap the `await` in `try { ... } catch (Exception ex) { ShowError(ex.Message); }` inside the page handler.
- **Concurrency**: single-user, single-process. No locking concerns at sample-data sizes.

---

## 11. Verification

Manual on Mac Catalyst (iOS sim is unusable on this hardware; see project memory `project_mac_catalyst_path.md`).

After implementation, the run helper from the predecessor plan still applies — substitute `-f net10.0-maccatalyst` and the `open …Gymers.app` command.

End-to-end demo script:

1. **Fresh DB**: delete `gymers.db3` from `AppDataDirectory` (or wipe app data via Mac Catalyst's "Reset" affordance). Launch — Members shows 6 rows, Payments shows 5, CheckIns shows 6 (seed data appears).
2. **Payments persist across restart**: record `Marcus Sterling / 75.50 / card`. New row at top. Quit (⌘Q). Relaunch. New row still at top of Recent Payments.
3. **CheckIns persist across restart**: search `lena`, tap suggestion, CHECK IN. New row at top. Quit. Relaunch. New row still at top.
4. **Validation regressions**: re-run a few error-path checks from the predecessor plan to confirm no behavior was broken (empty member, bad amount, etc.).
5. **Build**: both `dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug` and `-f net10.0-maccatalyst -c Debug` succeed with `0 Warning(s) 0 Error(s)`.

No automated tests added — matches existing precedent.

---

## 12. Files touched (summary)

| File                                       | Action  |
| ------------------------------------------ | ------- |
| `Gymers/Gymers.csproj`                     | Modify (add `sqlite-net-pcl` PackageReference) |
| `Gymers/Data/DataStore.cs`                 | Modify (use `GymersDb`; async write methods)   |
| `Gymers/Data/GymersDb.cs`                  | Create  |
| `Gymers/Data/Rows/MemberRow.cs`            | Create  |
| `Gymers/Data/Rows/PaymentRow.cs`           | Create  |
| `Gymers/Data/Rows/CheckInRow.cs`           | Create  |
| `Gymers/Pages/PaymentsPage.xaml.cs`        | Modify (await `RecordPaymentAsync`)            |
| `Gymers/Pages/AttendancePage.xaml.cs`      | Modify (await `RecordCheckInAsync`)            |

Eight files. No XAML changes.

---

## 13. Risks and notes for the implementer

- **Path on first launch**: `FileSystem.AppDataDirectory` is created automatically by MAUI; no `Directory.CreateDirectory` needed.
- **Decimal serialization**: always pass `CultureInfo.InvariantCulture` to `decimal.ToString` and `decimal.Parse`. PHP/locale-aware overloads will reject `.` separators on devices with comma decimals.
- **`DateOnly` parsing**: use `DateOnly.Parse(s, CultureInfo.InvariantCulture)` to be safe.
- **Order at load**: load Payments and CheckIns sorted by `AtTicks DESC` so Insert(0, ...) at runtime keeps the list newest-first. Members can load in any order.
- **Async-void event handlers**: keep them small; let exceptions bubble to the existing status-label error branch.
- **Connection lifetime**: open once in DataStore ctor, never close. The singleton's lifetime is the app's lifetime.
