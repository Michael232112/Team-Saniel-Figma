# Gymers Mobile App — SQLite Persistence — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the in-memory `DataStore` with a SQLite-backed store so members, payments, and check-ins survive app restart. Public read surface unchanged; write methods become async.

**Architecture:** `sqlite-net-pcl` wraps a SQLite file at `FileSystem.AppDataDirectory/gymers.db3`. A new `GymersDb` class owns the connection, table creation, mapping, and CRUD. `DataStore` continues to expose `ObservableCollection<Member|Payment|CheckIn>` (now SQLite-backed at startup) and async `Record*Async` methods that persist + update the in-memory mirror.

**Tech Stack:** .NET 10, MAUI, C# 12, XAML. Add `sqlite-net-pcl`. Existing project. iOS 26.2 sim + Mac Catalyst targets.

**Spec:** `docs/superpowers/specs/2026-05-09-sqlite-persistence-design.md` (commit `0ea1d71`).

---

## Files Touched

| File                                       | Action |
| ------------------------------------------ | ------ |
| `Gymers/Gymers.csproj`                     | Modify (add `sqlite-net-pcl` PackageReference) |
| `Gymers/Data/Rows/MemberRow.cs`            | Create |
| `Gymers/Data/Rows/PaymentRow.cs`           | Create |
| `Gymers/Data/Rows/CheckInRow.cs`           | Create |
| `Gymers/Data/GymersDb.cs`                  | Create |
| `Gymers/Data/DataStore.cs`                 | Modify (SQLite bootstrap + async write methods) |
| `Gymers/Pages/PaymentsPage.xaml.cs`        | Modify (await `RecordPaymentAsync`) |
| `Gymers/Pages/AttendancePage.xaml.cs`      | Modify (await `RecordCheckInAsync`) |

Eight files. No XAML changes.

---

## Run helper (referenced by every task)

When a task says "build and run," do this from the repo root.

**Build (Mac Catalyst, fast — primary verification target):**
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

**Find the SQLite file (after first launch):**
```bash
find ~/Library/Containers -name "gymers.db3" 2>/dev/null
```

**Wipe the DB to test fresh seed:**
```bash
DB=$(find ~/Library/Containers -name "gymers.db3" 2>/dev/null | head -1)
[ -n "$DB" ] && rm "$DB" && echo "Removed $DB" || echo "No DB found yet"
```

**Why Mac Catalyst, not iOS sim:** the iOS simulator is unusable on this hardware (sustained UI lag). Mac Catalyst runs the same MAUI code paths natively. iOS-target builds must still succeed (it's the primary deploy target), but verification happens on Mac Catalyst.

---

## Task 1: Foundation — NuGet package and Row DTOs

After this task the project compiles with `sqlite-net-pcl` available and three DTO classes that mirror the model records. No runtime behavior change yet.

**Files:**
- Modify: `Gymers/Gymers.csproj`
- Create: `Gymers/Data/Rows/MemberRow.cs`
- Create: `Gymers/Data/Rows/PaymentRow.cs`
- Create: `Gymers/Data/Rows/CheckInRow.cs`

- [ ] **Step 1: Add the `sqlite-net-pcl` PackageReference**

In `Gymers/Gymers.csproj`, find the `<ItemGroup>` containing `<PackageReference Include="Microsoft.Maui.Controls" ... />`. Add a new line inside it:

```xml
<PackageReference Include="sqlite-net-pcl" Version="1.9.172" />
```

The full `<ItemGroup>` becomes:

```xml
<ItemGroup>
    <PackageReference Include="Microsoft.Maui.Controls" Version="$(MauiVersion)" />
    <PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.0" />
    <PackageReference Include="sqlite-net-pcl" Version="1.9.172" />
</ItemGroup>
```

- [ ] **Step 2: Create `Gymers/Data/Rows/MemberRow.cs`**

```csharp
using SQLite;

namespace Gymers.Data.Rows;

public class MemberRow
{
    [PrimaryKey] public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; }
    public string Status { get; set; } = "";
    public string ExpiresIso { get; set; } = "";
}
```

- [ ] **Step 3: Create `Gymers/Data/Rows/PaymentRow.cs`**

```csharp
using SQLite;

namespace Gymers.Data.Rows;

public class PaymentRow
{
    [PrimaryKey] public int Id { get; set; }
    public string MemberId { get; set; } = "";
    public string AmountText { get; set; } = "0";
    public string Method { get; set; } = "";
    public int ReceiptNumber { get; set; }
    public long AtTicks { get; set; }
}
```

- [ ] **Step 4: Create `Gymers/Data/Rows/CheckInRow.cs`**

```csharp
using SQLite;

namespace Gymers.Data.Rows;

public class CheckInRow
{
    [PrimaryKey] public int Id { get; set; }
    public string MemberId { get; set; } = "";
    public long AtTicks { get; set; }
}
```

- [ ] **Step 5: Build both targets to verify the package restores cleanly**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: each ends with `Build succeeded. 0 Warning(s) 0 Error(s)`. The first build will pull `sqlite-net-pcl` and its `SQLitePCLRaw.*` transitive dependencies; this may add ~30 seconds to the restore step.

- [ ] **Step 6: Smoke-test the app**

Use the run helper to relaunch on Mac Catalyst. Verify the app behaves identically to before — Login → Dashboard → Members search → Payments record → Attendance check-in all work in-memory exactly as before. No persistence yet; that comes in Task 3 onward.

- [ ] **Step 7: Commit**

```bash
git add Gymers/Gymers.csproj Gymers/Data/Rows/MemberRow.cs Gymers/Data/Rows/PaymentRow.cs Gymers/Data/Rows/CheckInRow.cs
git commit -m "feat(data): add sqlite-net-pcl + Row DTOs

Foundation for SQLite persistence. Three mutable POCOs with
parameterless constructors satisfy sqlite-net's reflection
mapping while keeping the public Member/Payment/CheckIn records
immutable. No runtime behavior change yet."
```

---

## Task 2: GymersDb — SQLite connection wrapper + mapping

After this task `GymersDb` exists as a compiled class that owns SQLite connections, creates tables, seeds, queries, and inserts. `DataStore` does not yet use it. Build still green, behavior unchanged.

**Files:**
- Create: `Gymers/Data/GymersDb.cs`

- [ ] **Step 1: Create `Gymers/Data/GymersDb.cs`**

```csharp
using System.Globalization;
using Gymers.Data.Rows;
using Gymers.Models;
using SQLite;

namespace Gymers.Data;

public sealed class GymersDb
{
    readonly string _path;
    readonly SQLiteConnection _sync;
    SQLiteAsyncConnection? _async;

    public GymersDb(string path)
    {
        _path = path;
        _sync = new SQLiteConnection(path);
        _sync.CreateTable<MemberRow>();
        _sync.CreateTable<PaymentRow>();
        _sync.CreateTable<CheckInRow>();
    }

    public SQLiteAsyncConnection Async => _async ??= new SQLiteAsyncConnection(_path);

    public bool IsMembersEmpty() =>
        _sync.Table<MemberRow>().Count() == 0;

    public void SeedMembers(IEnumerable<Member> members)
    {
        foreach (var m in members) _sync.Insert(ToRow(m));
    }

    public void SeedPayments(IEnumerable<Payment> payments)
    {
        foreach (var p in payments) _sync.Insert(ToRow(p));
    }

    public void SeedCheckIns(IEnumerable<CheckIn> checkIns)
    {
        foreach (var c in checkIns) _sync.Insert(ToRow(c));
    }

    public IEnumerable<Member> GetMembers() =>
        _sync.Table<MemberRow>().ToList().Select(ToRecord);

    public IEnumerable<Payment> GetPaymentsNewestFirst() =>
        _sync.Table<PaymentRow>()
             .OrderByDescending(r => r.AtTicks)
             .ToList()
             .Select(ToRecord);

    public IEnumerable<CheckIn> GetCheckInsNewestFirst() =>
        _sync.Table<CheckInRow>()
             .OrderByDescending(r => r.AtTicks)
             .ToList()
             .Select(ToRecord);

    public Task InsertPaymentAsync(Payment p) => Async.InsertAsync(ToRow(p));
    public Task InsertCheckInAsync(CheckIn c) => Async.InsertAsync(ToRow(c));

    static MemberRow ToRow(Member m) => new()
    {
        Id         = m.Id,
        Name       = m.Name,
        Tier       = (int)m.Tier,
        Status     = m.Status,
        ExpiresIso = m.Expires.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    };

    static Member ToRecord(MemberRow r) => new(
        r.Id, r.Name, (MembershipTier)r.Tier, r.Status,
        DateOnly.ParseExact(r.ExpiresIso, "yyyy-MM-dd", CultureInfo.InvariantCulture));

    static PaymentRow ToRow(Payment p) => new()
    {
        Id            = p.Id,
        MemberId      = p.MemberId,
        AmountText    = p.Amount.ToString(CultureInfo.InvariantCulture),
        Method        = p.Method,
        ReceiptNumber = p.ReceiptNumber,
        AtTicks       = p.At.Ticks
    };

    static Payment ToRecord(PaymentRow r) => new(
        r.Id,
        r.MemberId,
        decimal.Parse(r.AmountText, CultureInfo.InvariantCulture),
        r.Method,
        r.ReceiptNumber,
        new DateTime(r.AtTicks));

    static CheckInRow ToRow(CheckIn c) => new()
    {
        Id       = c.Id,
        MemberId = c.MemberId,
        AtTicks  = c.At.Ticks
    };

    static CheckIn ToRecord(CheckInRow r) => new(
        r.Id, r.MemberId, new DateTime(r.AtTicks));
}
```

- [ ] **Step 2: Build both targets**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: `0 Warning(s) 0 Error(s)` on both.

- [ ] **Step 3: Smoke-test the app**

Relaunch on Mac Catalyst. Verify identical pre-existing behavior. `GymersDb` is unused so far — this step only confirms nothing broke.

- [ ] **Step 4: Commit**

```bash
git add Gymers/Data/GymersDb.cs
git commit -m "feat(data): add GymersDb wrapper around sqlite-net

Wraps SQLite connections (sync for bootstrap, async for runtime),
creates tables idempotently, exposes seed/query/insert helpers,
and converts between Row DTOs and Model records. Not yet wired
into DataStore — this commit only adds the file."
```

---

## Task 3: DataStore reads from SQLite

After this task the app loads members/payments/check-ins from the SQLite file on every launch. On first launch, the DB is empty, so `DataStore` seeds it from `SampleData`. Mutations (`RecordPayment`, `RecordCheckIn`) still update only the in-memory collections — they don't yet persist. Tasks 4 and 5 fix that.

**Files:**
- Modify: `Gymers/Data/DataStore.cs`

- [ ] **Step 1: Replace `Gymers/Data/DataStore.cs` body**

Replace the entire file with:

```csharp
using System.Collections.ObjectModel;
using Gymers.Models;
using Microsoft.Maui.Storage;

namespace Gymers.Data;

public sealed class DataStore
{
    readonly GymersDb _db;

    public ObservableCollection<Member>  Members  { get; }
    public ObservableCollection<Payment> Payments { get; }
    public ObservableCollection<CheckIn> CheckIns { get; }

    public DataStore()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "gymers.db3");
        _db = new GymersDb(dbPath);

        if (_db.IsMembersEmpty())
        {
            _db.SeedMembers(SampleData.Members);
            _db.SeedPayments(SampleData.Payments);
            _db.SeedCheckIns(SampleData.CheckIns);
        }

        Members  = new ObservableCollection<Member>(_db.GetMembers());
        Payments = new ObservableCollection<Payment>(_db.GetPaymentsNewestFirst());
        CheckIns = new ObservableCollection<CheckIn>(_db.GetCheckInsNewestFirst());
    }

    public Member? FindMemberByName(string? name) =>
        Members.FirstOrDefault(m =>
            string.Equals(m.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Member> SearchMembers(string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? Members
            : Members.Where(m =>
                m.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

    public Payment RecordPayment(Member m, decimal amount, string method)
    {
        int nextId      = Payments.Count == 0 ? 1043 : Payments.Max(p => p.Id) + 1;
        int nextReceipt = Payments.Count == 0 ? 1043 : Payments.Max(p => p.ReceiptNumber) + 1;
        var p = new Payment(nextId, m.Id, amount, method, nextReceipt, DateTime.Now);
        Payments.Insert(0, p);
        return p;
    }

    public CheckIn RecordCheckIn(Member m)
    {
        int nextId = CheckIns.Count == 0 ? 1 : CheckIns.Max(c => c.Id) + 1;
        var c = new CheckIn(nextId, m.Id, DateTime.Now);
        CheckIns.Insert(0, c);
        return c;
    }
}
```

What changed vs. the previous version:
- Constructor now opens `GymersDb`, seeds-if-empty, and reads from DB instead of `SampleData`.
- `RecordPayment` and `RecordCheckIn` are unchanged for now (still in-memory only). Tasks 4 and 5 replace them with async + persistent variants.

- [ ] **Step 2: Build both targets**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: `0 Warning(s) 0 Error(s)`.

- [ ] **Step 3: Manual verify — fresh seed**

Wipe the DB (it may not exist yet on first run; that's fine):

```bash
DB=$(find ~/Library/Containers -name "gymers.db3" 2>/dev/null | head -1)
[ -n "$DB" ] && rm "$DB" && echo "Removed $DB" || echo "No DB to remove"
```

Relaunch the app. Sign in `admin / admin123`. Verify:

1. **Members** tab shows all 6 seeded members (Marcus, Lena, Diego, Aisha, Sam, Priya).
2. **Payments** tab shows the 5 seeded payments newest-first (Receipt #1042 at top).
3. **Attendance** tab shows the 6 seeded check-ins newest-first.

Confirm the SQLite file was created:

```bash
find ~/Library/Containers -name "gymers.db3" 2>/dev/null
```

Should print one path. The file size should be a few KB.

- [ ] **Step 4: Manual verify — restart loads from DB (not seed)**

Quit the app (`pkill -f Gymers; sleep 1`). Relaunch. Verify the same 6 members / 5 payments / 6 check-ins appear — *without* re-seeding. (You can confirm no re-seed happened by recording a payment in the next step and watching its receipt number increment from #1043, not collide with the seed.)

- [ ] **Step 5: Manual verify — mutations are still ephemeral**

Record a payment: `Marcus Sterling / 75.50 / card`. New row appears at top with Receipt #1043. Quit and relaunch. Expected: the #1043 row is **gone** (mutations not yet persisted; we fix this in Task 4). Note this in your verification log so you can re-confirm it's fixed in Task 4.

- [ ] **Step 6: Commit**

```bash
git add Gymers/Data/DataStore.cs
git commit -m "feat(data): bootstrap DataStore from SQLite

Constructor opens GymersDb, seeds the three tables on first run,
and loads ObservableCollections from the DB on every launch.
Read path is fully persistent. Write path (RecordPayment /
RecordCheckIn) still updates only the in-memory collections —
async + persistent variants land in the next two tasks."
```

---

## Task 4: Persist payments

After this task, recording a payment writes to SQLite and survives app restart. `RecordPayment` is replaced by `RecordPaymentAsync`; `PaymentsPage.OnRecord` awaits it.

**Files:**
- Modify: `Gymers/Data/DataStore.cs`
- Modify: `Gymers/Pages/PaymentsPage.xaml.cs`

- [ ] **Step 1: Replace `RecordPayment` with `RecordPaymentAsync` in `DataStore.cs`**

Find this method:

```csharp
public Payment RecordPayment(Member m, decimal amount, string method)
{
    int nextId      = Payments.Count == 0 ? 1043 : Payments.Max(p => p.Id) + 1;
    int nextReceipt = Payments.Count == 0 ? 1043 : Payments.Max(p => p.ReceiptNumber) + 1;
    var p = new Payment(nextId, m.Id, amount, method, nextReceipt, DateTime.Now);
    Payments.Insert(0, p);
    return p;
}
```

Replace it with:

```csharp
public async Task<Payment> RecordPaymentAsync(Member m, decimal amount, string method)
{
    int nextId      = Payments.Count == 0 ? 1043 : Payments.Max(p => p.Id) + 1;
    int nextReceipt = Payments.Count == 0 ? 1043 : Payments.Max(p => p.ReceiptNumber) + 1;
    var p = new Payment(nextId, m.Id, amount, method, nextReceipt, DateTime.Now);
    await _db.InsertPaymentAsync(p);
    await MainThread.InvokeOnMainThreadAsync(() => Payments.Insert(0, p));
    return p;
}
```

(`MainThread` lives in `Microsoft.Maui.ApplicationModel`. With `<UseMaui>true</UseMaui>` in the csproj, MAUI's implicit usings should bring it in automatically. If the build complains about `MainThread`, add `using Microsoft.Maui.ApplicationModel;` to the top of `DataStore.cs`.)

- [ ] **Step 2: Update `Gymers/Pages/PaymentsPage.xaml.cs` `OnRecord`**

Find the existing method declaration:

```csharp
void OnRecord(object? sender, EventArgs e)
```

Change to:

```csharp
async void OnRecord(object? sender, EventArgs e)
```

Inside the method body, find this line:

```csharp
var payment = _data.RecordPayment(member, amount, method);
```

Replace with:

```csharp
var payment = await _data.RecordPaymentAsync(member, amount, method);
```

For reference, the full updated method reads:

```csharp
async void OnRecord(object? sender, EventArgs e)
{
    var nameRaw   = MemberInput.Text?.Trim() ?? "";
    var amountRaw = AmountInput.Text?.Trim() ?? "";
    var methodRaw = MethodInput.Text?.Trim() ?? "";

    var member = _data.FindMemberByName(nameRaw);
    if (member is null)
    { ShowError($"No member named \"{nameRaw}\". Try {SuggestNames()}."); return; }

    if (!decimal.TryParse(amountRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
        || amount <= 0
        || decimal.Round(amount, 2) != amount)
    { ShowError("Amount must be a positive number with up to 2 decimals."); return; }

    var method = methodRaw.ToLowerInvariant() switch
    {
        "card" => "Card",
        "cash" => "Cash",
        "bank" => "Bank",
        _      => null
    };
    if (method is null)
    { ShowError("Method must be Card, Cash, or Bank."); return; }

    var payment = await _data.RecordPaymentAsync(member, amount, method);

    MemberInput.Text = "";
    AmountInput.Text = "";
    MethodInput.Text = "";
    ShowSuccess($"Recorded ${payment.Amount:0.00} · Receipt #{payment.ReceiptNumber}.");
}
```

- [ ] **Step 3: Build both targets**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: `0 Warning(s) 0 Error(s)`.

- [ ] **Step 4: Manual verify — payment persists across restart**

Relaunch the app. Sign in. Tap **Payments**. Verify:

1. **Existing list still renders**: 5 seed payments visible at top, plus any payments persisted from previous test runs (if Task 3 verification step 5's #1043 row was lost, that's fine — Task 3's behavior was correct at that point).
2. **Happy path persists**: enter `Marcus Sterling / 75.50 / card`, tap **RECORD PAYMENT**. New row at top with Receipt #1043 and green success status `Recorded $75.50 · Receipt #1043.`
3. **Quit + relaunch**: `pkill -f "Gymers/bin/Debug/net10.0-maccatalyst" 2>/dev/null; sleep 1; open Gymers/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Gymers.app`
4. Sign in again. Tap **Payments**. The Marcus Sterling $75.50 row is **still at the top** of Recent Payments.
5. **Validation regression check**: try `Bob / 10 / card` → still errors `No member named "Bob"...`. Try `Lena Park / 0 / card` → still errors `Amount must be a positive number...`. Try `Lena Park / 25 / Crypto` → still errors `Method must be Card, Cash, or Bank.` (Validation paths must continue to work — they don't touch the DB.)
6. **Attendance still works in-memory**: tap **Attendance**, do a check-in, confirm the row appears. Quit + relaunch — that row will be **gone** (Attendance persistence comes in Task 5; this is the correct intermediate state).

- [ ] **Step 5: Commit**

```bash
git add Gymers/Data/DataStore.cs Gymers/Pages/PaymentsPage.xaml.cs
git commit -m "feat(payments): persist Record Payment to SQLite

RecordPaymentAsync inserts via SQLiteAsyncConnection, then marshals
back to the UI thread to update the ObservableCollection. The row
survives app restart. PaymentsPage.OnRecord becomes async void
and awaits the new method."
```

---

## Task 5: Persist check-ins

After this task, recording a check-in writes to SQLite and survives app restart. `RecordCheckIn` is replaced by `RecordCheckInAsync`; `AttendancePage.OnCheckIn` awaits it.

**Files:**
- Modify: `Gymers/Data/DataStore.cs`
- Modify: `Gymers/Pages/AttendancePage.xaml.cs`

- [ ] **Step 1: Replace `RecordCheckIn` with `RecordCheckInAsync` in `DataStore.cs`**

Find this method:

```csharp
public CheckIn RecordCheckIn(Member m)
{
    int nextId = CheckIns.Count == 0 ? 1 : CheckIns.Max(c => c.Id) + 1;
    var c = new CheckIn(nextId, m.Id, DateTime.Now);
    CheckIns.Insert(0, c);
    return c;
}
```

Replace with:

```csharp
public async Task<CheckIn> RecordCheckInAsync(Member m)
{
    int nextId = CheckIns.Count == 0 ? 1 : CheckIns.Max(c => c.Id) + 1;
    var c = new CheckIn(nextId, m.Id, DateTime.Now);
    await _db.InsertCheckInAsync(c);
    await MainThread.InvokeOnMainThreadAsync(() => CheckIns.Insert(0, c));
    return c;
}
```

- [ ] **Step 2: Update `Gymers/Pages/AttendancePage.xaml.cs` `OnCheckIn`**

Find the existing method:

```csharp
void OnCheckIn(object? sender, EventArgs e)
{
    if (_selected is null) { ShowError("Select a member first."); return; }
    var member = _selected;
    var c = _data.RecordCheckIn(member);
    _selected = null;
    MemberSearch.Text = "";
    Suggestions.IsVisible = false;
    ShowSuccess($"Checked in {member.Name} at {c.At:hh\\:mm tt}.");
}
```

Replace with:

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

Two changes: `void` → `async void`, and `_data.RecordCheckIn(member)` → `await _data.RecordCheckInAsync(member)`.

- [ ] **Step 3: Build both targets**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: `0 Warning(s) 0 Error(s)`.

- [ ] **Step 4: Manual verify — check-in persists across restart**

Relaunch the app. Sign in. Tap **Attendance**. Verify:

1. **Existing list renders**: 6 seed check-ins (or seeds + persisted from previous test runs).
2. **Suggestions still work**: type `le` → Lena Park appears in suggestions. Tap it → search field fills. Tap **CHECK IN** → green success `Checked in Lena Park at HH:MM AM.` New top row in Recent Check-ins.
3. **Quit + relaunch**: `pkill -f "Gymers/bin/Debug/net10.0-maccatalyst" 2>/dev/null; sleep 1; open Gymers/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Gymers.app`
4. Sign in. Tap **Attendance**. The Lena Park check-in is **still at the top** of Recent Check-ins.
5. **Empty + unknown still error**: tap CHECK IN with no selection → red `Select a member first.` Type `Bob` → suggestions hide (no matches). Tap CHECK IN → red `Select a member first.`
6. **Cross-tab persistence**: switch to Members → Dashboard → back to Attendance — Lena Park row still on top.

- [ ] **Step 5: Commit**

```bash
git add Gymers/Data/DataStore.cs Gymers/Pages/AttendancePage.xaml.cs
git commit -m "feat(attendance): persist check-ins to SQLite

RecordCheckInAsync inserts via SQLiteAsyncConnection and updates
the ObservableCollection on the UI thread. The row survives app
restart. AttendancePage.OnCheckIn becomes async void and awaits
the new method."
```

---

## Task 6: Final Verification Walk

After Task 5 is committed, run the full demo end-to-end in one launch session, exercising all three persistence paths from a clean DB. This catches anything one task may have inadvertently broken in another and proves the slice is complete.

- [ ] **Step 1: Wipe the DB and rebuild fresh**

```bash
DB=$(find ~/Library/Containers -name "gymers.db3" 2>/dev/null | head -1)
[ -n "$DB" ] && rm "$DB" && echo "Removed $DB" || echo "No DB to remove"
pkill -f "Gymers/bin/Debug/net10.0-maccatalyst" 2>/dev/null; sleep 1
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
open Gymers/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Gymers.app
```

- [ ] **Step 2: First-launch seed verification**

Sign in `admin / admin123`. Verify each tab in turn:

- **Members** — 6 rows (Marcus, Lena, Diego, Aisha, Sam, Priya). Search works (`lena`, `zzz`).
- **Payments** — 5 rows newest-first; top is Receipt #1042 (Marcus Sterling, $99.00, Card).
- **Attendance** — 6 rows newest-first; top is Marcus Sterling check-in.

- [ ] **Step 3: Mutate then restart**

Record a payment: `Diego Alvarez / 50 / BANK` → success `Recorded $50.00 · Receipt #1043.` Top row in Recent Payments shows `Diego Alvarez · $50.00 · Bank · Receipt #1043`.

Record a check-in: search `aisha`, tap suggestion `Aisha Khan`, tap CHECK IN → success. Top row in Recent Check-ins shows `Aisha Khan · Checked in · HH:MM AM`.

Quit and relaunch:

```bash
pkill -f "Gymers/bin/Debug/net10.0-maccatalyst" 2>/dev/null; sleep 1
open Gymers/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Gymers.app
```

Sign in. Verify:

- **Payments** top row is still `Diego Alvarez · $50.00 · Bank · Receipt #1043`.
- **Attendance** top row is still `Aisha Khan · Checked in · …`.

- [ ] **Step 4: Validation regression sweep**

In one session (post-restart), run a quick error-path sweep. Each must still behave exactly as before persistence:

- Login: empty fields → red `Enter username and password.` Wrong creds → red `Invalid credentials for the selected role.`
- Members: type `lena` → only Lena visible. Type `zzz` → muted `No members match "zzz".`
- Payments: empty member → red `No member named "". …`. `Lena Park / 0 / card` → red amount error. `Lena Park / 25 / Crypto` → red method error.
- Attendance: empty search, tap CHECK IN → red `Select a member first.` Unknown name `Bob` → no suggestions, CHECK IN → red `Select a member first.`

- [ ] **Step 5: iOS target builds clean**

The slice's verification target is Mac Catalyst, but iOS-target builds must remain green:

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: `0 Warning(s) 0 Error(s)`.

- [ ] **Step 6: Status doc — flip "SQLite database integration" to Completed**

Edit two files:

`docs/status/build_status_docx.py` — find the bullet in `Ongoing Tasks`:

```python
bullet("SQLite database integration: the in-memory DataStore needs to be replaced with persistent SQLite tables so records survive app restarts."),
```

Move it to the `completed_rows` list as a new row:

```python
["SQLite persistence",
 "Completed",
 "DataStore is now SQLite-backed via sqlite-net-pcl. Members, payments, and check-ins persist across app restart in gymers.db3 under FileSystem.AppDataDirectory. Bootstrap seeds from SampleData on first run; runtime mutations write through SQLiteAsyncConnection."],
```

`docs/status/gymers-mobile-app-status-update.html` — make the equivalent change: remove the SQLite item from the `Ongoing Tasks` `<ul>` and add a matching row to the Completed Features table.

Regenerate the .docx:

```bash
python3 docs/status/build_status_docx.py
```

- [ ] **Step 7: Final commit**

```bash
git add docs/status/build_status_docx.py docs/status/gymers-mobile-app-status-update.html
git commit -m "docs(status): mark SQLite persistence as completed

Slice landed. DataStore is now SQLite-backed; members, payments,
and check-ins survive app restart. Status doc updated to move the
item from Ongoing Tasks to Completed Features."
```

---

## Self-review notes (for the implementer)

- **Two SQLite connections to the same file:** `GymersDb` opens a sync `SQLiteConnection` for ctor bootstrap and lazily opens an async `SQLiteAsyncConnection` for runtime inserts. Both target the same `gymers.db3`. SQLite handles concurrent connections via OS file locks; with WAL journaling (sqlite-net's default since 1.6+) this is safe for a single-user app. If you see `SQLITE_BUSY` errors, switch the runtime path to use the sync connection too.
- **`MainThread` namespace:** if the build complains about `MainThread.InvokeOnMainThreadAsync` not resolving, add `using Microsoft.Maui.ApplicationModel;` to the top of `DataStore.cs`. MAUI usually pulls it in via implicit usings, but the implicit set varies by project type.
- **`DateOnly` parsing:** always use `DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture)`. Locale-aware parsing can flip month/day on some systems.
- **Decimal storage:** always pass `CultureInfo.InvariantCulture` to `decimal.ToString` and `decimal.Parse`. A device with comma decimal separators will silently corrupt amounts otherwise.
- **`Insert(0, …)` ordering:** `GymersDb.GetPaymentsNewestFirst()` and `GetCheckInsNewestFirst()` already sort `OrderByDescending(r => r.AtTicks)`. Runtime inserts use `Insert(0, …)`. So the list is always newest-first. Don't re-sort on render.
- **`async void` event handlers:** standard for MAUI button `Clicked +=`. Exceptions inside become unhandled async-void exceptions per spec §10. v1 accepts that — wrap in `try/catch` later only if reliability becomes a real concern.
- **No tests:** verification is manual, matching the predecessor "make it real" plan's precedent. Don't add xUnit. The spec deliberately scoped tests out.
- **No XAML changes:** if you find yourself editing `.xaml` files, you've drifted from this plan — the only XAML change in the make-it-real era was naming inputs, which is already done.
- **iOS sim:** don't try to verify on the iOS simulator. It's unusable on this hardware (sustained UI lag). Mac Catalyst is the verification target. iOS builds must still succeed though — Step 5 of Task 6 covers that.
- **DB path on Mac Catalyst:** lives somewhere under `~/Library/Containers/com.companyname.gymers/Data/...` — use `find ~/Library/Containers -name "gymers.db3"` to locate it. Don't try to construct the path manually; it differs subtly between macOS versions.
