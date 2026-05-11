# Workout Plan Module — Design

|       |       |
|-------|-------|
| **Date**        | 2026-05-11 |
| **Status**      | Approved |
| **Slice**       | Second of the deferred modules (Trainer / **Workout Plan** / Equipment) |
| **Predecessor** | `2026-05-10-trainer-module-design.md` |

## 1. Context

The trainer module shipped on 2026-05-10, leaving Workout Plan and Equipment in the deferred queue. Workout Plan is the next slice and the largest remaining gap against the README. It depends on Trainer for the `TrainerId` foreign key, which is why Trainer was sequenced first.

This slice mirrors the trainer slice's shape almost line-for-line: SQLite-backed read-only roster + name search + dashboard hook + Shell route reached via a button (no 6th BottomTabBar pill). The locked decisions, the avatar helper, and the empty-state pattern all carry over.

## 2. Goals

1. Persist a roster of workout plans in SQLite, seeded on first run; each plan FK'd to a trainer.
2. New **WorkoutsPage** — read-only list with a live name-search filter (mirrors TrainersPage).
3. Dashboard gains a **Featured Plan** card mirroring Coach Spotlight's shape, with a `BROWSE WORKOUT PLANS` button → `//Workouts`.
4. Build clean (0 warnings, 0 errors) on `net10.0-ios` and `net10.0-maccatalyst`; persistence verified across restart on Mac Catalyst.

## 3. Non-Goals

- No 6th BottomTabBar pill — same locked decision as Trainers (keep the 5-column bar visually breathable on phone width).
- **No exercises inside plans** (no sets/reps/weight). A plan in this slice is a **named program with metadata** only; per-exercise breakdown is a future slice.
- No member assignment (plan → member link). Future slice.
- No create / edit / delete plans. Read-only over seed data, matching Trainers and Members.
- No per-plan detail page. List subtitle carries everything (`trainer · level · cadence · duration`).
- No equipment linkage. Equipment is the next deferred module after this one.

## 4. Locked Decisions

| # | Decision | Rationale |
|---|---|---|
| 1 | **Plan = metadata-only** (no exercises) | Keeps the slice ~1-day; exercises are a clean future iteration where breaking the schema is contained. |
| 2 | **Trainer FK by Id; trainer name resolved at render time via `DataStore.TrainerName(id)`** | Matches existing patterns (no eager joins in SQL); trainer renames propagate automatically. |
| 3 | **`TopPlan()` = first seeded plan by `OrderRank` ascending** | "Featured" is editorial, not algorithmic — `OrderRank` lets the seed data control which plan headlines without polluting the model with a `IsFeatured` bool. |
| 4 | **WorkoutsPage mirrors TrainersPage line-for-line** | Reuses `AvatarFactory.MakeInitial`, the same search/empty-state pattern, the same `BottomTabBar(ActiveTab=...)` no-pill trick. |
| 5 | **Dashboard hook = new Featured Plan card under Coach Spotlight** | Mirrors trainer's spotlight + button pattern; one new XAML block + ctor wiring. The two spotlights stay visually parallel. |
| 6 | **`AppTab.Workouts` enum value with no pill rendered** | Same decision as Trainers — additive enum value, `ApplyActive` already returns transparent for any value not in the existing pill list. |
| 7 | **One sample plan per seeded trainer (5 plans total)** | Demo balance — every seeded trainer "owns" a plan, so the trainer-name resolver is exercised on every row. |
| 8 | **Featured plan's `OrderRank=1` belongs to Marcus Sterling (trainer `t1`)** | Demo continuity — the Featured Plan card and Coach Spotlight tell the same visual story on Dashboard. |

## 5. Architecture

Layer-by-layer the slice slots into existing patterns; nothing new structurally.

### 5.1 Model

```csharp
// Gymers/Models/WorkoutPlan.cs
public record WorkoutPlan(
    string Id,
    string Name,             // "Foundations of Strength"
    string TrainerId,        // FK → Trainer.Id
    string Level,            // "Beginner" | "Intermediate" | "Advanced"
    int    SessionsPerWeek,  // 3..5
    int    DurationWeeks,    // 4, 6, 8, 12
    string Summary,          // one-line description
    int    OrderRank);       // display order asc; lowest = featured
```

### 5.2 SQLite row + DB

```csharp
// Gymers/Data/Rows/WorkoutPlanRow.cs
public class WorkoutPlanRow
{
    [PrimaryKey] public string Id { get; set; } = "";
    public string Name             { get; set; } = "";
    public string TrainerId        { get; set; } = "";
    public string Level            { get; set; } = "";
    public int    SessionsPerWeek  { get; set; }
    public int    DurationWeeks    { get; set; }
    public string Summary          { get; set; } = "";
    public int    OrderRank        { get; set; }
}
```

`GymersDb` gains:
- `_sync.CreateTable<WorkoutPlanRow>()` in the constructor.
- `IsWorkoutPlansEmpty()`, `SeedWorkoutPlans(IEnumerable<WorkoutPlan>)`, `GetWorkoutPlansOrdered()` (`ORDER BY OrderRank ASC`).
- `ToRow(WorkoutPlan)` / `ToRecord(WorkoutPlanRow)` mirroring the existing pattern (no decimal columns — no invariant-culture concern).

### 5.3 DataStore

```csharp
public ObservableCollection<WorkoutPlan> WorkoutPlans { get; }

// ctor, after Trainers load:
if (_db.IsWorkoutPlansEmpty()) _db.SeedWorkoutPlans(SampleData.WorkoutPlans);
WorkoutPlans = new ObservableCollection<WorkoutPlan>(_db.GetWorkoutPlansOrdered());

public IEnumerable<WorkoutPlan> SearchWorkoutPlans(string? query) =>
    string.IsNullOrWhiteSpace(query)
        ? WorkoutPlans
        : WorkoutPlans.Where(p =>
            p.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

public WorkoutPlan? TopPlan() => WorkoutPlans.FirstOrDefault();

public string TrainerName(string trainerId) =>
    Trainers.FirstOrDefault(t => t.Id == trainerId)?.Name ?? "—";
```

### 5.4 Sample data

```csharp
// Gymers/Data/SampleData.cs
public static readonly IReadOnlyList<WorkoutPlan> WorkoutPlans = new[]
{
    new WorkoutPlan("p1", "Foundations of Strength",  "t1", "Beginner",     3, 6,
        "Compound lifts and bracing fundamentals for new gym members.",        1),
    new WorkoutPlan("p2", "HIIT Conditioning Cycle",  "t2", "Intermediate", 4, 4,
        "Four-week conditioning block built around 20-min HIIT circuits.",     2),
    new WorkoutPlan("p3", "Power Build 8-Week",       "t3", "Advanced",     5, 8,
        "Heavy-day / volume-day split for intermediate-to-advanced lifters.",  3),
    new WorkoutPlan("p4", "Mindful Mobility Series",  "t4", "Beginner",     3, 6,
        "Yoga-anchored mobility and breathwork; recovery between sessions.",   4),
    new WorkoutPlan("p5", "Active Recovery Block",    "t5", "Intermediate", 3, 4,
        "Low-intensity flow + mobility for deload weeks.",                     5),
};
```

`p1` (OrderRank=1) is the featured plan, intentionally owned by trainer `t1` (Marcus Sterling) so the Dashboard's Coach Spotlight and Featured Plan card line up.

### 5.5 WorkoutsPage

XAML mirrors TrainersPage almost line-for-line:

```
TopAppBar(Title="Workout Plans", TrailingIcon=Dumbbell)
  ScrollView
    SearchField(Placeholder="Search by name…")
    KpiCard(Variant=Light, Label="Active Plans", Value="<count>",
            Caption="curated", TrailingIcon=Dumbbell)
    H2Section("All Plans")
    VerticalStackLayout x:Name="PlanList"
BottomTabBar(ActiveTab=Workouts)
```

`Shell.NavBarIsVisible="False"` is set on the page (matches all other tab/route pages). `BottomTabBar(ActiveTab=Workouts)` renders all five existing pills unhighlighted; the user taps any pill to leave the page.

Code-behind mirrors TrainersPage exactly:
- ctor receives `DataStore` via DI.
- Subscribes to `Search.PropertyChanged` and `_data.WorkoutPlans.CollectionChanged`.
- `Render(query)` rebuilds rows via `_data.SearchWorkoutPlans(query)`.
- Empty state: `"No plans match \"<query>\"."` muted label (same pattern as Members/Trainers).
- Row builder: `ListRow` with `AvatarFactory.MakeInitial(plan.Name)` (avatar shows initials from plan name's first two whitespace-separated parts, falling back to first two chars).
- Subtitle: `$"{_data.TrainerName(p.TrainerId)} · {p.Level} · {p.SessionsPerWeek}×/wk · {p.DurationWeeks} wk"`.
- Title: `p.Name`.

### 5.6 Dashboard wiring

Two additions, parallel to the trainer slice's Coach Spotlight rebuild:

1. **New "Featured Workout Plan" card** inserted directly **below the Coach Spotlight card** in `DashboardPage.xaml`:
   ```
   H2Section("Featured Workout Plan")
   Frame / KpiCard host
     ┌ Plan name              (H3)
     ├ Trainer name           (BodyMd, muted)
     ├ "Level · N×/wk · N wk" (BodyMd pill row)
     ├ Summary                (BodyMd, muted, wrap)
     └ PrimaryButton "BROWSE WORKOUT PLANS"
   ```
   Leaf labels are `x:Name`'d (`FeaturedPlanName`, `FeaturedPlanTrainer`, `FeaturedPlanMeta`, `FeaturedPlanSummary`) so the code-behind can set them imperatively — matches the imperative style of the rest of the app, no `BindingContext` rewrite.

2. **Code-behind** in `DashboardPage`:
   - `OnAppearing` reads `_data.TopPlan()`.
   - If non-null: populate the four labels from the plan, resolving trainer name via `_data.TrainerName(p.TrainerId)`.
   - If null: muted `"No plans configured."` fallback in `FeaturedPlanName`, hide the meta + summary + button (parallels the existing empty-trainers fallback shipped in commit `7a66f78`).
   - `Browse` button `Clicked` → `Shell.Current.GoToAsync("//Workouts")`.

`DashboardPage` already receives `DataStore` via DI from the trainer slice; no ctor signature change.

### 5.7 AppShell + DI

`AppShell.xaml` adds Workouts as a Shell route, **outside the `<TabBar>`** (mirrors Trainers):

```xml
<ShellContent Route="Workouts"
              ContentTemplate="{DataTemplate pages:WorkoutsPage}" />
```

`MauiProgram.cs` adds:

```csharp
builder.Services.AddTransient<Pages.WorkoutsPage>();
```

### 5.8 BottomTabBar

One additive change: extend `AppTab` with a `Workouts` value. **No new pill is rendered** — `ApplyActive` returns the active-fill colour only when the enum matches one of the five existing pills; any other value (Trainers, now Workouts) leaves all pills transparent.

```csharp
public enum AppTab { Dashboard, Members, Payments, Attendance, Reports, Trainers, Workouts }
```

No changes to `BottomTabBar.xaml`, no changes to the `ApplyActive` body, no new tap handler.

## 6. Data Flow

```
                  ┌───────────────────────────────┐
                  │ SQLite (gymers.db3)           │
                  │ + workout_plans table         │
                  └──────────────┬────────────────┘
                                 │ seeded once from SampleData.WorkoutPlans
                                 ▼
                  ┌───────────────────────────────┐
                  │ DataStore.WorkoutPlans        │
                  │ (ObservableCollection,         │
                  │  OrderRank ASC on load)       │
                  └─────┬──────────────────┬──────┘
                        │                  │
                        │                  │ TopPlan()
                        ▼                  ▼
              ┌──────────────────┐   ┌──────────────────────────────┐
              │ WorkoutsPage     │   │ DashboardPage                │
              │ list + search    │   │ Featured Plan card (bound)   │
              │                  │   │ "BROWSE PLANS" → //Workouts  │
              └──────────────────┘   └──────────────────────────────┘
                        ↑                  ↑
                        └──────────────────┘
                  DataStore.TrainerName(id) — FK lookup at render time
```

Plans are read-only at runtime in this slice — no mutations beyond first-run seed — so there's no write-through, no `MainThread.InvokeOnMainThreadAsync`, no `CollectionChanged`-triggered re-render needed beyond the initial pass.

## 7. Error Handling / Edge Cases

| Case | Handling |
|---|---|
| Empty plans table after a manual DB wipe | `TopPlan()` returns null; Dashboard card shows `"No plans configured."` and hides meta/summary/button (parallels empty-trainers fallback). |
| Plan references unknown `TrainerId` (e.g. trainer deleted) | `TrainerName()` returns `"—"`; row still renders. |
| Search yields zero matches | Muted `"No plans match \"…\"."` label, identical to Members/Trainers. |
| Existing demo DB lacks the workout_plans table | `CreateTable<WorkoutPlanRow>()` is idempotent; first launch after this slice creates the table and seeds it from `SampleData.WorkoutPlans`. |
| User installs the slice over a DB seeded before Trainers shipped | Defensive: bootstrap re-checks `IsTrainersEmpty()` before `IsWorkoutPlansEmpty()` so trainer rows always exist before any plan resolves a name. (Already the case in DataStore ctor order.) |

## 8. Testing & Verification

Manual verification, same shape as the trainer slice:

1. **Build clean**: `dotnet build` for `net10.0-maccatalyst` → 0 warnings, 0 errors. Repeat for `net10.0-ios`.
2. **Smoke-test on Mac Catalyst** (per the smoke-test memory — `dotnet build` doesn't catch static-cctor crashes):
   - Launch app, sign in as admin.
   - Dashboard loads, **Coach Spotlight** still shows Marcus Sterling, **Featured Workout Plan** card shows `Foundations of Strength` / `Marcus Sterling` / `Beginner · 3×/wk · 6 wk` / summary.
   - Tap **BROWSE WORKOUT PLANS** → WorkoutsPage opens, list renders 5 rows in OrderRank order; top row is Foundations of Strength.
   - Type `hiit` → list filters to a single row (`HIIT Conditioning Cycle`).
   - Type `zzz` → empty-state label.
   - Tap any BottomTabBar pill → navigates out of Workouts cleanly.
   - Force-quit and relaunch → plans persist (table seeded once, not re-seeded).
3. **Existing flows still work**: Members search, payment recording + PDF receipt, check-in, report PDF/CSV export, Trainers list + Dashboard Coach Spotlight — all unchanged.
4. **Screenshot capture**: a new `08-workouts.png` for the status doc (mirrors `07-trainers.png` shape).

No automated test harness in this iteration; broader test coverage remains on the Ongoing Tasks list.

## 9. Risks / Trade-offs

- **A "Workout Plan" without exercises feels thin.** Accepted explicitly — the slice is sized to land in a day, mirrors a working pattern, and lets a future "Plan Exercises" slice land without disturbing the schema (exercises become a child table with `WorkoutPlanId` FK).
- **Trainer name denormalized at render time, not joined in SQL.** Means an in-memory linear scan per row. Trivial at 5 plans / 5 trainers; if either grows past ~100 a dictionary index in `DataStore` is a 5-line change.
- **Featured = first row by `OrderRank`, not algorithmic.** Easy to swap later (e.g. most-assigned, highest-rated) once mutations and signals exist.
- **Imperative code-behind, no MVVM.** Consistent with the rest of the app; same trade-off captured in the trainer slice.

## 10. Out of Scope (future slices)

- **Plan Exercises**: child table with sets / reps / weight / rest, tappable plan rows opening a detail page.
- **Member ↔ Plan assignment**: which member is on which plan, plus completion tracking.
- **Plan CRUD**: create / edit / archive plans from the app itself.
- **Equipment module**: the last deferred README item.
- **Role-based UI** (admin vs staff actually doing different things).
- Adding Workouts as a 6th (or 7th) BottomTabBar pill — would require a layout pass on phone width.
- Test harness + broader visual polish.
