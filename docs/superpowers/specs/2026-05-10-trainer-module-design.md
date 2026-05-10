# Trainer Module — Design

|       |       |
|-------|-------|
| **Date**       | 2026-05-10 |
| **Status**     | Draft (pending user review) |
| **Slice**      | First of the deferred modules (Trainer / Workout / Equipment) |
| **Predecessor**| `2026-05-10-reports-export-design.md` |

## 1. Context

The app ships six core screens persisted to SQLite, with PDF receipts and Reports/CSV export. The original proposal scope still has three deferred modules: **Trainer**, **Workout Plan**, **Equipment**. This spec covers Trainer only — the smallest of the three and a prerequisite for Workout Plans (which need a trainer FK).

The Dashboard already has a **Coach Spotlight** card with hardcoded data (Marcus Sterling, "Lead Performance Coach", 4.9/5.0, 142 sessions) and a **VIEW PERFORMANCE PROFILE** button that currently does nothing. This slice gives that data a real source and that button a real destination.

## 2. Goals

1. Persist a roster of trainers in SQLite, seeded on first run (mirrors the Members lifecycle).
2. Replace the hardcoded Coach Spotlight on Dashboard with the top-rated trainer from the store.
3. Add a **TrainersPage** with a name-search list (read-only, mirrors MembersPage exactly).
4. Wire the Dashboard's **VIEW PERFORMANCE PROFILE** button to navigate to TrainersPage.
5. Keep the build clean (0 warnings, 0 errors) on iOS + Mac Catalyst.

## 3. Non-Goals

- Adding a 6th BottomTabBar pill. The 5-column bar is already tight on phone width; Trainers is reached via Dashboard navigation, not a peer tab.
- Per-trainer detail page. The Coach Spotlight card already shows rich data for the top trainer; the list is name + title + rating only.
- Create / edit / delete trainers. Read-only over seed data, matching MembersPage.
- Trainer photos. Initials avatar (matches MembersPage convention).
- Linking trainers to members, classes, or workouts. That belongs to the Workout Plan slice.

## 4. Locked Decisions

| # | Decision | Rationale |
|---|---|---|
| 1 | **Read-only list + name search**         | Mirrors MembersPage exactly; minimum surface area. |
| 2 | **No 6th BottomTabBar pill**             | 5-column bar is visually tight; adding a 6th hurts phone-width layout. |
| 3 | **Reached via Dashboard "VIEW PERFORMANCE PROFILE"** | Wires an existing dead button; navigation is consistent with the app's "spotlight → list" pattern. |
| 4 | **Coach Spotlight = top-rated trainer**  | Replaces hardcoded data with a real DB query; keeps the dashboard alive after schema changes. |
| 5 | **Initials avatar (no photos)**          | Matches MembersPage; no MediaPicker dependency. |
| 6 | **TrainersPage renders BottomTabBar with `ActiveTab=Trainers` (no pill highlighted)** | `TopAppBar` doesn't have a leading-icon slot, so a back-arrow would mean extending a shared control. Cheaper: add an `AppTab.Trainers` enum value with no pill — all five existing pills render unhighlighted, and any pill taps navigate the user out. One-line change, no back-arrow ceremony, no new icon. |
| 7 | **Sample data includes "Marcus Sterling"** | Demo continuity — the dashboard's spotlighted name doesn't change between this slice and the previous build. |

## 5. Architecture

Layer-by-layer the slice slots into existing patterns. Nothing new structurally; just a new aggregate.

### 5.1 Model

```csharp
// Gymers/Models/Trainer.cs
public record Trainer(
    string  Id,
    string  Name,
    string  Title,            // e.g. "Lead Performance Coach"
    decimal Rating,           // 0.0 – 5.0, one decimal
    int     SessionsCompleted);
```

### 5.2 SQLite row + DB

```csharp
// Gymers/Data/Rows/TrainerRow.cs
public class TrainerRow
{
    [PrimaryKey] public string Id { get; set; } = "";
    public string Name              { get; set; } = "";
    public string Title             { get; set; } = "";
    public string RatingText        { get; set; } = "";   // invariant-culture decimal, mirrors PaymentRow
    public int    SessionsCompleted { get; set; }
}
```

`GymersDb` gains:
- `_sync.CreateTable<TrainerRow>()` in the constructor.
- `IsTrainersEmpty()`, `SeedTrainers(IEnumerable<Trainer>)`, `GetTrainersByRatingDesc()`.
- `ToRow(Trainer)` / `ToRecord(TrainerRow)` mirroring the existing pattern (decimals serialized as `InvariantCulture` strings, like `PaymentRow.AmountText`).

### 5.3 DataStore

```csharp
public ObservableCollection<Trainer> Trainers { get; }

// in ctor, after Members/Payments/CheckIns are loaded:
if (_db.IsTrainersEmpty()) _db.SeedTrainers(SampleData.Trainers);
Trainers = new ObservableCollection<Trainer>(_db.GetTrainersByRatingDesc());

// new search helper, mirrors SearchMembers
public IEnumerable<Trainer> SearchTrainers(string? query) =>
    string.IsNullOrWhiteSpace(query)
        ? Trainers
        : Trainers.Where(t =>
            t.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

// convenience for Dashboard
public Trainer TopTrainer() => Trainers.First();   // list is already rating-desc
```

### 5.4 Sample data

```csharp
// Gymers/Data/SampleData.cs
public static readonly IReadOnlyList<Trainer> Trainers = new[]
{
    new Trainer("t1", "Marcus Sterling", "Lead Performance Coach", 4.9m, 142),
    new Trainer("t2", "Sienna Vega",     "HIIT Specialist",        4.8m, 118),
    new Trainer("t3", "Rohan Iyer",      "Strength Coach",         4.7m,  96),
    new Trainer("t4", "Maya Okafor",     "Yoga Instructor",        4.7m,  88),
    new Trainer("t5", "Caleb Whit",      "Mobility Coach",         4.5m,  64),
};
```

Marcus Sterling stays at the top so the existing Coach Spotlight visuals don't shift. Demo continuity wins over "shake things up."

### 5.5 TrainersPage

XAML mirrors MembersPage almost line-for-line:

```
TopAppBar(Title="Trainers", TrailingIcon=Users)
  ScrollView
    SearchField(Placeholder="Search by name…")
    KpiCard(Variant=Light, Label="Active Trainers", Value="<count>",
            DeltaText="+2", DeltaDirection=Up, Caption="this month",
            TrailingIcon=Users)
    H2Section("All Trainers")
    VerticalStackLayout x:Name="TrainerList"
BottomTabBar(ActiveTab=Trainers)                       ← new enum value, no pill
```

`Shell.NavBarIsVisible="False"` is set on the page (matches all other tab pages). The `BottomTabBar` renders all five existing pills unhighlighted when `ActiveTab=Trainers`; the user taps any pill to leave the page.

Code-behind mirrors MembersPage exactly:
- ctor receives `DataStore` via DI
- subscribes to `Search.PropertyChanged` and `_data.Trainers.CollectionChanged`
- `Render(query)` rebuilds rows via `_data.SearchTrainers(query)`
- empty state: `"No trainers match \"<query>\"."` muted label (verbatim same pattern as Members)
- row builder: `ListRow` with `MakeInitialAvatar(name)` (lift `MakeInitialAvatar` from MembersPage into a small static helper on a new `Controls/AvatarFactory.cs` so both pages share it; this is the one targeted refactor justified by the slice)
- subtitle: `$"{t.Title} · {t.Rating:0.0}/5.0 · {t.SessionsCompleted} sessions"`

### 5.6 Dashboard wiring

Two changes to `DashboardPage`:

1. **Coach Spotlight** card stops being hardcoded XAML; ctor reads `_data.TopTrainer()` and binds:
   - Initials label  ← split `t.Name` on whitespace; if 2+ parts, take first letter of parts 0 and 1; otherwise first 2 chars of the only part. Sample data all have two-part names, so the spotlight reads "MS" for Marcus Sterling.
   - Name label      ← `t.Name`
   - Title label     ← `t.Title`
   - Rating value    ← `$"{t.Rating:0.0}/5.0"`
   - Sessions value  ← `t.SessionsCompleted.ToString("N0")`

   The XAML stays largely intact — the card structure is fine, only the four leaf labels become `x:Name`'d targets that the code-behind sets in `OnAppearing`. (Avoid `OneTime` bindings + a VM rewrite; the rest of the app is imperative code-behind, this slice should match.)

2. The **VIEW PERFORMANCE PROFILE** `PrimaryButton` gets a `Clicked` handler that calls `Shell.Current.GoToAsync("//Trainers")`. Since Trainers is registered as a Shell route (see §5.7), the Shell can navigate to it.

`DashboardPage` ctor now also takes `DataStore` via DI.

### 5.7 AppShell + DI

`AppShell.xaml` adds Trainers as a Shell route, but **outside the `<TabBar>`** (so no bottom-bar pill is implied):

```xml
<ShellContent Route="Trainers"
              ContentTemplate="{DataTemplate pages:TrainersPage}" />
```

`MauiProgram.cs` adds:

```csharp
builder.Services.AddTransient<Pages.TrainersPage>();
```

DashboardPage's existing transient registration is unchanged; its constructor signature gets `DataStore data` added (same pattern as MembersPage).

### 5.8 BottomTabBar

One additive change: extend `AppTab` with a `Trainers` value. **No new pill is rendered** — `ApplyActive` already does `ActiveTab == AppTab.Dashboard ? pale : Colors.Transparent` per pill, so a fresh `AppTab.Trainers` value naturally leaves every existing pill unhighlighted. The user taps any pill to leave TrainersPage.

```csharp
public enum AppTab { Dashboard, Members, Payments, Attendance, Reports, Trainers }
```

No changes to `BottomTabBar.xaml`, no changes to the `ApplyActive` body, no new tap handler.

## 6. Data Flow

```
                 ┌──────────────────────────────┐
                 │ SQLite (gymers.db3)          │
                 │ + trainers table             │
                 └────────────┬─────────────────┘
                              │ seeded once from SampleData.Trainers
                              ▼
                 ┌──────────────────────────────┐
                 │ DataStore.Trainers           │
                 │ (ObservableCollection,        │
                 │  rating-desc on load)        │
                 └────┬───────────────┬─────────┘
                      │               │
                      │               │ TopTrainer()
                      ▼               ▼
            ┌──────────────────┐   ┌──────────────────────────┐
            │ TrainersPage     │   │ DashboardPage            │
            │ list + search    │   │ Coach Spotlight (bound)  │
            │                  │   │ "VIEW PROFILE" → Shell   │
            └──────────────────┘   └──────────────────────────┘
```

`Trainers` is read-only at runtime in this slice — no mutations beyond first-run seed — so there's no write-through, no `MainThread.InvokeOnMainThreadAsync`, no `CollectionChanged`-triggered re-render needed beyond the initial pass.

## 7. Error Handling / Edge Cases

| Case | Handling |
|---|---|
| Empty trainers table after a manual DB wipe | `TopTrainer()` would throw; guard with `Trainers.FirstOrDefault()` and, if null, fall back to a "No trainers configured" placeholder card on Dashboard. |
| Search yields zero matches                  | Muted "No trainers match \"…\"." label, identical to MembersPage. |
| Rating parses back to wrong locale          | Persist as `InvariantCulture` decimal string, parse the same way (mirrors `PaymentRow.AmountText`). |
| Existing demo DB lacks the trainers table   | `CreateTable<TrainerRow>()` is idempotent; first launch after this slice creates the table and seeds it. |

## 8. Testing & Verification

Manual verification (in the spirit of the existing slices):

1. **Build clean**: `dotnet build` for `net10.0-maccatalyst` reports 0 warnings, 0 errors.
2. **Smoke-test on Mac Catalyst** (per the user's smoke-test memory — green build doesn't catch static-cctor crashes):
   - Launch app, sign in as admin.
   - Dashboard loads, Coach Spotlight shows "Marcus Sterling / Lead Performance Coach / 4.9/5.0 / 142".
   - Tap **VIEW PERFORMANCE PROFILE** → TrainersPage opens, list renders 5 rows, top is Marcus Sterling.
   - Type "vega" → list filters to Sienna Vega.
   - Type "zzz" → empty-state label.
   - Tap back arrow → returns to Dashboard with state intact.
   - Force-quit and relaunch → trainers persist (table seeded once, not re-seeded).
3. **Existing flows still work**: Members search, payment recording (PDF receipt), check-in, report PDF/CSV export — all unchanged.

No automated test harness in this iteration; broader test coverage stays on the Ongoing Tasks list.

## 9. Risks / Trade-offs

- **Imperative code-behind, no MVVM rewrite.** Consistent with the rest of the app; chosen over `BindingContext` + VM because the slice is small and parity matters more than ideology. If a future slice introduces MVVM, Trainer is trivially portable.
- **`MakeInitialAvatar` extraction is the only refactor.** Lifting it from MembersPage into `Controls/AvatarFactory.cs` removes a soon-to-be-duplicated helper. Anything beyond that (e.g., generic `EntityListPage<T>`) is premature.
- **Coach Spotlight code-behind binding (not XAML data binding).** Matches the rest of the codebase's imperative style. Pure-XAML binding would be cleaner long-term but introduces a one-off pattern in this slice.

## 10. Out of Scope (future slices)

- **Workout Plans**: trainer FK on plans, exercises with sets/reps/weight, member assignment.
- **Equipment**: inventory CRUD with maintenance dates.
- Trainer per-page detail view, photos, schedules, ratings-over-time.
- Adding Trainers as a 6th BottomTabBar pill (would require a layout pass).
- Test harness + visual polish (the other Ongoing Task item).
