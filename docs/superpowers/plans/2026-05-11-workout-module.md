# Workout Plan Module Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a SQLite-backed read-only Workout Plan roster + name search + Dashboard "Featured Plan" card with a route to a new `WorkoutsPage`, mirroring the trainer module slice.

**Architecture:** Plan rows live in a new `workout_plans` SQLite table seeded once from `SampleData.WorkoutPlans`. `DataStore` exposes the ordered collection plus `SearchWorkoutPlans`, `TopPlan`, and a `TrainerName(id)` helper for FK lookup. `WorkoutsPage` is a near-clone of `TrainersPage`; `DashboardPage` gains a new "Featured Workout Plan" card under Coach Spotlight whose `BROWSE WORKOUT PLANS` button navigates to `//Workouts`. `BottomTabBar` gets an additive `AppTab.Workouts` enum value with no new pill.

**Tech Stack:** .NET MAUI 10 (net10.0-ios, net10.0-maccatalyst), sqlite-net-pcl, imperative code-behind (no MVVM).

**Predecessor spec:** `docs/superpowers/specs/2026-05-11-workout-module-design.md`.

---

## File Structure

**Create:**
- `Gymers/Models/WorkoutPlan.cs` — record type for the plan aggregate.
- `Gymers/Data/Rows/WorkoutPlanRow.cs` — SQLite row mirror.
- `Gymers/Pages/WorkoutsPage.xaml` + `.xaml.cs` — list + search page (mirrors TrainersPage).

**Modify:**
- `Gymers/Data/GymersDb.cs` — register the new table; add seed/query/convert helpers.
- `Gymers/Data/SampleData.cs` — add the 5 seeded `WorkoutPlan` rows.
- `Gymers/Data/DataStore.cs` — expose `WorkoutPlans`, `SearchWorkoutPlans`, `TopPlan`, `TrainerName`.
- `Gymers/Controls/BottomTabBar.xaml.cs` — extend `AppTab` enum with `Workouts`.
- `Gymers/AppShell.xaml` — register `Workouts` route.
- `Gymers/MauiProgram.cs` — register `WorkoutsPage` as transient.
- `Gymers/Pages/DashboardPage.xaml` + `.xaml.cs` — add Featured Plan card + wiring.

**Status doc (after smoke-test):**
- `docs/status/screenshots/08-workouts.png` — new screenshot of WorkoutsPage.
- `docs/status/build_status_docx.py` — add "Workout Plans" row to completed table.
- `docs/status/gymers-mobile-app-status-update.html` — mirror that change.

---

## Task 1: WorkoutPlan model

**Files:**
- Create: `Gymers/Models/WorkoutPlan.cs`

- [ ] **Step 1: Create the record**

```csharp
namespace Gymers.Models;

public record WorkoutPlan(
    string Id,
    string Name,
    string TrainerId,
    string Level,
    int    SessionsPerWeek,
    int    DurationWeeks,
    string Summary,
    int    OrderRank);
```

- [ ] **Step 2: Verify build still compiles**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo -clp:ErrorsOnly`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Gymers/Models/WorkoutPlan.cs
git commit -m "feat(workouts): add WorkoutPlan record"
```

---

## Task 2: WorkoutPlanRow + DB table

**Files:**
- Create: `Gymers/Data/Rows/WorkoutPlanRow.cs`
- Modify: `Gymers/Data/GymersDb.cs` (register table + add seed/query/convert methods)

- [ ] **Step 1: Create the row class**

```csharp
using SQLite;

namespace Gymers.Data.Rows;

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

- [ ] **Step 2: Register the table in `GymersDb` constructor**

In `Gymers/Data/GymersDb.cs`, after the `_sync.CreateTable<TrainerRow>();` line, add:

```csharp
_sync.CreateTable<WorkoutPlanRow>();
```

- [ ] **Step 3: Add seed + query + convert helpers in `GymersDb`**

Append these methods to `GymersDb` (after the existing `GetTrainersByRatingDesc` method):

```csharp
public bool IsWorkoutPlansEmpty() =>
    _sync.Table<WorkoutPlanRow>().Count() == 0;

public void SeedWorkoutPlans(IEnumerable<WorkoutPlan> plans)
{
    foreach (var p in plans) _sync.Insert(ToRow(p));
}

public IEnumerable<WorkoutPlan> GetWorkoutPlansOrdered() =>
    _sync.Table<WorkoutPlanRow>()
         .OrderBy(r => r.OrderRank)
         .ToList()
         .Select(ToRecord);
```

Append these private converters next to the existing `ToRow`/`ToRecord` pairs:

```csharp
static WorkoutPlanRow ToRow(WorkoutPlan p) => new()
{
    Id              = p.Id,
    Name            = p.Name,
    TrainerId       = p.TrainerId,
    Level           = p.Level,
    SessionsPerWeek = p.SessionsPerWeek,
    DurationWeeks   = p.DurationWeeks,
    Summary         = p.Summary,
    OrderRank       = p.OrderRank
};

static WorkoutPlan ToRecord(WorkoutPlanRow r) => new(
    r.Id, r.Name, r.TrainerId, r.Level,
    r.SessionsPerWeek, r.DurationWeeks, r.Summary, r.OrderRank);
```

- [ ] **Step 4: Build clean**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo -clp:ErrorsOnly`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Gymers/Data/Rows/WorkoutPlanRow.cs Gymers/Data/GymersDb.cs
git commit -m "feat(workouts): add WorkoutPlanRow + GymersDb extensions"
```

---

## Task 3: Seed sample data

**Files:**
- Modify: `Gymers/Data/SampleData.cs` (append `WorkoutPlans` list)

- [ ] **Step 1: Append the seed list to `SampleData`**

In `Gymers/Data/SampleData.cs`, after the existing `Trainers` array (line 53) and before `GetMember`, add:

```csharp
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

- [ ] **Step 2: Build clean**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo -clp:ErrorsOnly`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Gymers/Data/SampleData.cs
git commit -m "feat(workouts): seed five sample workout plans"
```

---

## Task 4: DataStore wiring

**Files:**
- Modify: `Gymers/Data/DataStore.cs`

- [ ] **Step 1: Add the `WorkoutPlans` property**

In `Gymers/Data/DataStore.cs`, after the `Trainers` property declaration (line 14), add:

```csharp
public ObservableCollection<WorkoutPlan> WorkoutPlans { get; }
```

- [ ] **Step 2: Seed + load in the constructor**

After the existing trainers seed/load block in the constructor (lines 28–31 + line 36), add:

```csharp
if (_db.IsWorkoutPlansEmpty())
{
    _db.SeedWorkoutPlans(SampleData.WorkoutPlans);
}
```
…and after the line `Trainers = new ObservableCollection<Trainer>(_db.GetTrainersByRatingDesc());`:
```csharp
WorkoutPlans = new ObservableCollection<WorkoutPlan>(_db.GetWorkoutPlansOrdered());
```

- [ ] **Step 3: Add `SearchWorkoutPlans`, `TopPlan`, and `TrainerName` helpers**

Append these methods to `DataStore` (after the existing `TopTrainer()` method around line 55):

```csharp
public IEnumerable<WorkoutPlan> SearchWorkoutPlans(string? query) =>
    string.IsNullOrWhiteSpace(query)
        ? WorkoutPlans
        : WorkoutPlans.Where(p =>
            p.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

public WorkoutPlan? TopPlan() => WorkoutPlans.FirstOrDefault();

public string TrainerName(string trainerId) =>
    Trainers.FirstOrDefault(t => t.Id == trainerId)?.Name ?? "—";
```

- [ ] **Step 4: Build clean**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo -clp:ErrorsOnly`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add Gymers/Data/DataStore.cs
git commit -m "feat(workouts): expose plans + search + top in DataStore"
```

---

## Task 5: Extend `AppTab` enum

**Files:**
- Modify: `Gymers/Controls/BottomTabBar.xaml.cs:3`

- [ ] **Step 1: Add `Workouts` to the enum**

Change line 3 of `Gymers/Controls/BottomTabBar.xaml.cs` from:

```csharp
public enum AppTab { Dashboard, Members, Payments, Attendance, Reports, Trainers }
```

to:

```csharp
public enum AppTab { Dashboard, Members, Payments, Attendance, Reports, Trainers, Workouts }
```

No other lines in the file change — `ApplyActive` already returns `Colors.Transparent` for any value not explicitly matched, so adding `Workouts` renders the bar with no pill highlighted.

- [ ] **Step 2: Build clean**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo -clp:ErrorsOnly`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Gymers/Controls/BottomTabBar.xaml.cs
git commit -m "feat(controls): add AppTab.Workouts (no new pill rendered)"
```

---

## Task 6: WorkoutsPage XAML + code-behind

**Files:**
- Create: `Gymers/Pages/WorkoutsPage.xaml`
- Create: `Gymers/Pages/WorkoutsPage.xaml.cs`

- [ ] **Step 1: Create the XAML file**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:c="clr-namespace:Gymers.Controls"
             x:Class="Gymers.Pages.WorkoutsPage"
             BackgroundColor="{StaticResource BgApp}"
             Shell.NavBarIsVisible="False">

    <Grid RowDefinitions="Auto,*,Auto">

        <c:TopAppBar Grid.Row="0" Title="Workout Plans"
                     TrailingIconGlyph="{x:Static c:Icons.Users}" />

        <ScrollView Grid.Row="1" Padding="24,16">
            <VerticalStackLayout Spacing="16">
                <c:SearchField x:Name="Search" Placeholder="Search by name…" />

                <c:KpiCard Variant="Light"
                           Label="Active Plans" Value="5"
                           Caption="curated"
                           TrailingIconGlyph="{x:Static c:Icons.Users}" />

                <Label Style="{StaticResource H2Section}" Text="All Plans" />

                <VerticalStackLayout x:Name="PlanList" Spacing="12" />
            </VerticalStackLayout>
        </ScrollView>

        <c:BottomTabBar Grid.Row="2" ActiveTab="Workouts" />
    </Grid>
</ContentPage>
```

(`Icons.Users` is reused as the trailing glyph because no dedicated dumbbell glyph is currently in `Icons.cs`; adding a new glyph would be a 2-line follow-up but isn't needed for this slice.)

- [ ] **Step 2: Create the code-behind**

```csharp
using System.ComponentModel;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;

namespace Gymers.Pages;

public partial class WorkoutsPage : ContentPage
{
    readonly DataStore _data;

    public WorkoutsPage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        Search.PropertyChanged += OnSearchChanged;
        _data.WorkoutPlans.CollectionChanged += (_, _) => Render(Search.Text ?? "");
        Render("");
    }

    void OnSearchChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchField.Text))
            Render(Search.Text ?? "");
    }

    void Render(string query)
    {
        PlanList.Children.Clear();
        var matches = _data.SearchWorkoutPlans(query).ToList();

        if (matches.Count == 0)
        {
            PlanList.Children.Add(new Label
            {
                Text = $"No plans match \"{query.Trim()}\".",
                Style = (Style)Application.Current!.Resources["BodyMd"],
                TextColor = (Color)Application.Current.Resources["TextMuted"],
                HorizontalTextAlignment = TextAlignment.Center
            });
            return;
        }

        foreach (var p in matches)
            PlanList.Children.Add(BuildRow(p));
    }

    View BuildRow(WorkoutPlan p) => new ListRow
    {
        LeadingContent = AvatarFactory.MakeInitial(p.Name),
        Title          = p.Name,
        Subtitle       = $"{_data.TrainerName(p.TrainerId)} · {p.Level} · {p.SessionsPerWeek}×/wk · {p.DurationWeeks} wk"
    };
}
```

- [ ] **Step 3: Build clean**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo -clp:ErrorsOnly`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Gymers/Pages/WorkoutsPage.xaml Gymers/Pages/WorkoutsPage.xaml.cs
git commit -m "feat(workouts): add WorkoutsPage with name search"
```

---

## Task 7: Register Workouts route + DI

**Files:**
- Modify: `Gymers/AppShell.xaml` (add `ShellContent`)
- Modify: `Gymers/MauiProgram.cs` (add transient registration)

- [ ] **Step 1: Add the Shell route**

In `Gymers/AppShell.xaml`, after the existing Trainers `ShellContent` (lines 24–25), add a parallel block so the file ends with:

```xml
    <ShellContent Route="Trainers"
                  ContentTemplate="{DataTemplate pages:TrainersPage}" />

    <ShellContent Route="Workouts"
                  ContentTemplate="{DataTemplate pages:WorkoutsPage}" />

</Shell>
```

- [ ] **Step 2: Register the page in DI**

In `Gymers/MauiProgram.cs`, after the existing `builder.Services.AddTransient<Pages.TrainersPage>();` line (line 36), add:

```csharp
builder.Services.AddTransient<Pages.WorkoutsPage>();
```

- [ ] **Step 3: Build clean**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo -clp:ErrorsOnly`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Gymers/AppShell.xaml Gymers/MauiProgram.cs
git commit -m "feat(workouts): register WorkoutsPage route + DI"
```

---

## Task 8: Dashboard Featured Plan card

**Files:**
- Modify: `Gymers/Pages/DashboardPage.xaml` (insert card under Coach Spotlight)
- Modify: `Gymers/Pages/DashboardPage.xaml.cs` (bind labels + wire button)

- [ ] **Step 1: Insert the Featured Plan card XAML**

In `Gymers/Pages/DashboardPage.xaml`, between the closing `</Border>` of the Coach Spotlight card (line 144) and the `<!-- Today's Classes -->` comment (line 146), insert:

```xml
                <!-- Featured Workout Plan -->
                <Border Style="{StaticResource Card}">
                    <VerticalStackLayout Spacing="16">
                        <Label Style="{StaticResource H2Section}" Text="Featured Workout Plan" />

                        <VerticalStackLayout Spacing="4">
                            <Label x:Name="FeaturedPlanName"
                                   Style="{StaticResource H3Card}"
                                   Text="—" />
                            <Label x:Name="FeaturedPlanTrainer"
                                   Style="{StaticResource BodyMd}"
                                   Text="" />
                        </VerticalStackLayout>

                        <Label x:Name="FeaturedPlanMeta"
                               FontFamily="{StaticResource FontInterSemiBold}"
                               FontSize="14"
                               TextColor="{StaticResource NavyDeep}"
                               Text="" />

                        <Label x:Name="FeaturedPlanSummary"
                               Style="{StaticResource BodyMd}"
                               TextColor="{StaticResource TextMuted}"
                               Text="" />

                        <c:PrimaryButton x:Name="BrowsePlansButton"
                                         Text="BROWSE WORKOUT PLANS" />
                    </VerticalStackLayout>
                </Border>
```

- [ ] **Step 2: Wire the code-behind**

In `Gymers/Pages/DashboardPage.xaml.cs`, inside the existing constructor (`public DashboardPage(DataStore data)`), after the line `ProfileButton.Clicked += async (_, _) => await Shell.Current.GoToAsync("//Trainers");` (line 17–18), add:

```csharp
        ApplyFeaturedPlan();
        BrowsePlansButton.Clicked += async (_, _) =>
            await Shell.Current.GoToAsync("//Workouts");
```

Then append a new method to the class (after `ApplyCoachSpotlight`):

```csharp
    void ApplyFeaturedPlan()
    {
        var top = _data.TopPlan();
        if (top is null)
        {
            FeaturedPlanName.Text     = "No plans configured.";
            FeaturedPlanTrainer.IsVisible = false;
            FeaturedPlanMeta.IsVisible    = false;
            FeaturedPlanSummary.IsVisible = false;
            BrowsePlansButton.IsVisible   = false;
            return;
        }

        FeaturedPlanName.Text    = top.Name;
        FeaturedPlanTrainer.Text = _data.TrainerName(top.TrainerId);
        FeaturedPlanMeta.Text    = $"{top.Level}  ·  {top.SessionsPerWeek}×/wk  ·  {top.DurationWeeks} wk";
        FeaturedPlanSummary.Text = top.Summary;
    }
```

- [ ] **Step 3: Build clean**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo -clp:ErrorsOnly`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Gymers/Pages/DashboardPage.xaml Gymers/Pages/DashboardPage.xaml.cs
git commit -m "feat(dashboard): wire Featured Plan card + Workouts nav"
```

---

## Task 9: Mac Catalyst smoke test

**Files:**
- (none — runtime verification only)

- [ ] **Step 1: Full build, both targets**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo`
Expected: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios -nologo`
Expected: same — 0 warnings, 0 errors. (If iOS sim isn't available, this step still validates the compile path.)

- [ ] **Step 2: Launch on Mac Catalyst**

Per the smoke-test memory: green `dotnet build` does NOT guarantee runtime liveness on MAUI (static cctor crashes are invisible to the compiler). The app must be launched and visually confirmed.

Run:
```
dotnet build Gymers/Gymers.csproj -t:Run -f net10.0-maccatalyst
```
…or the standard local-launch workflow used in earlier slices.

- [ ] **Step 3: Verify in the running app**

Tick each:
- [ ] Sign in as `admin` / `admin123`.
- [ ] Dashboard loads. Coach Spotlight still shows Marcus Sterling.
- [ ] **Featured Workout Plan** card shows `Foundations of Strength`, trainer `Marcus Sterling`, meta `Beginner  ·  3×/wk  ·  6 wk`, summary populated.
- [ ] Tap **BROWSE WORKOUT PLANS** → WorkoutsPage opens.
- [ ] List renders 5 rows; top row is `Foundations of Strength`; subtitle `Marcus Sterling · Beginner · 3×/wk · 6 wk`.
- [ ] Type `hiit` into search → list filters to a single row (`HIIT Conditioning Cycle`).
- [ ] Type `zzz` → muted `No plans match "zzz".` label.
- [ ] Tap any BottomTabBar pill (e.g. Members) → navigates out of Workouts.
- [ ] Trainers page still loads via `VIEW PERFORMANCE PROFILE`.
- [ ] Members, Payments (tap a row → PDF receipt), Attendance, Reports (export PDF) all still work.
- [ ] Force-quit and relaunch → Featured Plan + Workouts list persist (no re-seed).

- [ ] **Step 4: If any verification fails**

Diagnose the failure and patch in a follow-up commit before moving to Task 10. Do NOT proceed past this gate with a runtime regression.

---

## Task 10: Screenshot + status doc

**Files:**
- Create: `docs/status/screenshots/08-workouts.png`
- Modify: `docs/status/build_status_docx.py`
- Modify: `docs/status/gymers-mobile-app-status-update.html`

- [ ] **Step 1: Capture the screenshot**

With WorkoutsPage open in the running app, capture a window screenshot of the page (mirror the `07-trainers.png` framing — full window, no chrome). Save as `docs/status/screenshots/08-workouts.png`.

- [ ] **Step 2: Add the completed-features row in `build_status_docx.py`**

Open `docs/status/build_status_docx.py`. After the existing `["Trainer roster", "Completed", "…"]` row (around line 111–113), append a new row inside the same `completed_rows` array:

```python
        ["Workout plans",
         "Completed",
         "A SQLite-backed Workout Plans screen lists curated plans with a live name-search filter; each row shows the assigned trainer, level, weekly cadence, and total duration. The Dashboard's new Featured Workout Plan card highlights the top-ranked plan and its BROWSE WORKOUT PLANS button navigates to the Workouts screen via //Workouts."],
```

Also update the opening summary paragraph (around line 120) so the screen count is current. Change `Six core screens (Login, Dashboard, Members, Payments, Attendance, Reports)` if needed to keep parity with what's shipped, and update the closing sentence about deferrals to drop "workout" (Equipment is now the only remaining deferred README module).

- [ ] **Step 3: Mirror the change in the HTML status doc**

In `docs/status/gymers-mobile-app-status-update.html`, find the corresponding "Trainer roster" row in the completed-features table and add a parallel row for Workout Plans right after it. Also embed the new screenshot: add an `<img src="screenshots/08-workouts.png" alt="Workouts screen">` block following the Trainers screenshot block.

- [ ] **Step 4: Regenerate the .docx locally (sanity check)**

Run: `python3 docs/status/build_status_docx.py`
Expected: writes `docs/status/Gymers-Mobile-App-Status-Update.docx` (gitignored, do not commit).

- [ ] **Step 5: Commit the doc + screenshot changes**

```bash
git add docs/status/screenshots/08-workouts.png docs/status/build_status_docx.py docs/status/gymers-mobile-app-status-update.html
git commit -m "docs(status): mark Workout Plans as completed + add screenshot"
```

---

## Self-Review (already performed against the spec)

| Spec section | Implemented by |
|---|---|
| §5.1 Model              | Task 1 |
| §5.2 Row + DB           | Task 2 |
| §5.3 DataStore wiring   | Task 4 |
| §5.4 Sample data        | Task 3 |
| §5.5 WorkoutsPage       | Task 6 |
| §5.6 Dashboard hook     | Task 8 |
| §5.7 AppShell + DI      | Task 7 |
| §5.8 BottomTabBar enum  | Task 5 |
| §6  Data flow           | Tasks 2–4 (composes naturally) |
| §7  Edge cases          | Empty-table fallback in Task 8 step 2; unknown trainer FK handled by `TrainerName` in Task 4; empty search by `Render` in Task 6 |
| §8  Verification        | Task 9 |
| Status doc + screenshot | Task 10 |

No spec section is left unrealized. No placeholders. Type / method names verified consistent across tasks (`WorkoutPlan`, `WorkoutPlans`, `WorkoutPlanRow`, `IsWorkoutPlansEmpty`, `SeedWorkoutPlans`, `GetWorkoutPlansOrdered`, `SearchWorkoutPlans`, `TopPlan`, `TrainerName`, `AppTab.Workouts`, `WorkoutsPage`, `//Workouts`).
