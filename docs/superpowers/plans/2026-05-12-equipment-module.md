# Equipment Module Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a SQLite-backed read-only Equipment roster + name search + Dashboard "Equipment Status" card with a route to a new `EquipmentPage`, mirroring the workout-plan module slice and closing the README's last scope gap.

**Architecture:** Equipment rows live in a new `equipment` SQLite table seeded once from `SampleData.Equipment`. `DataStore` exposes the ordered collection plus `SearchEquipment`, `OperationalEquipmentCount`, and `MaintenanceEquipmentCount` helpers. `EquipmentPage` is a near-clone of `WorkoutsPage`; `DashboardPage` gains a new "Equipment Status" card under Featured Workout Plan whose `VIEW EQUIPMENT` button navigates to `//Equipment`. `BottomTabBar` gets an additive `AppTab.Equipment` enum value with no new pill.

**Tech Stack:** .NET MAUI 10 (net10.0-ios, net10.0-maccatalyst), sqlite-net-pcl, imperative code-behind (no MVVM).

**Predecessor spec:** `docs/superpowers/specs/2026-05-12-equipment-module-design.md`.

---

## File Structure

**Create:**
- `Gymers/Models/Equipment.cs` — record type for the equipment aggregate.
- `Gymers/Data/Rows/EquipmentRow.cs` — SQLite row mirror.
- `Gymers/Pages/EquipmentPage.xaml` + `.xaml.cs` — list + search page (mirrors WorkoutsPage).

**Modify:**
- `Gymers/Data/GymersDb.cs` — register the new table; add seed/query/convert helpers.
- `Gymers/Data/SampleData.cs` — add the 6 seeded `Equipment` rows.
- `Gymers/Data/DataStore.cs` — expose `Equipment`, `SearchEquipment`, `OperationalEquipmentCount`, `MaintenanceEquipmentCount`.
- `Gymers/Controls/BottomTabBar.xaml.cs` — extend `AppTab` enum with `Equipment`.
- `Gymers/AppShell.xaml` — register `Equipment` route.
- `Gymers/MauiProgram.cs` — register `EquipmentPage` as transient.
- `Gymers/Pages/DashboardPage.xaml` + `.xaml.cs` — add Equipment Status card + wiring.

**Status doc (after smoke-test):**
- `docs/status/screenshots/09-equipment.png` — new screenshot of EquipmentPage.
- `docs/status/build_status_docx.py` — add "Equipment management" row to completed table; drop the Equipment bullet from Ongoing Tasks.
- `docs/status/gymers-mobile-app-status-update.html` — mirror that change + embed screenshot 9.

---

## Task 1: Equipment model

**Files:**
- Create: `Gymers/Models/Equipment.cs`

- [ ] **Step 1: Create the record**

```csharp
namespace Gymers.Models;

public record Equipment(
    string Id,
    string Name,
    string Category,
    string Status,
    string Location,
    int    OrderRank);
```

- [ ] **Step 2: Verify build still compiles**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo -clp:ErrorsOnly`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Gymers/Models/Equipment.cs
git commit -m "feat(equipment): add Equipment record"
```

---

## Task 2: EquipmentRow + DB table

**Files:**
- Create: `Gymers/Data/Rows/EquipmentRow.cs`
- Modify: `Gymers/Data/GymersDb.cs` (register table + add seed/query/convert methods)

- [ ] **Step 1: Create the row class**

```csharp
using SQLite;

namespace Gymers.Data.Rows;

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

- [ ] **Step 2: Register the table in `GymersDb` constructor**

In `Gymers/Data/GymersDb.cs`, after the existing `_sync.CreateTable<WorkoutPlanRow>();` line (line 22), add:

```csharp
        _sync.CreateTable<EquipmentRow>();
```

- [ ] **Step 3: Add seed + query helpers in `GymersDb`**

Append these methods to `GymersDb` after the existing `GetWorkoutPlansOrdered()` method (after line 87, before `InsertPaymentAsync`):

```csharp
    public bool IsEquipmentEmpty() =>
        _sync.Table<EquipmentRow>().Count() == 0;

    public void SeedEquipment(IEnumerable<Equipment> items)
    {
        foreach (var e in items) _sync.Insert(ToRow(e));
    }

    public IEnumerable<Equipment> GetEquipmentOrdered() =>
        _sync.Table<EquipmentRow>()
             .OrderBy(r => r.OrderRank)
             .ToList()
             .Select(ToRecord);
```

- [ ] **Step 4: Add converter helpers**

Append at the end of `GymersDb` (after the existing `WorkoutPlan` converter pair, before the closing brace):

```csharp
    static EquipmentRow ToRow(Equipment e) => new()
    {
        Id        = e.Id,
        Name      = e.Name,
        Category  = e.Category,
        Status    = e.Status,
        Location  = e.Location,
        OrderRank = e.OrderRank
    };

    static Equipment ToRecord(EquipmentRow r) => new(
        r.Id, r.Name, r.Category, r.Status, r.Location, r.OrderRank);
```

- [ ] **Step 5: Build clean**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo -clp:ErrorsOnly`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Gymers/Data/Rows/EquipmentRow.cs Gymers/Data/GymersDb.cs
git commit -m "feat(equipment): add EquipmentRow + GymersDb extensions"
```

---

## Task 3: Seed sample data

**Files:**
- Modify: `Gymers/Data/SampleData.cs` (append `Equipment` list)

- [ ] **Step 1: Append the seed list to `SampleData`**

In `Gymers/Data/SampleData.cs`, after the existing `WorkoutPlans` array (ending at line 67) and before `GetMember`, add:

```csharp
    public static readonly IReadOnlyList<Equipment> Equipment = new[]
    {
        new Equipment("e1", "Treadmill TR-01",     "Cardio",   "Operational", "Cardio Zone",  1),
        new Equipment("e2", "Treadmill TR-02",     "Cardio",   "Operational", "Cardio Zone",  2),
        new Equipment("e3", "Power Rack PR-A",     "Strength", "Operational", "Weight Room",  3),
        new Equipment("e4", "Smith Machine SM-01", "Strength", "Maintenance", "Weight Room",  4),
        new Equipment("e5", "Spin Bike SB-03",     "Cardio",   "Operational", "Cardio Zone",  5),
        new Equipment("e6", "Yoga Mat Set YM-01",  "Studio",   "Operational", "Yoga Studio",  6),
    };
```

- [ ] **Step 2: Build clean**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo -clp:ErrorsOnly`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Gymers/Data/SampleData.cs
git commit -m "feat(equipment): seed six sample equipment items"
```

---

## Task 4: DataStore wiring

**Files:**
- Modify: `Gymers/Data/DataStore.cs`

- [ ] **Step 1: Add the `Equipment` property**

In `Gymers/Data/DataStore.cs`, after the `WorkoutPlans` property declaration (line 15), add:

```csharp
    public ObservableCollection<Equipment>   Equipment    { get; }
```

- [ ] **Step 2: Seed + load in the constructor**

After the existing workout-plans seed block ending at line 37, add:

```csharp
        if (_db.IsEquipmentEmpty())
        {
            _db.SeedEquipment(SampleData.Equipment);
        }
```

Then after the line `WorkoutPlans = new ObservableCollection<WorkoutPlan>(_db.GetWorkoutPlansOrdered());` (line 43), add:

```csharp
        Equipment    = new ObservableCollection<Equipment>(_db.GetEquipmentOrdered());
```

- [ ] **Step 3: Add `SearchEquipment` + counters**

Append these methods to `DataStore` after the existing `TrainerName(...)` method (after line 73):

```csharp
    public IEnumerable<Equipment> SearchEquipment(string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? Equipment
            : Equipment.Where(e =>
                e.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

    public int OperationalEquipmentCount() =>
        Equipment.Count(e => string.Equals(e.Status, "Operational", StringComparison.OrdinalIgnoreCase));

    public int MaintenanceEquipmentCount() =>
        Equipment.Count - OperationalEquipmentCount();
```

- [ ] **Step 4: Build clean**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo -clp:ErrorsOnly`
Expected: Build succeeded.

Note: the property name `Equipment` shadows the type name `Gymers.Models.Equipment`. Inside `DataStore`, the property usage (`Equipment.Count`, `Equipment.Where(...)`) is unambiguous because the type is only referenced via the fully-qualified `IEnumerable<Equipment>` and `ObservableCollection<Equipment>` generic args — C# resolves the type and the property by context.

- [ ] **Step 5: Commit**

```bash
git add Gymers/Data/DataStore.cs
git commit -m "feat(equipment): expose equipment + search + counts in DataStore"
```

---

## Task 5: Extend `AppTab` enum

**Files:**
- Modify: `Gymers/Controls/BottomTabBar.xaml.cs:3`

- [ ] **Step 1: Add `Equipment` to the enum**

Change line 3 of `Gymers/Controls/BottomTabBar.xaml.cs` from:

```csharp
public enum AppTab { Dashboard, Members, Payments, Attendance, Reports, Trainers, Workouts }
```

to:

```csharp
public enum AppTab { Dashboard, Members, Payments, Attendance, Reports, Trainers, Workouts, Equipment }
```

No other lines in the file change — `ApplyActive` already returns `Colors.Transparent` for any value not explicitly matched, so adding `Equipment` renders the bar with no pill highlighted.

- [ ] **Step 2: Build clean**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo -clp:ErrorsOnly`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Gymers/Controls/BottomTabBar.xaml.cs
git commit -m "feat(controls): add AppTab.Equipment (no new pill rendered)"
```

---

## Task 6: EquipmentPage XAML + code-behind

**Files:**
- Create: `Gymers/Pages/EquipmentPage.xaml`
- Create: `Gymers/Pages/EquipmentPage.xaml.cs`

- [ ] **Step 1: Create the XAML file**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:c="clr-namespace:Gymers.Controls"
             x:Class="Gymers.Pages.EquipmentPage"
             BackgroundColor="{StaticResource BgApp}"
             Shell.NavBarIsVisible="False">

    <Grid RowDefinitions="Auto,*,Auto">

        <c:TopAppBar Grid.Row="0" Title="Equipment"
                     TrailingIconGlyph="{x:Static c:Icons.Users}" />

        <ScrollView Grid.Row="1" Padding="24,16">
            <VerticalStackLayout Spacing="16">
                <c:SearchField x:Name="Search" Placeholder="Search by name…" />

                <c:KpiCard x:Name="StatusKpi"
                           Variant="Light"
                           Label="Operational" Value="0"
                           Caption="of 0 items"
                           TrailingIconGlyph="{x:Static c:Icons.Users}" />

                <Label Style="{StaticResource H2Section}" Text="All Equipment" />

                <VerticalStackLayout x:Name="EquipmentList" Spacing="12" />
            </VerticalStackLayout>
        </ScrollView>

        <c:BottomTabBar Grid.Row="2" ActiveTab="Equipment" />
    </Grid>
</ContentPage>
```

(`Icons.Users` is reused for the trailing glyph; same trade-off as Workouts — adding a dedicated dumbbell/wrench glyph is a 2-line follow-up not needed for this slice.)

- [ ] **Step 2: Create the code-behind**

```csharp
using System.ComponentModel;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;

namespace Gymers.Pages;

public partial class EquipmentPage : ContentPage
{
    readonly DataStore _data;

    public EquipmentPage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        Search.PropertyChanged += OnSearchChanged;
        _data.Equipment.CollectionChanged += (_, _) => Render(Search.Text ?? "");
        ApplyStatusKpi();
        Render("");
    }

    void OnSearchChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchField.Text))
            Render(Search.Text ?? "");
    }

    void ApplyStatusKpi()
    {
        int operational = _data.OperationalEquipmentCount();
        int total       = _data.Equipment.Count;
        StatusKpi.Value   = operational.ToString();
        StatusKpi.Caption = $"of {total} item{(total == 1 ? "" : "s")}";
    }

    void Render(string query)
    {
        EquipmentList.Children.Clear();
        var matches = _data.SearchEquipment(query).ToList();

        if (matches.Count == 0)
        {
            EquipmentList.Children.Add(new Label
            {
                Text = $"No equipment matches \"{query.Trim()}\".",
                Style = (Style)Application.Current!.Resources["BodyMd"],
                TextColor = (Color)Application.Current.Resources["TextMuted"],
                HorizontalTextAlignment = TextAlignment.Center
            });
            return;
        }

        foreach (var e in matches)
            EquipmentList.Children.Add(BuildRow(e));
    }

    View BuildRow(Equipment e) => new ListRow
    {
        LeadingContent = AvatarFactory.MakeInitial(e.Name),
        Title          = e.Name,
        Subtitle       = $"{e.Category} · {e.Status} · {e.Location}"
    };
}
```

- [ ] **Step 3: Build clean**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo -clp:ErrorsOnly`
Expected: Build succeeded.

If the build complains that `StatusKpi.Value` / `StatusKpi.Caption` are not settable from code, verify they are exposed as bindable properties on `KpiCard`. They are (used as XAML attributes everywhere else); set via property assignment is fine.

- [ ] **Step 4: Commit**

```bash
git add Gymers/Pages/EquipmentPage.xaml Gymers/Pages/EquipmentPage.xaml.cs
git commit -m "feat(equipment): add EquipmentPage with name search + status KPI"
```

---

## Task 7: Register Equipment route + DI

**Files:**
- Modify: `Gymers/AppShell.xaml` (add `ShellContent`)
- Modify: `Gymers/MauiProgram.cs` (add transient registration)

- [ ] **Step 1: Add the Shell route**

In `Gymers/AppShell.xaml`, after the existing Workouts `ShellContent` block (lines 27–28), add a parallel block so the file ends with:

```xml
    <ShellContent Route="Workouts"
                  ContentTemplate="{DataTemplate pages:WorkoutsPage}" />

    <ShellContent Route="Equipment"
                  ContentTemplate="{DataTemplate pages:EquipmentPage}" />

</Shell>
```

- [ ] **Step 2: Register the page in DI**

In `Gymers/MauiProgram.cs`, after the existing `builder.Services.AddTransient<Pages.WorkoutsPage>();` line (line 37), add:

```csharp
		builder.Services.AddTransient<Pages.EquipmentPage>();
```

(Use the existing tab indentation style — `MauiProgram.cs` uses tabs.)

- [ ] **Step 3: Build clean**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo -clp:ErrorsOnly`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Gymers/AppShell.xaml Gymers/MauiProgram.cs
git commit -m "feat(equipment): register EquipmentPage route + DI"
```

---

## Task 8: Dashboard Equipment Status card

**Files:**
- Modify: `Gymers/Pages/DashboardPage.xaml` (insert card under Featured Workout Plan)
- Modify: `Gymers/Pages/DashboardPage.xaml.cs` (bind labels + wire button)

- [ ] **Step 1: Insert the Equipment Status card XAML**

In `Gymers/Pages/DashboardPage.xaml`, between the closing `</Border>` of the Featured Workout Plan card (line 174) and the `<!-- Today's Classes -->` comment (line 176), insert:

```xml
                <!-- Equipment Status -->
                <Border Style="{StaticResource Card}">
                    <VerticalStackLayout Spacing="16">
                        <Label Style="{StaticResource H2Section}" Text="Equipment Status" />

                        <Grid ColumnDefinitions="*,Auto" VerticalOptions="Center">
                            <VerticalStackLayout Grid.Column="0" Spacing="4">
                                <Label x:Name="EquipmentHeadline"
                                       Style="{StaticResource H3Card}"
                                       Text="—" />
                                <Label Style="{StaticResource BodyMd}"
                                       TextColor="{StaticResource TextMuted}"
                                       Text="Operational" />
                            </VerticalStackLayout>
                            <Label x:Name="EquipmentTotalLabel"
                                   Grid.Column="1"
                                   FontFamily="{StaticResource FontInterSemiBold}"
                                   FontSize="14"
                                   TextColor="{StaticResource NavyDeep}"
                                   VerticalTextAlignment="Center"
                                   Text="" />
                        </Grid>

                        <Label x:Name="EquipmentMaintenanceMeta"
                               FontFamily="{StaticResource FontInterSemiBold}"
                               FontSize="14"
                               TextColor="{StaticResource NavyDeep}"
                               Text="" />

                        <Label x:Name="EquipmentSummary"
                               Style="{StaticResource BodyMd}"
                               TextColor="{StaticResource TextMuted}"
                               Text="Active fleet across cardio, strength, and studio zones." />

                        <c:PrimaryButton x:Name="BrowseEquipmentButton"
                                         Text="VIEW EQUIPMENT" />
                    </VerticalStackLayout>
                </Border>
```

- [ ] **Step 2: Wire the code-behind**

In `Gymers/Pages/DashboardPage.xaml.cs`, inside the existing constructor, after the line `BrowsePlansButton.Clicked += async (_, _) => await Shell.Current.GoToAsync("//Workouts");` (currently lines 20–21), add:

```csharp
        ApplyEquipmentStatus();
        BrowseEquipmentButton.Clicked += async (_, _) =>
            await Shell.Current.GoToAsync("//Equipment");
```

Then append a new method to the class (after `ApplyFeaturedPlan`):

```csharp
    void ApplyEquipmentStatus()
    {
        int total       = _data.Equipment.Count;
        int operational = _data.OperationalEquipmentCount();
        int maintenance = _data.MaintenanceEquipmentCount();

        if (total == 0)
        {
            EquipmentHeadline.Text          = "No equipment configured.";
            EquipmentTotalLabel.IsVisible   = false;
            EquipmentMaintenanceMeta.IsVisible = false;
            EquipmentSummary.IsVisible      = false;
            BrowseEquipmentButton.IsVisible = false;
            return;
        }

        EquipmentHeadline.Text         = $"{operational} / {total}";
        EquipmentTotalLabel.Text       = $"{total} item{(total == 1 ? "" : "s")}";
        EquipmentMaintenanceMeta.Text  = maintenance == 1
            ? "1 under maintenance"
            : $"{maintenance} under maintenance";
    }
```

- [ ] **Step 3: Build clean**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo -clp:ErrorsOnly`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Gymers/Pages/DashboardPage.xaml Gymers/Pages/DashboardPage.xaml.cs
git commit -m "feat(dashboard): wire Equipment Status card + Equipment nav"
```

---

## Task 9: Mac Catalyst smoke test

**Files:**
- (none — runtime verification only)

- [ ] **Step 1: Full build, both targets**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo`
Expected: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios -nologo`
Expected: same — 0 warnings, 0 errors.

- [ ] **Step 2: Launch on Mac Catalyst**

Per the smoke-test memory: green `dotnet build` does NOT guarantee runtime liveness on MAUI (static cctor crashes are invisible to the compiler). The app must be launched and visually confirmed.

Run:
```
dotnet build Gymers/Gymers.csproj -t:Run -f net10.0-maccatalyst
```

- [ ] **Step 3: Verify in the running app**

Tick each:
- [ ] Sign in as `admin` / `admin123`.
- [ ] Dashboard loads.
- [ ] **Coach Spotlight** still shows Marcus Sterling.
- [ ] **Featured Workout Plan** card still shows `Foundations of Strength`.
- [ ] **Equipment Status** card shows headline `5 / 6`, total label `6 items`, meta `1 under maintenance`, summary populated.
- [ ] Tap **VIEW EQUIPMENT** → EquipmentPage opens.
- [ ] KPI shows `Operational: 5  of 6 items`.
- [ ] List renders 6 rows; top row is `Treadmill TR-01`; subtitle `Cardio · Operational · Cardio Zone`.
- [ ] Row 4 is `Smith Machine SM-01`; subtitle `Strength · Maintenance · Weight Room`.
- [ ] Type `smith` into search → list filters to single row.
- [ ] Type `zzz` → muted `No equipment matches "zzz".` label.
- [ ] Tap any BottomTabBar pill (e.g. Members) → navigates out of Equipment.
- [ ] Trainers + Workouts pages still load via their dashboard buttons.
- [ ] Members, Payments (tap a row → PDF receipt), Attendance, Reports (export PDF) all still work.
- [ ] Force-quit and relaunch → Equipment Status + Equipment list persist (no re-seed).

- [ ] **Step 4: If any verification fails**

Diagnose the failure and patch in a follow-up commit before moving to Task 10. Do NOT proceed past this gate with a runtime regression.

---

## Task 10: Screenshot + status doc

**Files:**
- Create: `docs/status/screenshots/09-equipment.png`
- Modify: `docs/status/build_status_docx.py`
- Modify: `docs/status/gymers-mobile-app-status-update.html`

- [ ] **Step 1: Capture the screenshot**

With EquipmentPage open in the running app, capture a window screenshot (mirror the `08-workouts.png` framing — full window, no chrome). Save as `docs/status/screenshots/09-equipment.png`.

- [ ] **Step 2: Add the completed-features row in `build_status_docx.py`**

Open `docs/status/build_status_docx.py`. After the existing Workout Plans row, append a new row inside the same `completed_rows` array:

```python
        ["Equipment management",
         "Completed",
         "A SQLite-backed Equipment screen lists the gym's equipment roster with a live name-search filter and an Operational-count KPI; each row shows category, status, and floor location. The Dashboard's new Equipment Status card surfaces operational-vs-maintenance counts and its VIEW EQUIPMENT button navigates to the Equipment screen via //Equipment."],
```

Also update the opening summary paragraph so the screen / scope language reads cleanly with Equipment shipped (e.g. drop the "Equipment module from the original scope is the last remaining deferred item" sentence, replace with a "Every README scope item is now implemented" line). Drop the "Equipment module" bullet from the Ongoing Tasks list.

- [ ] **Step 3: Mirror the change in the HTML status doc**

In `docs/status/gymers-mobile-app-status-update.html`:
- Find the Workout Plans completed row and add a parallel Equipment management row right after it.
- Update the summary paragraph to drop "The Equipment module from the original scope is the last remaining deferred item" and replace with a line stating every README scope item is implemented.
- Remove the Equipment-module bullet from the Ongoing Tasks `<ul>`.
- Add an `<h3>Screenshot 9: Equipment Roster</h3>` block with `<img src="screenshots/09-equipment.png" alt="Equipment screen">` after the Workouts screenshot block, before the Login Error State (which is currently numbered "Screenshot 9" — bump it to "Screenshot 10" or "Login Error State" without a number).

- [ ] **Step 4: Regenerate the .docx locally (sanity check)**

Run: `python3 docs/status/build_status_docx.py`
Expected: writes `docs/status/Gymers-Mobile-App-Status-Update.docx` (gitignored, do not commit).

- [ ] **Step 5: Commit the doc + screenshot changes**

```bash
git add docs/status/screenshots/09-equipment.png docs/status/build_status_docx.py docs/status/gymers-mobile-app-status-update.html
git commit -m "docs(status): mark Equipment management as completed + add screenshot"
```

---

## Self-Review

| Spec section | Implemented by |
|---|---|
| §5.1 Model              | Task 1 |
| §5.2 Row + DB           | Task 2 |
| §5.3 DataStore wiring   | Task 4 |
| §5.4 Sample data        | Task 3 |
| §5.5 EquipmentPage      | Task 6 |
| §5.6 Dashboard hook     | Task 8 |
| §5.7 AppShell + DI      | Task 7 |
| §5.8 BottomTabBar enum  | Task 5 |
| §6  Data flow           | Tasks 2–4 (composes naturally) |
| §7  Edge cases          | Empty-table fallback in Task 8 step 2; empty search by `Render` in Task 6; unknown status by `MaintenanceEquipmentCount = total − operational` in Task 4 |
| §8  Verification        | Task 9 |
| Status doc + screenshot | Task 10 |

No spec section is left unrealized. No placeholders. Type / method names verified consistent across tasks (`Equipment` model, `Equipment` DataStore property, `EquipmentRow`, `IsEquipmentEmpty`, `SeedEquipment`, `GetEquipmentOrdered`, `SearchEquipment`, `OperationalEquipmentCount`, `MaintenanceEquipmentCount`, `AppTab.Equipment`, `EquipmentPage`, `//Equipment`).
