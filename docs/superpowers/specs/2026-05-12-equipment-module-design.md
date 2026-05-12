# Equipment Module — Design

|       |       |
|-------|-------|
| **Date**        | 2026-05-12 |
| **Status**      | Approved |
| **Slice**       | Third and final of the deferred modules (Trainer / Workout Plan / **Equipment**) |
| **Predecessor** | `2026-05-11-workout-module-design.md` |

## 1. Context

The trainer module shipped 2026-05-10 and the workout-plan module shipped 2026-05-11, leaving Equipment as the last README scope gap. Equipment is sequenced last because nothing depends on it — no other slice carries a foreign key into the equipment table — so it can land cleanly at the end without rework on previous slices.

This slice mirrors the trainer and workout-plan slices nearly line-for-line: SQLite-backed read-only roster + name search + dashboard hook + Shell route reached via a button (no 6th BottomTabBar pill). The locked decisions, the avatar helper, and the empty-state pattern all carry over.

## 2. Goals

1. Persist a roster of gym equipment items in SQLite, seeded on first run.
2. New **EquipmentPage** — read-only list with a live name-search filter (mirrors TrainersPage / WorkoutsPage).
3. Dashboard gains an **Equipment Status** card showing operational vs. maintenance counts with a `VIEW EQUIPMENT` button → `//Equipment`.
4. Build clean (0 warnings, 0 errors) on `net10.0-ios` and `net10.0-maccatalyst`; persistence verified across restart on Mac Catalyst.

## 3. Non-Goals

- No 6th BottomTabBar pill — same locked decision as Trainers and Workouts (keep the 5-column bar visually breathable on phone width).
- **No equipment ↔ workout-plan linkage.** Future slice (the workout-plan spec already deferred this explicitly).
- No member assignment, reservations, or booking. Future slice.
- No create / edit / delete equipment in the app. Read-only over seed data, matching all other deferred-module slices.
- No per-item detail page. List subtitle carries everything (`category · status · location`).
- No service history, depreciation, or warranty fields. Status is a simple enum-like string for the demo.

## 4. Locked Decisions

| # | Decision | Rationale |
|---|---|---|
| 1 | **Equipment = name + category + status + location + order** (six fields including Id) | Matches workout-plan's footprint; enough to render an informative row without leaking into a detail page. |
| 2 | **`Status` is a free-form string** (`"Operational"`, `"Maintenance"`, `"Out of Service"`) | Same shape as `Member.Status` and `Trainer.Title`; no enum table needed for a demo with 3 categories. |
| 3 | **`Category` likewise a free-form string** (`"Cardio"`, `"Strength"`, `"Free Weights"`, `"Studio"`) | Demo-scale; mirrors Trainer.Title and matches the Dashboard's existing zone labels (Cardio Zone, Weight Room, Yoga Studio, Pool Area). |
| 4 | **`OrderRank` controls display order**; lowest rank renders first | Lets seed data put the "headline" equipment up top without an algorithmic ranking model. |
| 5 | **EquipmentPage mirrors WorkoutsPage line-for-line** | Reuses `AvatarFactory.MakeInitial`, the same search/empty-state pattern, the same `BottomTabBar(ActiveTab=...)` no-pill trick. |
| 6 | **Dashboard hook = new Equipment Status card under Featured Workout Plan** | Mirrors the workout/trainer card stack; one new XAML block + ctor wiring. The three feature cards stay visually parallel. |
| 7 | **`AppTab.Equipment` enum value with no pill rendered** | Same decision as Trainers and Workouts — additive enum value, `ApplyActive` returns transparent for any value not in the existing five pills. |
| 8 | **6 seeded items** (5 operational, 1 under maintenance) | Provides one non-trivial status to render distinctly in the KPI; mix of categories so the search filter is exercised. |
| 9 | **No dedicated icon glyph; reuse `Icons.Users`** (or any existing glyph) as the TopAppBar trailing icon | Same trade-off as Workouts; adding a fresh `Icons.Dumbbell` / `Icons.Wrench` glyph is a 2-line follow-up and not needed for the slice to ship. |
| 10 | **Status colours not encoded yet** — status renders as plain text in the subtitle | Keeps the slice tight; a Maintenance pill colour can be added in a 5-line polish pass if there's time after smoke-test. |

## 5. Architecture

Layer-by-layer the slice slots into existing patterns; nothing new structurally.

### 5.1 Model

```csharp
// Gymers/Models/Equipment.cs
public record Equipment(
    string Id,
    string Name,        // "Treadmill TR-01"
    string Category,    // "Cardio" | "Strength" | "Free Weights" | "Studio"
    string Status,      // "Operational" | "Maintenance" | "Out of Service"
    string Location,    // "Cardio Zone" | "Weight Room" | "Yoga Studio" | "Pool Area"
    int    OrderRank);  // display order asc; lowest = featured
```

### 5.2 SQLite row + DB

```csharp
// Gymers/Data/Rows/EquipmentRow.cs
public class EquipmentRow
{
    [PrimaryKey] public string Id { get; set; } = "";
    public string Name      { get; set; } = "";
    public string Category  { get; set; } = "";
    public string Status    { get; set; } = "";
    public string Location  { get; set; } = "";
    public int    OrderRank { get; set; }
}
```

`GymersDb` gains:
- `_sync.CreateTable<EquipmentRow>()` in the constructor.
- `IsEquipmentEmpty()`, `SeedEquipment(IEnumerable<Equipment>)`, `GetEquipmentOrdered()` (`ORDER BY OrderRank ASC`).
- `ToRow(Equipment)` / `ToRecord(EquipmentRow)` mirroring the existing pattern (all string + int columns — no decimal, no culture concern).

### 5.3 DataStore

```csharp
public ObservableCollection<Equipment> Equipment { get; }

// ctor, after WorkoutPlans load:
if (_db.IsEquipmentEmpty()) _db.SeedEquipment(SampleData.Equipment);
Equipment = new ObservableCollection<Equipment>(_db.GetEquipmentOrdered());

public IEnumerable<Equipment> SearchEquipment(string? query) =>
    string.IsNullOrWhiteSpace(query)
        ? Equipment
        : Equipment.Where(e =>
            e.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

public int OperationalEquipmentCount() =>
    Equipment.Count(e => string.Equals(e.Status, "Operational", StringComparison.OrdinalIgnoreCase));

public int MaintenanceEquipmentCount() =>
    Equipment.Count(e => !string.Equals(e.Status, "Operational", StringComparison.OrdinalIgnoreCase));
```

(Two summary helpers feed the Dashboard card without duplicating the LINQ at the call site.)

### 5.4 Sample data

```csharp
// Gymers/Data/SampleData.cs
public static readonly IReadOnlyList<Equipment> Equipment = new[]
{
    new Equipment("e1", "Treadmill TR-01",     "Cardio",       "Operational", "Cardio Zone",  1),
    new Equipment("e2", "Treadmill TR-02",     "Cardio",       "Operational", "Cardio Zone",  2),
    new Equipment("e3", "Power Rack PR-A",     "Strength",     "Operational", "Weight Room",  3),
    new Equipment("e4", "Smith Machine SM-01", "Strength",     "Maintenance", "Weight Room",  4),
    new Equipment("e5", "Spin Bike SB-03",     "Cardio",       "Operational", "Cardio Zone",  5),
    new Equipment("e6", "Yoga Mat Set YM-01",  "Studio",       "Operational", "Yoga Studio",  6),
};
```

Six items, one (`e4`) in Maintenance so the Dashboard breakdown reads `5 operational · 1 maintenance`.

### 5.5 EquipmentPage

XAML mirrors WorkoutsPage almost line-for-line:

```
TopAppBar(Title="Equipment", TrailingIcon=Users)
  ScrollView
    SearchField(Placeholder="Search by name…")
    KpiCard(Variant=Light, Label="Operational", Value="<count>",
            Caption="of <total> items", TrailingIcon=Users)
    H2Section("All Equipment")
    VerticalStackLayout x:Name="EquipmentList"
BottomTabBar(ActiveTab=Equipment)
```

`Shell.NavBarIsVisible="False"` is set on the page (matches all other tab/route pages). `BottomTabBar(ActiveTab=Equipment)` renders all five existing pills unhighlighted; the user taps any pill to leave the page.

Code-behind mirrors WorkoutsPage exactly:
- ctor receives `DataStore` via DI.
- Subscribes to `Search.PropertyChanged` and `_data.Equipment.CollectionChanged`.
- `Render(query)` rebuilds rows via `_data.SearchEquipment(query)`.
- Empty state: `"No equipment matches \"<query>\"."` muted label.
- Row builder: `ListRow` with `AvatarFactory.MakeInitial(item.Name)`.
- Subtitle: `$"{e.Category} · {e.Status} · {e.Location}"`.
- Title: `e.Name`.

### 5.6 Dashboard wiring

Two additions, parallel to the workout-plan slice's Featured Plan card:

1. **New "Equipment Status" card** inserted directly **below the Featured Workout Plan card** in `DashboardPage.xaml`:
   ```
   H2Section("Equipment Status")
   Big stat row
     ┌ "<operational> / <total>"    (H3, large numeral)
     └ "Operational"                (BodyMd, muted, label)
   Meta line: "<maintenance> under maintenance"
   Summary: "Active fleet across cardio, strength, and studio zones."
   PrimaryButton "VIEW EQUIPMENT"
   ```
   Leaf labels are `x:Name`'d (`EquipmentOperationalCount`, `EquipmentTotalCount`, `EquipmentMaintenanceMeta`) so the code-behind can set them imperatively.

2. **Code-behind** in `DashboardPage`:
   - `ApplyEquipmentStatus()` reads `_data.OperationalEquipmentCount()`, `_data.Equipment.Count`, `_data.MaintenanceEquipmentCount()`.
   - If total is 0: muted `"No equipment configured."` fallback, hide the meta + summary + button (parallels the existing empty-trainers / empty-plans fallbacks).
   - `BrowseEquipmentButton.Clicked` → `Shell.Current.GoToAsync("//Equipment")`.

`DashboardPage` already receives `DataStore` via DI; no ctor signature change.

### 5.7 AppShell + DI

`AppShell.xaml` adds Equipment as a Shell route, **outside the `<TabBar>`** (mirrors Trainers + Workouts):

```xml
<ShellContent Route="Equipment"
              ContentTemplate="{DataTemplate pages:EquipmentPage}" />
```

`MauiProgram.cs` adds:

```csharp
builder.Services.AddTransient<Pages.EquipmentPage>();
```

### 5.8 BottomTabBar

One additive change: extend `AppTab` with an `Equipment` value. **No new pill is rendered** — `ApplyActive` returns the active-fill colour only when the enum matches one of the five existing pills; any other value (Trainers, Workouts, now Equipment) leaves all pills transparent.

```csharp
public enum AppTab { Dashboard, Members, Payments, Attendance, Reports, Trainers, Workouts, Equipment }
```

No changes to `BottomTabBar.xaml`, no changes to the `ApplyActive` body, no new tap handler.

## 6. Data Flow

```
                  ┌───────────────────────────────┐
                  │ SQLite (gymers.db3)           │
                  │ + equipment table             │
                  └──────────────┬────────────────┘
                                 │ seeded once from SampleData.Equipment
                                 ▼
                  ┌───────────────────────────────┐
                  │ DataStore.Equipment           │
                  │ (ObservableCollection,         │
                  │  OrderRank ASC on load)       │
                  └─────┬──────────────────┬──────┘
                        │                  │
                        │                  │ OperationalEquipmentCount(),
                        │                  │ MaintenanceEquipmentCount()
                        ▼                  ▼
              ┌──────────────────┐   ┌──────────────────────────────┐
              │ EquipmentPage    │   │ DashboardPage                │
              │ list + search    │   │ Equipment Status card        │
              │                  │   │ "VIEW EQUIPMENT" → //Equipment │
              └──────────────────┘   └──────────────────────────────┘
```

Equipment is read-only at runtime in this slice — no mutations beyond first-run seed — so no write-through, no `MainThread.InvokeOnMainThreadAsync`, no `CollectionChanged`-triggered re-render needed beyond the initial pass.

## 7. Error Handling / Edge Cases

| Case | Handling |
|---|---|
| Empty equipment table after manual DB wipe | `OperationalEquipmentCount()` and `Equipment.Count` both return 0; Dashboard card shows `"No equipment configured."` and hides meta/summary/button. |
| All items operational | Meta line shows `"0 under maintenance"` (intentional — still renders, just zero). |
| Search yields zero matches | Muted `"No equipment matches \"…\"."` label, identical to Members / Trainers / Workouts. |
| Existing demo DB lacks the equipment table | `CreateTable<EquipmentRow>()` is idempotent; first launch after this slice creates the table and seeds it from `SampleData.Equipment`. |
| Status string with unexpected value (e.g. `"Retired"`) | Counted as non-operational by `MaintenanceEquipmentCount()` (it returns `Equipment.Count - operational`). |

## 8. Testing & Verification

Manual verification, same shape as the workout-plan slice:

1. **Build clean**: `dotnet build` for `net10.0-maccatalyst` → 0 warnings, 0 errors. Repeat for `net10.0-ios`.
2. **Smoke-test on Mac Catalyst** (per the smoke-test memory — `dotnet build` doesn't catch static-cctor crashes):
   - Launch app, sign in as admin.
   - Dashboard loads. Coach Spotlight, Featured Workout Plan still render correctly.
   - **Equipment Status** card shows `5 / 6` Operational, `1 under maintenance`.
   - Tap **VIEW EQUIPMENT** → EquipmentPage opens, list renders 6 rows in OrderRank order; top row is `Treadmill TR-01`.
   - KPI card on EquipmentPage shows `Operational: 5 of 6 items`.
   - Type `smith` into search → list filters to a single row (`Smith Machine SM-01`).
   - Type `zzz` → empty-state label.
   - Tap any BottomTabBar pill → navigates out of Equipment cleanly.
   - Force-quit and relaunch → equipment persists (table seeded once, not re-seeded).
3. **Existing flows still work**: Members search, payment recording + PDF receipt, check-in, report PDF/CSV export, Trainers list + Dashboard Coach Spotlight, Workouts list + Featured Plan — all unchanged.
4. **Screenshot capture**: a new `09-equipment.png` for the status doc (mirrors `08-workouts.png` shape).

No automated test harness in this iteration; broader test coverage remains on the Ongoing Tasks list.

## 9. Risks / Trade-offs

- **Read-only Equipment is thin.** Same trade-off as Trainers and Workouts; sized for ship in <1 day and gives a future "Equipment CRUD + maintenance log" slice clean room to land.
- **No equipment ↔ workout-plan linkage yet.** Acknowledged — the workout-plan spec explicitly deferred this. Linkage table would be a single child row + UI tweak.
- **Status is a string, not an enum.** Trivially upgradable to a typed enum + custom-renderer pill if the demo gets review feedback on it.
- **No dedicated icon glyph.** TopAppBar reuses `Icons.Users` (same as Workouts); a `Dumbbell` / `Wrench` glyph addition is a follow-up.
- **Imperative code-behind, no MVVM.** Consistent with the rest of the app.

## 10. Out of Scope (future slices)

- **Equipment CRUD**: create / edit / retire equipment from the app itself.
- **Maintenance log + service history** with timestamped entries.
- **Workout Plan ↔ Equipment** linkage (which plan uses which equipment).
- **Equipment reservation / booking** by members.
- **Role-based UI** (admin vs staff actually doing different things).
- Adding Equipment as a 6th BottomTabBar pill — would require a layout pass on phone width.
- Test harness + broader visual polish.
