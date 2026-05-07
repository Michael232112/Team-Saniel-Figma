# Gymers Mobile App — "Make It Real" Slice — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing five-screen iOS demo functional — Login validates real credentials, Members search filters live, Payments / Attendance forms validate and insert into in-memory lists that update on screen.

**Architecture:** Single `DataStore` singleton holds `ObservableCollection<Member|Payment|CheckIn>` seeded from `SampleData`. Pages get the store via constructor injection (DI), subscribe to `CollectionChanged`, and rebuild their child lists imperatively. No new screens, no SQLite, no MVVM toolkit. One new color token (`Danger`).

**Tech Stack:** .NET 10, MAUI, C# 12, XAML. Existing project. iOS 26.2 simulator on macOS.

**Spec:** `docs/superpowers/specs/2026-05-07-app-comes-alive-design.md` (commit `568dccd`).

---

## Files Touched

| File                                                  | Action  | Responsibility                                    |
|-------------------------------------------------------|---------|---------------------------------------------------|
| `Gymers/Data/DataStore.cs`                            | Create  | Mutable in-memory store, single source of truth   |
| `Gymers/MauiProgram.cs`                               | Modify  | Register `DataStore` and pages in DI              |
| `Gymers/Resources/Styles/Colors.xaml`                 | Modify  | Add `Danger` token                                |
| `Gymers/Pages/LoginPage.xaml`                         | Modify  | Add error label, name inputs, update demo caption |
| `Gymers/Pages/LoginPage.xaml.cs`                      | Modify  | Validate fixed credentials per role               |
| `Gymers/Pages/MembersPage.xaml`                       | Modify  | Name the search field                             |
| `Gymers/Pages/MembersPage.xaml.cs`                    | Modify  | Inject store, live filter, empty-state            |
| `Gymers/Pages/PaymentsPage.xaml`                      | Modify  | Name inputs/button, add status label              |
| `Gymers/Pages/PaymentsPage.xaml.cs`                   | Modify  | Inject store, validate + record payments          |
| `Gymers/Pages/AttendancePage.xaml`                    | Modify  | Name search/button, add suggestions + status      |
| `Gymers/Pages/AttendancePage.xaml.cs`                 | Modify  | Inject store, picker UX, record check-ins         |

---

## Run helper (referenced by every task)

When a task says "build and run," do this from the repo root:

**Build:**
```bash
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

**Find a booted iOS 26.2 simulator UDID** (do once per session):
```bash
xcrun simctl list devices "iOS 26.2" booted
```
If none, boot one:
```bash
xcrun simctl boot "iPhone 17"; open -a Simulator
```

**Install + launch:**
```bash
UDID=$(xcrun simctl list devices "iOS 26.2" booted | grep -Eo '\([0-9A-F-]{36}\)' | head -1 | tr -d '()')
xcrun simctl install "$UDID" Gymers/bin/Debug/net10.0-ios/iossimulator-arm64/Gymers.app
xcrun simctl launch "$UDID" com.companyname.gymers
```

---

## Task 1: Foundation — Danger token + DataStore + DI registration

Build the invisible plumbing first. After this task the app runs identically to before, but `DataStore` exists, all pages can request it, and we have one new color.

**Files:**
- Create: `Gymers/Data/DataStore.cs`
- Modify: `Gymers/MauiProgram.cs`
- Modify: `Gymers/Resources/Styles/Colors.xaml`

- [ ] **Step 1: Add the `Danger` color token**

In `Gymers/Resources/Styles/Colors.xaml`, add a single line before the closing `</ResourceDictionary>` tag:

```xml
<Color x:Key="Danger">#B91C1C</Color>
```

- [ ] **Step 2: Create `Gymers/Data/DataStore.cs`**

```csharp
using System.Collections.ObjectModel;
using Gymers.Models;

namespace Gymers.Data;

public sealed class DataStore
{
    public ObservableCollection<Member>  Members  { get; }
    public ObservableCollection<Payment> Payments { get; }
    public ObservableCollection<CheckIn> CheckIns { get; }

    public DataStore()
    {
        Members  = new ObservableCollection<Member>(SampleData.Members);
        Payments = new ObservableCollection<Payment>(
            SampleData.Payments.OrderByDescending(p => p.At));
        CheckIns = new ObservableCollection<CheckIn>(
            SampleData.CheckIns.OrderByDescending(c => c.At));
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

- [ ] **Step 3: Register `DataStore` and pages in `MauiProgram.cs`**

Replace the body of `CreateMauiApp` so it reads end-to-end as:

```csharp
using Microsoft.Extensions.Logging;
using Gymers.Data;

namespace Gymers;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Manrope-Bold.ttf",      "ManropeBold");
                fonts.AddFont("Manrope-ExtraBold.ttf", "ManropeExtraBold");
                fonts.AddFont("Manrope-SemiBold.ttf",  "ManropeSemiBold");
                fonts.AddFont("Inter-Regular.ttf",     "InterRegular");
                fonts.AddFont("Inter-Medium.ttf",      "InterMedium");
                fonts.AddFont("Inter-SemiBold.ttf",    "InterSemiBold");
                fonts.AddFont("Lucide.ttf",            "LucideIcons");
            });

        builder.Services.AddSingleton<DataStore>();

        builder.Services.AddTransient<Pages.LoginPage>();
        builder.Services.AddTransient<Pages.DashboardPage>();
        builder.Services.AddTransient<Pages.MembersPage>();
        builder.Services.AddTransient<Pages.PaymentsPage>();
        builder.Services.AddTransient<Pages.AttendancePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
```

- [ ] **Step 4: Build to verify nothing broke**

Run:
```bash
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 5: Install + launch + smoke-test**

Use the run helper. Expected: app launches into LoginPage, identical to before. Tap *Sign In* — it still navigates to Dashboard (validation comes in Task 2). All four tabs still work. No crashes.

- [ ] **Step 6: Commit**

```bash
git add Gymers/Data/DataStore.cs Gymers/MauiProgram.cs Gymers/Resources/Styles/Colors.xaml
git commit -m "feat(data): add DataStore singleton + Danger color token

DataStore wraps SampleData in ObservableCollections and exposes
FindMemberByName / SearchMembers / RecordPayment / RecordCheckIn.
Registered as a DI singleton; all pages registered transient.
No page yet consumes the store — pure plumbing."
```

---

## Task 2: LoginPage — validate against fixed credentials

After this task, signing in requires real credentials matching the selected role pill.

**Files:**
- Modify: `Gymers/Pages/LoginPage.xaml`
- Modify: `Gymers/Pages/LoginPage.xaml.cs`

- [ ] **Step 1: Update `LoginPage.xaml`**

Two surgical changes:

**a)** Add `x:Name="UsernameInput"` to the first `c:LabeledInput` and `x:Name="PasswordInput"` to the second:

```xml
<c:LabeledInput x:Name="UsernameInput" Label="Username" Placeholder="admin" />
<c:LabeledInput x:Name="PasswordInput" Label="Password" Placeholder="••••••••" IsPassword="True" />
```

**b)** Insert an error label between the role-pill `HorizontalStackLayout` (closing `</HorizontalStackLayout>`) and the `c:PrimaryButton`:

```xml
<Label x:Name="ErrorLabel"
       Style="{StaticResource BodySm}"
       TextColor="{StaticResource Danger}"
       HorizontalTextAlignment="Center"
       IsVisible="False" />
```

**c)** Replace the bottom caption text from `Demo: any username/password works` to `Demo: admin / admin123  ·  staff / staff123`. The `Label` element stays — only `Text` changes.

- [ ] **Step 2: Replace `LoginPage.xaml.cs` body**

```csharp
namespace Gymers.Pages;

public partial class LoginPage : ContentPage
{
    enum SelectedRole { Admin, Staff }
    SelectedRole _role = SelectedRole.Admin;

    public LoginPage() => InitializeComponent();

    void OnSelectAdmin(object? sender, TappedEventArgs e)
    {
        _role = SelectedRole.Admin;
        var navy = (Color)Application.Current!.Resources["NavyDeep"];
        var sec  = (Color)Application.Current.Resources["TextSecondary"];
        AdminPill.BackgroundColor = navy;
        StaffPill.BackgroundColor = Colors.Transparent;
        AdminLabel.TextColor = Colors.White;
        StaffLabel.TextColor = sec;
    }

    void OnSelectStaff(object? sender, TappedEventArgs e)
    {
        _role = SelectedRole.Staff;
        var navy = (Color)Application.Current!.Resources["NavyDeep"];
        var sec  = (Color)Application.Current.Resources["TextSecondary"];
        AdminPill.BackgroundColor = Colors.Transparent;
        StaffPill.BackgroundColor = navy;
        AdminLabel.TextColor = sec;
        StaffLabel.TextColor = Colors.White;
    }

    async void OnSignIn(object? sender, EventArgs e)
    {
        var u = UsernameInput.Text?.Trim() ?? "";
        var p = PasswordInput.Text ?? "";

        if (u.Length == 0 || p.Length == 0)
        { ShowError("Enter username and password."); return; }

        bool ok = (_role == SelectedRole.Admin && u == "admin" && p == "admin123")
               || (_role == SelectedRole.Staff && u == "staff" && p == "staff123");

        if (!ok)
        { ShowError("Invalid credentials for the selected role."); return; }

        ErrorLabel.IsVisible = false;
        await Shell.Current.GoToAsync("//Dashboard");
    }

    void ShowError(string text)
    {
        ErrorLabel.Text = text;
        ErrorLabel.IsVisible = true;
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```
Expected: 0 errors. The XAML-generated partial fields (`UsernameInput`, `PasswordInput`, `ErrorLabel`) compile against the new XAML names.

- [ ] **Step 4: Manual verification**

Reinstall + launch the app. Verify:

1. With both fields empty, tap **SIGN IN** → red label appears: *"Enter username and password."*
2. Type `admin` / `wrong` with **Admin** pill selected → tap SIGN IN → *"Invalid credentials for the selected role."*
3. Type `staff` / `staff123` with **Admin** pill still selected → *"Invalid credentials for the selected role."*
4. Tap **Staff** pill → tap SIGN IN with same `staff/staff123` → lands on Dashboard.
5. Hit the iOS *Home* indicator to background, relaunch — back at LoginPage.
6. Type `admin` / `admin123` with **Admin** pill → lands on Dashboard.

If any of (1)–(6) fails, fix before moving on.

- [ ] **Step 5: Commit**

```bash
git add Gymers/Pages/LoginPage.xaml Gymers/Pages/LoginPage.xaml.cs
git commit -m "feat(login): validate fixed admin/staff credentials per role pill

Empty fields show 'Enter username and password.'
Wrong creds for the selected role show 'Invalid credentials...'
Demo caption now lists the working credentials."
```

---

## Task 3: MembersPage — live search filtering

After this task, typing in the search field filters the visible list immediately.

**Files:**
- Modify: `Gymers/Pages/MembersPage.xaml`
- Modify: `Gymers/Pages/MembersPage.xaml.cs`

- [ ] **Step 1: Name the search field in `MembersPage.xaml`**

Change:
```xml
<c:SearchField Placeholder="Search by name…" />
```
to:
```xml
<c:SearchField x:Name="Search" Placeholder="Search by name…" />
```
No other XAML changes.

- [ ] **Step 2: Replace `MembersPage.xaml.cs` body**

```csharp
using System.ComponentModel;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Microsoft.Maui.Controls.Shapes;

namespace Gymers.Pages;

public partial class MembersPage : ContentPage
{
    readonly DataStore _data;

    public MembersPage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        Search.PropertyChanged += OnSearchChanged;
        _data.Members.CollectionChanged += (_, _) => Render(Search.Text ?? "");
        Render("");
    }

    void OnSearchChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchField.Text))
            Render(Search.Text ?? "");
    }

    void Render(string query)
    {
        MemberList.Children.Clear();
        var matches = _data.SearchMembers(query).ToList();

        if (matches.Count == 0)
        {
            MemberList.Children.Add(new Label
            {
                Text = $"No members match \"{query.Trim()}\".",
                Style = (Style)Application.Current!.Resources["BodyMd"],
                TextColor = (Color)Application.Current.Resources["TextMuted"],
                HorizontalTextAlignment = TextAlignment.Center
            });
            return;
        }

        foreach (var m in matches)
            MemberList.Children.Add(BuildRow(m));
    }

    static View BuildRow(Member m) => new ListRow
    {
        LeadingContent = MakeInitialAvatar(m.Name),
        Title          = m.Name,
        Subtitle       = $"{m.Tier} · {m.Status} · Expires {m.Expires:MM/dd/yyyy}"
    };

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
}
```

- [ ] **Step 3: Build**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```
Expected: 0 errors.

- [ ] **Step 4: Manual verification**

Reinstall + launch. Sign in (`admin` / `admin123`). Tap *Members* tab. Verify:

1. List shows all 6 members (Marcus, Lena, Diego, Aisha, Sam, Priya) — same as before.
2. Tap the search field, type `lena` → only *Lena Park* visible.
3. Type `a` → multiple matches visible (Marcus, Diego, Aisha, Priya).
4. Type `zzz` → list shows muted text *"No members match \"zzz\"."*
5. Clear the search field → all 6 rows return.
6. Switch to Dashboard, then back to Members → list still works, no duplicate rows.

- [ ] **Step 5: Commit**

```bash
git add Gymers/Pages/MembersPage.xaml Gymers/Pages/MembersPage.xaml.cs
git commit -m "feat(members): live search filtering with empty-state

Page now injects DataStore. SearchField text drives a re-render
of the imperative list. Zero matches show a muted notice."
```

---

## Task 4: PaymentsPage — Record Payment with validation

After this task, the *Record Payment* form validates inputs, inserts a new payment into the store, and the new row appears at the top of *Recent Payments*.

**Files:**
- Modify: `Gymers/Pages/PaymentsPage.xaml`
- Modify: `Gymers/Pages/PaymentsPage.xaml.cs`

- [ ] **Step 1: Update `PaymentsPage.xaml`**

Inside the *Record Payment* card, name the inputs and the button, and add a status label below the button. The block becomes:

```xml
<Border Style="{StaticResource Card}">
    <VerticalStackLayout Spacing="16">
        <Label Style="{StaticResource H3Card}" Text="Record Payment" />
        <c:LabeledInput x:Name="MemberInput" Label="Member"  Placeholder="Member name" />
        <c:LabeledInput x:Name="AmountInput" Label="Amount"  Placeholder="0.00" Keyboard="Numeric" />
        <c:LabeledInput x:Name="MethodInput" Label="Method"  Placeholder="Card / Cash / Bank" />
        <c:PrimaryButton x:Name="RecordButton" Text="RECORD PAYMENT" />
        <Label x:Name="StatusLabel"
               Style="{StaticResource BodySm}"
               HorizontalTextAlignment="Center"
               IsVisible="False" />
    </VerticalStackLayout>
</Border>
```

No other XAML changes.

- [ ] **Step 2: Replace `PaymentsPage.xaml.cs` body**

```csharp
using System.Globalization;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Microsoft.Maui.Controls.Shapes;

namespace Gymers.Pages;

public partial class PaymentsPage : ContentPage
{
    readonly DataStore _data;
    IDispatcherTimer? _statusTimer;

    public PaymentsPage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        RecordButton.Clicked += OnRecord;
        _data.Payments.CollectionChanged += (_, _) => Render();
        Render();
    }

    void OnRecord(object? sender, EventArgs e)
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

        var payment = _data.RecordPayment(member, amount, method);

        MemberInput.Text = "";
        AmountInput.Text = "";
        MethodInput.Text = "";
        ShowSuccess($"Recorded ${payment.Amount:0.00} · Receipt #{payment.ReceiptNumber}.");
    }

    string SuggestNames() =>
        string.Join(", ", _data.Members.Take(3).Select(m => m.Name));

    void Render()
    {
        PaymentList.Children.Clear();
        foreach (var p in _data.Payments)
        {
            var member = _data.Members.FirstOrDefault(m => m.Id == p.MemberId);
            var displayName = member?.Name ?? "Unknown member";
            PaymentList.Children.Add(new ListRow
            {
                LeadingContent = MakeAmountPill(p.Amount),
                Title          = displayName,
                Subtitle       = $"${p.Amount:0.00} · {p.Method} · Receipt #{p.ReceiptNumber}"
            });
        }
    }

    static View MakeAmountPill(decimal amount)
    {
        var pale = (Color)Application.Current!.Resources["PaleBlue"];
        var navy = (Color)Application.Current.Resources["NavyHeading"];
        return new Border
        {
            BackgroundColor = pale,
            StrokeThickness = 0,
            WidthRequest = 56,
            HeightRequest = 40,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            Content = new Label
            {
                Text = $"${(int)amount}",
                FontFamily = "ManropeBold",
                FontSize = 14,
                TextColor = navy,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
    }

    void ShowError(string text)
    {
        _statusTimer?.Stop();
        StatusLabel.Text = text;
        StatusLabel.TextColor = (Color)Application.Current!.Resources["Danger"];
        StatusLabel.IsVisible = true;
    }

    void ShowSuccess(string text)
    {
        StatusLabel.Text = text;
        StatusLabel.TextColor = (Color)Application.Current!.Resources["Olive"];
        StatusLabel.IsVisible = true;

        _statusTimer?.Stop();
        _statusTimer = Dispatcher.CreateTimer();
        _statusTimer.Interval = TimeSpan.FromSeconds(2.5);
        _statusTimer.IsRepeating = false;
        _statusTimer.Tick += (_, _) => StatusLabel.IsVisible = false;
        _statusTimer.Start();
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```
Expected: 0 errors.

- [ ] **Step 4: Manual verification**

Reinstall + launch. Sign in. Tap *Payments* tab. Verify each in turn:

1. **Existing list** still renders 5 starter payments at the top of *Recent Payments*.
2. **Empty member:** leave Member empty, tap RECORD PAYMENT → red status: *No member named "". Try Marcus Sterling, Lena Park, Diego Alvarez.*
3. **Unknown member:** Member: `Bob`, Amount: `10`, Method: `card` → red status: *No member named "Bob". …*
4. **Bad amount:** Member: `Lena Park`, Amount: `0`, Method: `card` → *Amount must be a positive number with up to 2 decimals.* Repeat with `12.345` → same.
5. **Bad method:** Member: `Lena Park`, Amount: `25`, Method: `Crypto` → *Method must be Card, Cash, or Bank.*
6. **Happy path:** Member: `Marcus Sterling`, Amount: `75.50`, Method: `card` → tap RECORD PAYMENT. Expected: a row appears at the very top of *Recent Payments* with `Marcus Sterling · $75.50 · Card · Receipt #1043`. The form fields clear. A green success status reads *Recorded $75.50 · Receipt #1043.* and disappears after ~2.5s.
7. **Method casing:** Repeat with Method `BANK` → row stored as `Bank`.
8. **Cross-tab persistence:** record a second payment, switch to Members, back to Payments — both new rows still at the top.

- [ ] **Step 5: Commit**

```bash
git add Gymers/Pages/PaymentsPage.xaml Gymers/Pages/PaymentsPage.xaml.cs
git commit -m "feat(payments): validate form & record payments into store

Member must match an existing name; Amount must parse as decimal>0
with up to 2 decimals; Method must be Card/Cash/Bank. New rows
appear at the top of Recent Payments. Status label flips between
Danger and Olive; success auto-hides after 2.5s."
```

---

## Task 5: AttendancePage — Check-In flow

After this task, typing in the *Check In* card surfaces up to 3 member suggestions; tapping one selects that member; tapping CHECK IN appends a check-in to the recent list.

**Files:**
- Modify: `Gymers/Pages/AttendancePage.xaml`
- Modify: `Gymers/Pages/AttendancePage.xaml.cs`

- [ ] **Step 1: Update `AttendancePage.xaml`**

Replace the *Check In* card's `<VerticalStackLayout>` body so it reads:

```xml
<VerticalStackLayout Spacing="16">
    <Label Style="{StaticResource H3Card}" Text="Check In" />
    <c:SearchField x:Name="MemberSearch" Placeholder="Search member by name…" />
    <VerticalStackLayout x:Name="Suggestions" Spacing="4" IsVisible="False" />
    <c:PrimaryButton x:Name="CheckInButton" Text="CHECK IN" />
    <Label x:Name="StatusLabel"
           Style="{StaticResource BodySm}"
           HorizontalTextAlignment="Center"
           IsVisible="False" />
</VerticalStackLayout>
```

No other XAML changes (the *Recent Check-ins* `VerticalStackLayout x:Name="CheckInList"` stays).

- [ ] **Step 2: Replace `AttendancePage.xaml.cs` body**

```csharp
using System.ComponentModel;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Microsoft.Maui.Controls.Shapes;

namespace Gymers.Pages;

public partial class AttendancePage : ContentPage
{
    readonly DataStore _data;
    Member? _selected;
    IDispatcherTimer? _statusTimer;

    public AttendancePage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        MemberSearch.PropertyChanged += OnSearchChanged;
        CheckInButton.Clicked += OnCheckIn;
        _data.CheckIns.CollectionChanged += (_, _) => Render();
        Render();
    }

    void OnSearchChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SearchField.Text)) return;
        var q = MemberSearch.Text?.Trim() ?? "";

        if (q.Length == 0)
        {
            Suggestions.IsVisible = false;
            Suggestions.Children.Clear();
            _selected = null;
            return;
        }

        var exact = _data.FindMemberByName(q);
        if (exact is not null)
        {
            _selected = exact;
            Suggestions.IsVisible = false;
            Suggestions.Children.Clear();
            return;
        }

        _selected = null;
        var matches = _data.SearchMembers(q).Take(3).ToList();
        Suggestions.Children.Clear();
        foreach (var m in matches) Suggestions.Children.Add(BuildSuggestion(m));
        Suggestions.IsVisible = matches.Count > 0;
    }

    View BuildSuggestion(Member m)
    {
        var label = new Label
        {
            Text = m.Name,
            Style = (Style)Application.Current!.Resources["BodyMd"]
        };
        var border = new Border
        {
            BackgroundColor = (Color)Application.Current.Resources["SurfaceMuted"],
            StrokeThickness = 0,
            Padding = new Thickness(16, 8),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Content = label
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            _selected = m;
            MemberSearch.Text = m.Name;     // re-enters OnSearchChanged → exact-match branch
            Suggestions.IsVisible = false;
        };
        border.GestureRecognizers.Add(tap);
        return border;
    }

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

    void Render()
    {
        CheckInList.Children.Clear();

        if (_data.CheckIns.Count == 0)
        {
            CheckInList.Children.Add(new Label
            {
                Text = "No check-ins yet today.",
                Style = (Style)Application.Current!.Resources["BodyMd"],
                TextColor = (Color)Application.Current.Resources["TextMuted"],
                HorizontalTextAlignment = TextAlignment.Center
            });
            return;
        }

        foreach (var c in _data.CheckIns)
        {
            var member = _data.Members.FirstOrDefault(m => m.Id == c.MemberId);
            var displayName = member?.Name ?? "Unknown member";
            CheckInList.Children.Add(new ListRow
            {
                LeadingContent = MakeStatusDot(),
                Title          = displayName,
                Subtitle       = $"Checked in · {c.At:hh\\:mm tt}"
            });
        }
    }

    static View MakeStatusDot()
    {
        var olive = (Color)Application.Current!.Resources["Olive"];
        return new Border
        {
            BackgroundColor = olive,
            StrokeThickness = 0,
            WidthRequest = 12,
            HeightRequest = 12,
            VerticalOptions = LayoutOptions.Center,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) }
        };
    }

    void ShowError(string text)
    {
        _statusTimer?.Stop();
        StatusLabel.Text = text;
        StatusLabel.TextColor = (Color)Application.Current!.Resources["Danger"];
        StatusLabel.IsVisible = true;
    }

    void ShowSuccess(string text)
    {
        StatusLabel.Text = text;
        StatusLabel.TextColor = (Color)Application.Current!.Resources["Olive"];
        StatusLabel.IsVisible = true;

        _statusTimer?.Stop();
        _statusTimer = Dispatcher.CreateTimer();
        _statusTimer.Interval = TimeSpan.FromSeconds(2.5);
        _statusTimer.IsRepeating = false;
        _statusTimer.Tick += (_, _) => StatusLabel.IsVisible = false;
        _statusTimer.Start();
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```
Expected: 0 errors.

- [ ] **Step 4: Manual verification**

Reinstall + launch. Sign in. Tap *Attendance* tab. Verify:

1. Existing list still renders 6 seed check-ins.
2. **No selection:** leave search empty, tap CHECK IN → red status: *Select a member first.*
3. **Suggestions appear:** type `l` → suggestions show *Lena Park* (and possibly others). Type `lena` → only *Lena Park* in the suggestion list.
4. **Tap suggestion:** tap *Lena Park* in the list → search field updates to "Lena Park", suggestions hide.
5. **Happy path:** tap CHECK IN → row at top of *Recent Check-ins* with *Lena Park · Checked in · {current time}*. Search field clears, green success: *Checked in Lena Park at HH:MM AM.*
6. **Exact-name selection (no tap):** type `Marcus Sterling` exactly → suggestions stay hidden, tap CHECK IN → row appears.
7. **Unknown name:** type `Bob` → suggestions show 0 rows (hidden) and tap CHECK IN → *Select a member first.*
8. **Cross-tab persistence:** record a check-in, switch to Payments, back to Attendance — new rows still on top.

- [ ] **Step 5: Commit**

```bash
git add Gymers/Pages/AttendancePage.xaml Gymers/Pages/AttendancePage.xaml.cs
git commit -m "feat(attendance): search-pick member & record check-ins

Typing surfaces up to 3 suggestion rows; tapping one or typing an
exact name selects that member. CHECK IN inserts a CheckIn into
the store; the recent list updates. Empty store shows a muted
'No check-ins yet today.' notice."
```

---

## Final Verification Walk

After Task 5 is committed, re-run the full demo script from the spec end-to-end in one launch session. This catches anything one task may have inadvertently broken in another.

- [ ] **Step 1: Clean install**

```bash
UDID=$(xcrun simctl list devices "iOS 26.2" booted | grep -Eo '\([0-9A-F-]{36}\)' | head -1 | tr -d '()')
xcrun simctl uninstall "$UDID" com.companyname.gymers
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
xcrun simctl install "$UDID" Gymers/bin/Debug/net10.0-ios/iossimulator-arm64/Gymers.app
xcrun simctl launch "$UDID" com.companyname.gymers
```

- [ ] **Step 2: Run the spec's nine-step manual demo script**

Reproduce items 1–9 from the spec's Section 8 *Manual demo script* in order. Each must pass.

- [ ] **Step 3: Capture screenshots**

For each shot, navigate the simulator to the relevant screen, then run the `simctl io` command:

```bash
# 1. LoginPage with the inline error visible (tap Sign In with empty fields first)
xcrun simctl io "$UDID" screenshot docs/status/screenshots/v2-login-error.png

# 2. Members tab with "lena" typed in the search field
xcrun simctl io "$UDID" screenshot docs/status/screenshots/v2-members-search.png

# 3. Payments tab right after recording a payment (success status visible)
xcrun simctl io "$UDID" screenshot docs/status/screenshots/v2-payments-recorded.png

# 4. Attendance tab with suggestion list visible (type "le")
xcrun simctl io "$UDID" screenshot docs/status/screenshots/v2-attendance-suggestions.png

# 5. Attendance tab right after a check-in (new top row visible)
xcrun simctl io "$UDID" screenshot docs/status/screenshots/v2-attendance-checked-in.png
```

- [ ] **Step 4: Final commit (only if new screenshots saved)**

```bash
git add docs/status/screenshots/v2-*.png
git commit -m "docs: add v2 'app comes alive' screenshots"
```

---

## Self-review notes (for the implementer)

- **DI gotcha:** if `xcrun simctl launch` crashes immediately on Task 1's smoke-test, the most likely cause is a typo in `MauiProgram.cs`'s `AddTransient<Pages.LoginPage>()` (e.g. wrong namespace). Read `xcrun simctl spawn "$UDID" log show --last 1m --predicate 'process == "Gymers"'` for stack traces.
- **`PropertyChanged` event filter:** all three pages compare `e.PropertyName == nameof(SearchField.Text)` (or `LabeledInput.Text`). If you accidentally compare against `"Text"` literally, refactors will silently break — keep `nameof()`.
- **`Style` resource lookup:** Always pull resources from `Application.Current!.Resources["…"]`, not `this.Resources`. The merged dictionaries live on the App, and using the page's own `Resources` will only find styles declared inline on the page.
- **`Insert(0, …)` ordering:** the store's seed already does `OrderByDescending(p => p.At)`, then new entries `Insert(0, …)`, so the list is always newest-first. Don't re-sort on render.
- **No tests:** verification is manual. Don't add xUnit. The spec deliberately scoped tests out.
