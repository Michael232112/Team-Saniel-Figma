# Trainer Module Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a SQLite-backed Trainer roster with a TrainersPage (read-only list + name search), wire the Dashboard's Coach Spotlight to the top-rated trainer, and make the dashboard's "VIEW PERFORMANCE PROFILE" button navigate to TrainersPage.

**Architecture:** New `Trainer` aggregate parallels the existing `Member` aggregate end-to-end — record + `[PrimaryKey]` row + `GymersDb` extension + `SampleData` seed + `DataStore` `ObservableCollection` + page mirroring `MembersPage`. No MVVM; imperative code-behind, identical pattern. One additive enum value (`AppTab.Trainers`) lets `BottomTabBar` render unhighlighted on TrainersPage with no new pill. One targeted refactor: lift `MakeInitialAvatar` from `MembersPage` into a shared `Controls/AvatarFactory.cs`.

**Tech Stack:** .NET 10 MAUI, `sqlite-net-pcl`, Manrope/Inter/Lucide fonts. Target frameworks `net10.0-ios` + `net10.0-maccatalyst`. Mac Catalyst is the verification target per project memory.

**Spec:** `docs/superpowers/specs/2026-05-10-trainer-module-design.md`

**No automated test harness in this codebase.** "Verify" means a clean `dotnet build` for `net10.0-maccatalyst` (0 warnings, 0 errors) and, at the end, a manual smoke test on Mac Catalyst per the user's "smoke-test after startup-code changes" memory.

**Build verify command (used at the end of every task):**
```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst
```
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

---

## Task 1: Add the `Trainer` record

**Files:**
- Create: `Gymers/Models/Trainer.cs`

- [ ] **Step 1: Create the model file**

Create `Gymers/Models/Trainer.cs` with this content:

```csharp
namespace Gymers.Models;

public record Trainer(
    string  Id,
    string  Name,
    string  Title,
    decimal Rating,
    int     SessionsCompleted);
```

- [ ] **Step 2: Build to confirm the new type compiles**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 3: Commit**

```bash
git add Gymers/Models/Trainer.cs
git commit -m "feat(trainers): add Trainer record"
```

---

## Task 2: Add `TrainerRow` and extend `GymersDb`

**Files:**
- Create: `Gymers/Data/Rows/TrainerRow.cs`
- Modify: `Gymers/Data/GymersDb.cs`

- [ ] **Step 1: Create the SQLite row class**

Create `Gymers/Data/Rows/TrainerRow.cs`:

```csharp
using SQLite;

namespace Gymers.Data.Rows;

public class TrainerRow
{
    [PrimaryKey] public string Id                { get; set; } = "";
    public string             Name              { get; set; } = "";
    public string             Title             { get; set; } = "";
    public string             RatingText        { get; set; } = "";
    public int                SessionsCompleted { get; set; }
}
```

`RatingText` mirrors `PaymentRow.AmountText` — `decimal` is persisted as an invariant-culture string and parsed back. Same convention used for Payment amounts.

- [ ] **Step 2: Register the table in the `GymersDb` constructor**

Open `Gymers/Data/GymersDb.cs`. Find this block in the ctor (around line 17–21):

```csharp
_sync = new SQLiteConnection(path);
_sync.CreateTable<MemberRow>();
_sync.CreateTable<PaymentRow>();
_sync.CreateTable<CheckInRow>();
```

Add the trainer table line at the end:

```csharp
_sync = new SQLiteConnection(path);
_sync.CreateTable<MemberRow>();
_sync.CreateTable<PaymentRow>();
_sync.CreateTable<CheckInRow>();
_sync.CreateTable<TrainerRow>();
```

- [ ] **Step 3: Add seed-empty / seed / read helpers to `GymersDb`**

In `Gymers/Data/GymersDb.cs`, add these three methods alongside the existing `IsMembersEmpty` / `SeedMembers` / `GetMembers` family. Place them right after `GetCheckInsNewestFirst()` (so the public API is grouped per aggregate):

```csharp
public bool IsTrainersEmpty() =>
    _sync.Table<TrainerRow>().Count() == 0;

public void SeedTrainers(IEnumerable<Trainer> trainers)
{
    foreach (var t in trainers) _sync.Insert(ToRow(t));
}

public IEnumerable<Trainer> GetTrainersByRatingDesc() =>
    _sync.Table<TrainerRow>()
         .ToList()
         .OrderByDescending(r => decimal.Parse(r.RatingText, CultureInfo.InvariantCulture))
         .ThenByDescending(r => r.SessionsCompleted)
         .Select(ToRecord);
```

`ThenByDescending(SessionsCompleted)` is a deterministic tiebreaker — two trainers tied at 4.7 will sort by experience.

`OrderByDescending` is done in-memory (after `ToList()`) because `RatingText` is a string column; sorting a `decimal`-as-string column at the SQLite layer would sort lexicographically, which is wrong.

- [ ] **Step 4: Add the `Trainer` ↔ `TrainerRow` conversions**

At the bottom of `Gymers/Data/GymersDb.cs`, after the `static CheckIn ToRecord(CheckInRow r) => …` line, add:

```csharp
static TrainerRow ToRow(Trainer t) => new()
{
    Id                = t.Id,
    Name              = t.Name,
    Title             = t.Title,
    RatingText        = t.Rating.ToString(CultureInfo.InvariantCulture),
    SessionsCompleted = t.SessionsCompleted
};

static Trainer ToRecord(TrainerRow r) => new(
    r.Id,
    r.Name,
    r.Title,
    decimal.Parse(r.RatingText, CultureInfo.InvariantCulture),
    r.SessionsCompleted);
```

- [ ] **Step 5: Build to verify**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst`
Expected: `Build succeeded.` `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 6: Commit**

```bash
git add Gymers/Data/Rows/TrainerRow.cs Gymers/Data/GymersDb.cs
git commit -m "feat(trainers): add TrainerRow + GymersDb extensions"
```

---

## Task 3: Add trainer sample data

**Files:**
- Modify: `Gymers/Data/SampleData.cs`

- [ ] **Step 1: Add the trainers seed**

Open `Gymers/Data/SampleData.cs`. Right after the `TodaysClasses` array (before the static `GetMember` helper), add:

```csharp
public static readonly IReadOnlyList<Trainer> Trainers = new[]
{
    new Trainer("t1", "Marcus Sterling", "Lead Performance Coach", 4.9m, 142),
    new Trainer("t2", "Sienna Vega",     "HIIT Specialist",        4.8m, 118),
    new Trainer("t3", "Rohan Iyer",      "Strength Coach",         4.7m,  96),
    new Trainer("t4", "Maya Okafor",     "Yoga Instructor",        4.7m,  88),
    new Trainer("t5", "Caleb Whit",      "Mobility Coach",         4.5m,  64),
};
```

Marcus Sterling stays first so the existing Coach Spotlight visuals don't shift when this slice ships.

- [ ] **Step 2: Build to verify**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst`
Expected: `Build succeeded.` `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Commit**

```bash
git add Gymers/Data/SampleData.cs
git commit -m "feat(trainers): seed five sample trainers"
```

---

## Task 4: Extend `DataStore` with `Trainers`, `SearchTrainers`, `TopTrainer`

**Files:**
- Modify: `Gymers/Data/DataStore.cs`

- [ ] **Step 1: Add the `Trainers` collection property**

Open `Gymers/Data/DataStore.cs`. Find the existing collection-property block:

```csharp
public ObservableCollection<Member>  Members  { get; }
public ObservableCollection<Payment> Payments { get; }
public ObservableCollection<CheckIn> CheckIns { get; }
```

Append:

```csharp
public ObservableCollection<Trainer> Trainers { get; }
```

- [ ] **Step 2: Seed and load trainers in the ctor**

In the ctor, find this block:

```csharp
if (_db.IsMembersEmpty())
{
    _db.SeedMembers(SampleData.Members);
    _db.SeedPayments(SampleData.Payments);
    _db.SeedCheckIns(SampleData.CheckIns);
}

Members  = new ObservableCollection<Member>(_db.GetMembers());
Payments = new ObservableCollection<Payment>(_db.GetPaymentsNewestFirst());
CheckIns = new ObservableCollection<CheckIn>(_db.GetCheckInsNewestFirst());
```

Add a separate seed-empty check for trainers (so existing demo databases that already have members but no trainers still get seeded), and load trainers into the new collection:

```csharp
if (_db.IsMembersEmpty())
{
    _db.SeedMembers(SampleData.Members);
    _db.SeedPayments(SampleData.Payments);
    _db.SeedCheckIns(SampleData.CheckIns);
}

if (_db.IsTrainersEmpty())
{
    _db.SeedTrainers(SampleData.Trainers);
}

Members  = new ObservableCollection<Member>(_db.GetMembers());
Payments = new ObservableCollection<Payment>(_db.GetPaymentsNewestFirst());
CheckIns = new ObservableCollection<CheckIn>(_db.GetCheckInsNewestFirst());
Trainers = new ObservableCollection<Trainer>(_db.GetTrainersByRatingDesc());
```

The two separate empty-checks matter: an existing on-device DB from a prior build will have a non-empty members table, so a single combined check would never seed trainers. Splitting them ensures upgrades populate the new table once.

- [ ] **Step 3: Add `SearchTrainers` and `TopTrainer` helpers**

After the existing `SearchMembers` method, add:

```csharp
public IEnumerable<Trainer> SearchTrainers(string? query) =>
    string.IsNullOrWhiteSpace(query)
        ? Trainers
        : Trainers.Where(t =>
            t.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

public Trainer? TopTrainer() => Trainers.FirstOrDefault();
```

`TopTrainer` returns nullable so the dashboard can guard against an empty (manually-wiped) trainers table without crashing (per spec §7).

- [ ] **Step 4: Build to verify**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst`
Expected: `Build succeeded.` `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 5: Commit**

```bash
git add Gymers/Data/DataStore.cs
git commit -m "feat(trainers): expose Trainers + search + top in DataStore"
```

---

## Task 5: Extract `MakeInitialAvatar` into `Controls/AvatarFactory`

This is the one targeted refactor justified by the slice (per spec §9). Lifting it now keeps `MembersPage` and the new `TrainersPage` from duplicating the same 25 lines.

**Files:**
- Create: `Gymers/Controls/AvatarFactory.cs`
- Modify: `Gymers/Pages/MembersPage.xaml.cs`

- [ ] **Step 1: Create the shared factory**

Create `Gymers/Controls/AvatarFactory.cs`:

```csharp
using Microsoft.Maui.Controls.Shapes;

namespace Gymers.Controls;

public static class AvatarFactory
{
    public static View MakeInitial(string name)
    {
        var pale = (Color)Application.Current!.Resources["PaleBlue"];
        var navy = (Color)Application.Current.Resources["NavyHeading"];
        var initial = name.Length > 0 ? name[0].ToString() : "?";
        return new Border
        {
            BackgroundColor = pale,
            StrokeThickness = 0,
            WidthRequest = 40,
            HeightRequest = 40,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            Content = new Label
            {
                Text = initial,
                FontFamily = "ManropeBold",
                FontSize = 16,
                TextColor = navy,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
    }
}
```

This is a verbatim lift of the body of the old static `MakeInitialAvatar` method on `MembersPage`, renamed `MakeInitial` for the type-prefixed call site (`AvatarFactory.MakeInitial(...)`).

- [ ] **Step 2: Replace the in-page method with the factory call**

Open `Gymers/Pages/MembersPage.xaml.cs`. Delete the entire static method (lines roughly 56–78 in the current file):

```csharp
static View MakeInitialAvatar(string name)
{
    var pale = (Color)Application.Current!.Resources["PaleBlue"];
    var navy = (Color)Application.Current.Resources["NavyHeading"];
    var initial = name.Length > 0 ? name[0].ToString() : "?";
    return new Border
    {
        BackgroundColor = pale,
        StrokeThickness = 0,
        WidthRequest = 40,
        HeightRequest = 40,
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
        Content = new Label
        {
            Text = initial,
            FontFamily = "ManropeBold",
            FontSize = 16,
            TextColor = navy,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        }
    };
}
```

In `BuildRow`, replace `MakeInitialAvatar(m.Name)` with `AvatarFactory.MakeInitial(m.Name)`. The full updated `BuildRow` reads:

```csharp
static View BuildRow(Member m) => new ListRow
{
    LeadingContent = AvatarFactory.MakeInitial(m.Name),
    Title          = m.Name,
    Subtitle       = $"{m.Tier} · {m.Status} · Expires {m.Expires:MM/dd/yyyy}"
};
```

The `using Microsoft.Maui.Controls.Shapes;` import at the top of `MembersPage.xaml.cs` is no longer needed (the only consumer was the deleted method). Remove it. The file's remaining `using` block should be:

```csharp
using System.ComponentModel;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
```

- [ ] **Step 3: Build to verify the refactor compiles**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst`
Expected: `Build succeeded.` `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
git add Gymers/Controls/AvatarFactory.cs Gymers/Pages/MembersPage.xaml.cs
git commit -m "refactor(controls): extract MakeInitialAvatar to AvatarFactory"
```

---

## Task 6: Add `AppTab.Trainers` enum value

**Files:**
- Modify: `Gymers/Controls/BottomTabBar.xaml.cs`

- [ ] **Step 1: Extend the enum**

Open `Gymers/Controls/BottomTabBar.xaml.cs`. Replace the existing enum line:

```csharp
public enum AppTab { Dashboard, Members, Payments, Attendance, Reports }
```

with:

```csharp
public enum AppTab { Dashboard, Members, Payments, Attendance, Reports, Trainers }
```

No other change. `ApplyActive` already does `ActiveTab == AppTab.Dashboard ? pale : Colors.Transparent` for each pill — when `ActiveTab=Trainers`, every comparison is false, every pill renders unhighlighted, and the bar still functions as a navigation surface (each pill's tap handler routes to `//<route>`).

- [ ] **Step 2: Build to verify**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst`
Expected: `Build succeeded.` `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Commit**

```bash
git add Gymers/Controls/BottomTabBar.xaml.cs
git commit -m "feat(controls): add AppTab.Trainers (no new pill rendered)"
```

---

## Task 7: Build TrainersPage (XAML + code-behind)

**Files:**
- Create: `Gymers/Pages/TrainersPage.xaml`
- Create: `Gymers/Pages/TrainersPage.xaml.cs`

- [ ] **Step 1: Create the XAML**

Create `Gymers/Pages/TrainersPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:c="clr-namespace:Gymers.Controls"
             x:Class="Gymers.Pages.TrainersPage"
             BackgroundColor="{StaticResource BgApp}"
             Shell.NavBarIsVisible="False">

    <Grid RowDefinitions="Auto,*,Auto">

        <c:TopAppBar Grid.Row="0" Title="Trainers"
                     TrailingIconGlyph="{x:Static c:Icons.Users}" />

        <ScrollView Grid.Row="1" Padding="24,16">
            <VerticalStackLayout Spacing="16">
                <c:SearchField x:Name="Search" Placeholder="Search by name…" />

                <c:KpiCard Variant="Light"
                           Label="Active Trainers" Value="5"
                           DeltaText="+2" DeltaDirection="Up"
                           Caption="this month"
                           TrailingIconGlyph="{x:Static c:Icons.Users}" />

                <Label Style="{StaticResource H2Section}" Text="All Trainers" />

                <VerticalStackLayout x:Name="TrainerList" Spacing="12" />
            </VerticalStackLayout>
        </ScrollView>

        <c:BottomTabBar Grid.Row="2" ActiveTab="Trainers" />
    </Grid>
</ContentPage>
```

The KPI value is hardcoded to `"5"` — this matches the existing pattern on MembersPage (`Value="1,250"`) and PaymentsPage (the dashboard card uses sample-style figures). Don't bind it to `_data.Trainers.Count` in this slice; that introduces a partial-MVVM pattern the rest of the app doesn't have. If trainer count becomes dynamic later, do it in a follow-up.

- [ ] **Step 2: Create the code-behind**

Create `Gymers/Pages/TrainersPage.xaml.cs`:

```csharp
using System.ComponentModel;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;

namespace Gymers.Pages;

public partial class TrainersPage : ContentPage
{
    readonly DataStore _data;

    public TrainersPage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        Search.PropertyChanged += OnSearchChanged;
        _data.Trainers.CollectionChanged += (_, _) => Render(Search.Text ?? "");
        Render("");
    }

    void OnSearchChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchField.Text))
            Render(Search.Text ?? "");
    }

    void Render(string query)
    {
        TrainerList.Children.Clear();
        var matches = _data.SearchTrainers(query).ToList();

        if (matches.Count == 0)
        {
            TrainerList.Children.Add(new Label
            {
                Text = $"No trainers match \"{query.Trim()}\".",
                Style = (Style)Application.Current!.Resources["BodyMd"],
                TextColor = (Color)Application.Current.Resources["TextMuted"],
                HorizontalTextAlignment = TextAlignment.Center
            });
            return;
        }

        foreach (var t in matches)
            TrainerList.Children.Add(BuildRow(t));
    }

    static View BuildRow(Trainer t) => new ListRow
    {
        LeadingContent = AvatarFactory.MakeInitial(t.Name),
        Title          = t.Name,
        Subtitle       = $"{t.Title} · {t.Rating:0.0}/5.0 · {t.SessionsCompleted} sessions"
    };
}
```

This is a structural mirror of `MembersPage.xaml.cs` after Task 5 — same constructor shape, same `OnSearchChanged` / `Render` pair, same empty-state pattern, same `BuildRow` style. The subtitle format follows spec §5.5: `"Lead Performance Coach · 4.9/5.0 · 142 sessions"`.

- [ ] **Step 3: Build to verify**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst`
Expected: `Build succeeded.` `0 Warning(s)`, `0 Error(s)`.

If you see `XFC0000` ("Cannot resolve type 'Trainers' for ActiveTab"), Task 6 wasn't applied — go back and add the enum value.

- [ ] **Step 4: Commit**

```bash
git add Gymers/Pages/TrainersPage.xaml Gymers/Pages/TrainersPage.xaml.cs
git commit -m "feat(trainers): add TrainersPage with name search"
```

---

## Task 8: Register the Trainers Shell route + DI

**Files:**
- Modify: `Gymers/AppShell.xaml`
- Modify: `Gymers/MauiProgram.cs`

- [ ] **Step 1: Add the Shell route**

Open `Gymers/AppShell.xaml`. Find the `<TabBar>` block:

```xml
<TabBar>
    <ShellContent Route="Dashboard"  ContentTemplate="{DataTemplate pages:DashboardPage}" />
    <ShellContent Route="Members"    ContentTemplate="{DataTemplate pages:MembersPage}" />
    <ShellContent Route="Payments"   ContentTemplate="{DataTemplate pages:PaymentsPage}" />
    <ShellContent Route="Attendance" ContentTemplate="{DataTemplate pages:AttendancePage}" />
    <ShellContent Route="Reports"    ContentTemplate="{DataTemplate pages:ReportsPage}" />
</TabBar>
```

After the closing `</TabBar>` (and before `</Shell>`), add a peer ShellContent — outside the TabBar, like the existing Login route — so Trainers is reachable via `//Trainers` but not implied as a tab:

```xml
<ShellContent Route="Trainers"
              ContentTemplate="{DataTemplate pages:TrainersPage}" />
```

The full `<Shell>` body should now be:

```xml
<ShellContent Title="Login"
              Route="Login"
              ContentTemplate="{DataTemplate pages:LoginPage}" />

<TabBar>
    <ShellContent Route="Dashboard"  ContentTemplate="{DataTemplate pages:DashboardPage}" />
    <ShellContent Route="Members"    ContentTemplate="{DataTemplate pages:MembersPage}" />
    <ShellContent Route="Payments"   ContentTemplate="{DataTemplate pages:PaymentsPage}" />
    <ShellContent Route="Attendance" ContentTemplate="{DataTemplate pages:AttendancePage}" />
    <ShellContent Route="Reports"    ContentTemplate="{DataTemplate pages:ReportsPage}" />
</TabBar>

<ShellContent Route="Trainers"
              ContentTemplate="{DataTemplate pages:TrainersPage}" />
```

- [ ] **Step 2: Register the page in DI**

Open `Gymers/MauiProgram.cs`. Find the existing transient-page block:

```csharp
builder.Services.AddTransient<Pages.LoginPage>();
builder.Services.AddTransient<Pages.DashboardPage>();
builder.Services.AddTransient<Pages.MembersPage>();
builder.Services.AddTransient<Pages.PaymentsPage>();
builder.Services.AddTransient<Pages.AttendancePage>();
builder.Services.AddTransient<Pages.ReportsPage>();
```

Append the trainers registration:

```csharp
builder.Services.AddTransient<Pages.TrainersPage>();
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst`
Expected: `Build succeeded.` `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
git add Gymers/AppShell.xaml Gymers/MauiProgram.cs
git commit -m "feat(trainers): register TrainersPage route + DI"
```

---

## Task 9: Wire Coach Spotlight + "VIEW PERFORMANCE PROFILE" on DashboardPage

**Files:**
- Modify: `Gymers/Pages/DashboardPage.xaml`
- Modify: `Gymers/Pages/DashboardPage.xaml.cs`

- [ ] **Step 1: Name the four spotlight labels and the button in XAML**

Open `Gymers/Pages/DashboardPage.xaml`. The Coach Spotlight `<Border>` block currently has hardcoded values. Replace its contents (the existing block from `<!-- Coach Spotlight -->` through its closing `</Border>`) with this version. The structure and styling are unchanged; only `x:Name` attributes are added so the code-behind can set the leaf text values:

```xml
<!-- Coach Spotlight -->
<Border Style="{StaticResource Card}">
    <VerticalStackLayout Spacing="24">
        <Border WidthRequest="80" HeightRequest="80"
                BackgroundColor="{StaticResource PaleBlue}"
                StrokeThickness="0" HorizontalOptions="Start">
            <Border.StrokeShape>
                <RoundRectangle CornerRadius="24" />
            </Border.StrokeShape>
            <Label x:Name="CoachInitials"
                   Text="MS"
                   FontFamily="{StaticResource FontManropeBold}"
                   FontSize="28"
                   TextColor="{StaticResource NavyHeading}"
                   HorizontalTextAlignment="Center"
                   VerticalTextAlignment="Center" />
        </Border>

        <VerticalStackLayout Spacing="0">
            <Label x:Name="CoachName" Style="{StaticResource H3Card}" Text="Marcus Sterling" />
            <Label x:Name="CoachTitle"
                   FontFamily="{StaticResource FontInterSemiBold}"
                   FontSize="14"
                   TextColor="{StaticResource NavyDeep}"
                   Text="Lead Performance Coach" />
        </VerticalStackLayout>

        <VerticalStackLayout Spacing="16">
            <Grid ColumnDefinitions="*,Auto">
                <Label Grid.Column="0" Style="{StaticResource BodyMd}" Text="Client Rating" />
                <Label x:Name="CoachRating" Grid.Column="1"
                       FontFamily="{StaticResource FontInterSemiBold}"
                       FontSize="14"
                       TextColor="{StaticResource TextPrimary}"
                       Text="4.9/5.0" />
            </Grid>
            <Grid ColumnDefinitions="*,Auto">
                <Label Grid.Column="0" Style="{StaticResource BodyMd}" Text="Sessions Completed" />
                <Label x:Name="CoachSessions" Grid.Column="1"
                       FontFamily="{StaticResource FontInterSemiBold}"
                       FontSize="14"
                       TextColor="{StaticResource TextPrimary}"
                       Text="142" />
            </Grid>
        </VerticalStackLayout>

        <c:PrimaryButton x:Name="ProfileButton" Text="VIEW PERFORMANCE PROFILE" />
    </VerticalStackLayout>
</Border>
```

The hardcoded `Text=` values stay as design-time placeholders so the XAML preview still shows something sensible; the code-behind overwrites them with real data on first render.

- [ ] **Step 2: Inject `DataStore`, set the spotlight, wire the button**

Open `Gymers/Pages/DashboardPage.xaml.cs`. Replace the entire current ctor and add an `ApplyCoachSpotlight` method. The full updated file body should read:

```csharp
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Microsoft.Maui.Controls.Shapes;

namespace Gymers.Pages;

public partial class DashboardPage : ContentPage
{
    readonly DataStore _data;

    public DashboardPage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        ApplyCoachSpotlight();
        ProfileButton.Clicked += async (_, _) =>
            await Shell.Current.GoToAsync("//Trainers");
        BuildClassList();
    }

    void ApplyCoachSpotlight()
    {
        var top = _data.TopTrainer();
        if (top is null) return;   // empty trainers table — keep design-time XAML text

        CoachInitials.Text = InitialsFor(top.Name);
        CoachName.Text     = top.Name;
        CoachTitle.Text    = top.Title;
        CoachRating.Text   = $"{top.Rating:0.0}/5.0";
        CoachSessions.Text = top.SessionsCompleted.ToString("N0");
    }

    static string InitialsFor(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
        if (parts.Length == 1 && parts[0].Length >= 2)
            return parts[0][..2].ToUpperInvariant();
        if (parts.Length == 1 && parts[0].Length == 1)
            return parts[0].ToUpperInvariant();
        return "?";
    }

    void BuildClassList()
    {
        foreach (var cls in SampleData.TodaysClasses)
        {
            ClassList.Children.Add(new ListRow
            {
                LeadingContent = MakeDatePill(cls.Start),
                Title          = cls.Title,
                Subtitle       = $"{cls.Location} • {cls.Start:hh\\:mm tt}–{cls.End:hh\\:mm tt}",
                TrailingChevron = true
            });
        }
    }

    static View MakeDatePill(DateTime when)
    {
        var pale = (Color)Application.Current!.Resources["PaleBlue"];
        var navy = (Color)Application.Current.Resources["NavyMid"];
        return new Border
        {
            BackgroundColor = pale,
            StrokeThickness = 0,
            WidthRequest = 48,
            HeightRequest = 48,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24) },
            Content = new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                Spacing = 0,
                Children =
                {
                    new Label
                    {
                        Text = when.Day.ToString(),
                        FontFamily = "ManropeBold",
                        FontSize = 18,
                        TextColor = navy,
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    new Label
                    {
                        Text = when.ToString("MMM").ToUpperInvariant(),
                        FontFamily = "InterSemiBold",
                        FontSize = 8,
                        TextColor = navy,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            }
        };
    }
}
```

Note the ctor signature change: `DashboardPage(DataStore data)` instead of `DashboardPage()`. The DI container already binds `DataStore` as a singleton (per `MauiProgram.cs`), and `DashboardPage` is registered transient, so this Just Works — same wiring `MembersPage`, `PaymentsPage`, etc. already use.

`InitialsFor` implements the spec's whitespace-split rule (§5.6 of the design): two-part names → "MS"; single-part names ≥2 chars → first two letters; single char → that char; empty → "?".

- [ ] **Step 3: Build to verify**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst`
Expected: `Build succeeded.` `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
git add Gymers/Pages/DashboardPage.xaml Gymers/Pages/DashboardPage.xaml.cs
git commit -m "feat(dashboard): wire Coach Spotlight + Trainers nav"
```

---

## Task 10: Smoke test on Mac Catalyst + status doc update

**Files:**
- Modify: `docs/status/gymers-mobile-app-status-update.html`

**Why this task exists:** the project's "smoke-test after startup-code changes" memory says a green `dotnet build` doesn't catch static-cctor crashes — launch the binary on Mac Catalyst and confirm the new flows work before declaring done. We also need to update the public status doc so it reflects the trainer module shipping.

- [ ] **Step 1: Run the app on Mac Catalyst**

Run:

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -t:Run
```

(or use the IDE's Mac Catalyst run config — whichever the project has been using). Expected: app launches, Login screen appears.

- [ ] **Step 2: Walk the smoke test**

Sign in with `admin` / `admin123`. Then verify each:

1. **Dashboard's Coach Spotlight** shows "MS" / "Marcus Sterling" / "Lead Performance Coach" / "4.9/5.0" / "142". (No regression vs the previous build — the values match the existing hardcoded ones, which is the demo-continuity decision in spec §5.4.)
2. **Tap "VIEW PERFORMANCE PROFILE"** → TrainersPage appears with five trainers, top is Marcus Sterling, BottomTabBar visible at the bottom with no pill highlighted.
3. **Type "vega"** in the search field → list filters to "Sienna Vega".
4. **Type "zzz"** → muted "No trainers match \"zzz\"." message.
5. **Clear the search** → all five trainers reappear.
6. **Tap the Dashboard pill in BottomTabBar** → returns to Dashboard.
7. **Force-quit and relaunch the app** → trainers persist; Coach Spotlight still shows the right person (i.e. the seed didn't double-insert).
8. **Existing flows still work**: Members search, payment recording (PDF receipt opens), check-in flow, report PDF/CSV export from Reports tab.

If any of those fail, fix and rebuild before continuing.

- [ ] **Step 3: Update the status doc**

Open `docs/status/gymers-mobile-app-status-update.html`. Two changes:

(a) In the **Overall Status** paragraph (around line 97), change "The trainer / workout / equipment modules from the original scope are deferred to the next iteration." to:

```
The workout / equipment modules from the original scope are deferred to the next iteration.
```

(b) In the **Completed Features** table, after the existing Reports row (`<tr>...Reports + export...</tr>`), add a new row for trainers:

```html
<tr>
  <td>Trainer roster</td>
  <td class="status-done">Completed</td>
  <td>A SQLite-backed Trainers screen lists all trainers with a live name-search filter; rows render as <code>ListRow</code>s with initials avatars and "Title · Rating · Sessions" subtitles. The Dashboard's Coach Spotlight now reads from the trainers table (top by rating, with sessions as a tiebreaker), and its <em>VIEW PERFORMANCE PROFILE</em> button navigates to the Trainers screen via <code>//Trainers</code>.</td>
</tr>
```

(c) In the **Ongoing Tasks** list (around line 161), change `<strong>Trainer / Workout Plan / Equipment modules:</strong>` to:

```html
<strong>Workout Plan / Equipment modules:</strong>
```

— since Trainer is no longer ongoing.

- [ ] **Step 4: Final commit**

```bash
git add docs/status/gymers-mobile-app-status-update.html
git commit -m "docs(status): mark Trainer roster as completed"
```

- [ ] **Step 5: Verify both target frameworks build clean**

Per-task we've only built Mac Catalyst for fast feedback. Spec §2 Goal 5 requires a clean build on iOS too. Build the whole project (no `-f`, so both `net10.0-ios` and `net10.0-maccatalyst` are produced):

```bash
dotnet build Gymers/Gymers.csproj
```

Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)` for **both** TFMs (you'll see two "Build succeeded" lines, one per framework).

If iOS-only warnings or errors surface (e.g. iOS-specific API differences), fix them in a follow-up commit before proceeding to Step 6.

- [ ] **Step 6: Verify the branch is clean**

Run: `git status`
Expected: `nothing to commit, working tree clean`.

Run: `git log --oneline -12`
Expected: a sequence of trainer-slice commits ending with the status-doc one, e.g.:
```
<sha> docs(status): mark Trainer roster as completed
<sha> feat(dashboard): wire Coach Spotlight + Trainers nav
<sha> feat(trainers): register TrainersPage route + DI
<sha> feat(trainers): add TrainersPage with name search
<sha> feat(controls): add AppTab.Trainers (no new pill rendered)
<sha> refactor(controls): extract MakeInitialAvatar to AvatarFactory
<sha> feat(trainers): expose Trainers + search + top in DataStore
<sha> feat(trainers): seed five sample trainers
<sha> feat(trainers): add TrainerRow + GymersDb extensions
<sha> feat(trainers): add Trainer record
<sha> docs(spec): correct TopAppBar fact in trainer module spec
<sha> docs(spec): add 2026-05-10 trainer module slice design
```

---

## Out of Scope (do not do in this plan)

- Workout Plan module (trainer FK + exercises + assignment).
- Equipment module (inventory + maintenance dates).
- Per-trainer detail page or photos.
- Test harness / broader test coverage.
- Any change to `TopAppBar`, the existing five tab pills, or `Icons`.
