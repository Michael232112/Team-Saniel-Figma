# Gymers Mobile App — "Make It Real" Slice

**Spec date:** 2026-05-07
**Status:** Approved (all six sections agreed in brainstorm)
**Builds on:** `2026-05-05-figma-reskin-mobile-design.md` (v1 — 5-screen visual demo)

## 1. Goal

The v1 demo looks complete but is inert: every button is a no-op, search doesn't filter, login accepts anything. This slice makes the existing UI **functional** against an in-memory data store. Goal is a class-demo-quality walkthrough where a grader can sign in, search members, record a payment, check a member in, and watch the lists update — without adding new screens, new libraries, or new visual design.

## 2. Scope

### In scope
- A `DataStore` service that holds mutable `ObservableCollection<Member>`, `ObservableCollection<Payment>`, `ObservableCollection<CheckIn>` seeded from `SampleData`.
- DI registration; pages get the store via constructor injection.
- **LoginPage:** fixed credentials (`admin` / `admin123`, `staff` / `staff123`), role pill enforced, inline error label.
- **MembersPage:** live search filtering, empty-state label.
- **PaymentsPage:** form validates Member / Amount / Method, inserts a new payment, list updates.
- **AttendancePage:** search-pick suggestion list (max 3), check-in inserts into list.
- One new color token: `Danger = #B91C1C`.

### Out of scope (still)
- SQLite or any persistence — store is in-memory and resets on every launch. Acceptable for a class demo.
- Sign-out, session/identity model. The Dashboard does not greet the signed-in user.
- Member detail page; tapping a `ListRow` does nothing new.
- Filtering on Attendance's *Recent Check-ins* list (the search field there is part of the *Check In* card, not a list filter).
- Currency formatting on input. The `LabeledInput` accepts a numeric string; we parse with `InvariantCulture`.
- Method dropdown / autocomplete; method is a free-text field validated against `Card` / `Cash` / `Bank`.
- Member autocomplete on *PaymentsPage*. Exact-name match only (the matching error message lists the first three real names so a grader can copy one).
- Automated tests. Pure UI state; manual walkthrough verifies everything.
- Trainers, Workout Plans, Equipment, Reports tabs — not in this slice.
- Android / Mac Catalyst targets — iOS only, unchanged from v1.

## 3. Architecture

### 3.1 New file: `Gymers/Data/DataStore.cs`

Single mutable in-memory store. One instance per app, registered as a DI singleton.

```csharp
public sealed class DataStore
{
    public ObservableCollection<Member>  Members  { get; }
    public ObservableCollection<Payment> Payments { get; }   // newest first
    public ObservableCollection<CheckIn> CheckIns { get; }   // newest first

    public DataStore()
    {
        Members  = new ObservableCollection<Member>(SampleData.Members);
        Payments = new ObservableCollection<Payment>(
            SampleData.Payments.OrderByDescending(p => p.At));
        CheckIns = new ObservableCollection<CheckIn>(
            SampleData.CheckIns.OrderByDescending(c => c.At));
    }

    public Member? FindMemberByName(string name) =>
        Members.FirstOrDefault(m =>
            string.Equals(m.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Member> SearchMembers(string query) =>
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

`SampleData` stays as the immutable seed source. Pages no longer reference `SampleData` directly — they hold a `_data` field.

### 3.2 DI registration (`Gymers/MauiProgram.cs`)

Inside `CreateMauiApp`:

```csharp
builder.Services.AddSingleton<DataStore>();

builder.Services.AddTransient<Pages.LoginPage>();
builder.Services.AddTransient<Pages.DashboardPage>();
builder.Services.AddTransient<Pages.MembersPage>();
builder.Services.AddTransient<Pages.PaymentsPage>();
builder.Services.AddTransient<Pages.AttendancePage>();
```

Page transient registrations are explicit even though MAUI Shell will resolve them implicitly — explicit registration removes ambiguity and surfaces missing constructors at startup instead of at navigation.

### 3.3 Page constructor change

Every page (except Dashboard, which doesn't need data this slice) takes `DataStore` via the constructor:

```csharp
public MembersPage(DataStore data)
{
    _data = data;
    InitializeComponent();
    // …
}
```

`AppShell.xaml`'s `DataTemplate` references resolve through `IServiceProvider` automatically; no AppShell code change.

### 3.4 New token (`Resources/Styles/Colors.xaml`)

```xml
<Color x:Key="Danger">#B91C1C</Color>
```

No other color additions.

## 4. LoginPage

### Behavior

- Two fixed accounts:
  - `admin` / `admin123`
  - `staff` / `staff123`
- The Admin/Staff pill state determines which account is allowed. Username + password must match the pill.
- Empty username **or** empty password → *"Enter username and password."*
- Wrong creds for the selected role → *"Invalid credentials for the selected role."*
- Success → `Shell.Current.GoToAsync("//Dashboard")`. Login is not in nav history (matches v1).
- The "Demo: any username/password works" caption changes to `"Demo: admin / admin123  ·  staff / staff123"`.

### XAML changes (`Pages/LoginPage.xaml`)

- Add `x:Name="UsernameInput"` and `x:Name="PasswordInput"` to the two `c:LabeledInput` elements (no other property changes — `Text` is already TwoWay-bindable).
- Insert a single error label between the role pill row and the *SIGN IN* button:

```xml
<Label x:Name="ErrorLabel"
       Style="{StaticResource BodySm}"
       TextColor="{StaticResource Danger}"
       HorizontalTextAlignment="Center"
       IsVisible="False" />
```

- Update the bottom caption text.

### Code-behind (`Pages/LoginPage.xaml.cs`)

```csharp
public partial class LoginPage : ContentPage
{
    enum SelectedRole { Admin, Staff }
    SelectedRole _role = SelectedRole.Admin;

    public LoginPage() => InitializeComponent();

    void OnSelectAdmin(object? sender, TappedEventArgs e)
    {
        _role = SelectedRole.Admin;
        // existing visual swap retained
    }

    void OnSelectStaff(object? sender, TappedEventArgs e)
    {
        _role = SelectedRole.Staff;
        // existing visual swap retained
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

`LoginPage` does not take `DataStore` — auth is hardcoded.

## 5. MembersPage

### Behavior

- Typing in the search field filters the list as `Text` changes.
- Empty/whitespace query → all members.
- Zero matches → a single muted label: *No members match "{query}"*.
- The *Active Members* KPI value (`1,250`) stays hardcoded (vanity stat).

### XAML changes (`Pages/MembersPage.xaml`)

- Add `x:Name="Search"` to the existing `c:SearchField`.
- `<VerticalStackLayout x:Name="MemberList">` is unchanged.

### Code-behind (`Pages/MembersPage.xaml.cs`)

```csharp
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

    static View BuildRow(Member m) { /* lifted verbatim from current BuildMemberList */ }
    static View MakeInitialAvatar(string name) { /* lifted verbatim */ }
}
```

Imperative rebuild matches the existing pattern. No `CollectionView` refactor.

## 6. PaymentsPage

### Behavior

- *Record Payment* button validates and inserts. New row appears at the top of *Recent Payments*. Form clears. Status label below the button shows success in `Olive`, errors in `Danger`. Success auto-hides after 2.5s; errors persist until the next submit.
- The *Today's Earnings* KPI card stays hardcoded.

### Validation rules

| Field   | Rule                                                                                |
|---------|-------------------------------------------------------------------------------------|
| Member  | Trimmed name must match a `DataStore.Members` entry case-insensitively.             |
| Amount  | Parses as `decimal` with `InvariantCulture`, value > 0, ≤ 2 decimal places.         |
| Method  | After lower-casing, equals `card`, `cash`, or `bank`. Stored capitalised.           |

Error messages:
- Empty / unknown member: `No member named "{x}". Try {first 3 names from store, comma-separated}.`
- Bad amount: `Amount must be a positive number with up to 2 decimals.`
- Bad method: `Method must be Card, Cash, or Bank.`

Success message: `Recorded ${amount:0.00} · Receipt #{n}.`

### XAML changes (`Pages/PaymentsPage.xaml`)

- Name the inputs and button: `MemberInput`, `AmountInput`, `MethodInput`, `RecordButton`.
- Insert a single status label below the button:

```xml
<Label x:Name="StatusLabel"
       Style="{StaticResource BodySm}"
       HorizontalTextAlignment="Center"
       IsVisible="False" />
```

- `<VerticalStackLayout x:Name="PaymentList">` is unchanged.

### Code-behind (`Pages/PaymentsPage.xaml.cs`)

```csharp
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

        if (nameRaw.Length == 0)
        { ShowError($"No member named \"\". Try {SuggestNames()}."); return; }

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

    void Render() { /* rebuild PaymentList.Children from _data.Payments */ }

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

`Render()` lifts the existing `BuildPaymentList` logic, sourced from `_data.Payments` instead of `SampleData.Payments`.

## 7. AttendancePage

### Behavior

- The *Check In* card's `SearchField` doubles as a member picker. Typing surfaces up to **3** matching members in a suggestion list directly under the field. Tap a suggestion → field text becomes that name, suggestions hide, member is selected.
- If the user types an exact name (e.g. pasted), the page silently selects that member and hides the suggestion list.
- *Check In* button: with a selected member, calls `DataStore.RecordCheckIn`, prepends to *Recent Check-ins*, clears the field, shows success.
- With no selection, shows *"Select a member first."*
- Empty `_data.CheckIns` → list shows *"No check-ins yet today."*
- *Today's Check-Ins* KPI value (`350`) stays hardcoded.

### XAML changes (`Pages/AttendancePage.xaml`)

Inside the *Check In* card, replace the existing `<c:SearchField>` and `<c:PrimaryButton>` block with:

```xml
<c:SearchField x:Name="MemberSearch" Placeholder="Search member by name…" />

<VerticalStackLayout x:Name="Suggestions" Spacing="4" IsVisible="False" />

<c:PrimaryButton x:Name="CheckInButton" Text="CHECK IN" />

<Label x:Name="StatusLabel"
       Style="{StaticResource BodySm}"
       HorizontalTextAlignment="Center"
       IsVisible="False" />
```

`<VerticalStackLayout x:Name="CheckInList">` (the recent list) is unchanged.

### Suggestion row visual

Each tappable row inside `Suggestions`:
- `Border`, `BackgroundColor=SurfaceMuted`, `StrokeThickness=0`, `RoundRectangle CornerRadius=12`, `Padding="16,8"`.
- `Label`, `Style=BodyMd`, `Text` = member name.
- `TapGestureRecognizer.Tapped` → selects the member, sets `MemberSearch.Text`, hides `Suggestions`.

### Code-behind (`Pages/AttendancePage.xaml.cs`)

```csharp
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
        { Suggestions.IsVisible = false; Suggestions.Children.Clear(); _selected = null; return; }

        var exact = _data.FindMemberByName(q);
        if (exact is not null)
        { _selected = exact; Suggestions.IsVisible = false; Suggestions.Children.Clear(); return; }

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
            MemberSearch.Text = m.Name;        // re-enters OnSearchChanged → exact-match branch
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

    void Render() { /* rebuild CheckInList.Children from _data.CheckIns; show empty-state label if zero */ }

    void ShowError(string text)   { /* same shape as PaymentsPage */ }
    void ShowSuccess(string text) { /* same shape as PaymentsPage */ }
}
```

## 8. Verification & success criteria

### Build / runtime
1. `dotnet build Gymers/Gymers.csproj -f net10.0-ios` → 0 warnings, 0 errors.
2. App launches in iOS 26.2 simulator.
3. All 5 screens render — no visual regression vs. screenshots committed in `0155fb9`.

### Manual demo script
1. **Login fail (empty):** open app → tap *Sign In* with empty fields → *"Enter username and password."*
2. **Login wrong-role:** type `staff` / `staff123` with **Admin** pill selected → *"Invalid credentials for the selected role."*
3. **Login success:** select **Admin** pill, type `admin` / `admin123` → land on Dashboard.
4. **Members search:** Members tab → type `lena` → only Lena Park visible. Type `zzz` → empty-state label. Clear → 6 rows back.
5. **Payment happy path:** Payments tab → Member: `Marcus Sterling`, Amount: `75.50`, Method: `card` → tap *Record Payment* → row at top of *Recent Payments* shows `$75.50 · Card · Receipt #1043`; form clears; success status fades after ~2.5s.
6. **Payment validation:** try empty member, unknown member (`Bob`), amount `0`, amount `12.345`, method `Crypto` — each shows the expected inline error and the list does **not** mutate.
7. **Check-in happy path:** Attendance tab → type `lena` → suggestion row appears → tap → field shows "Lena Park", suggestions hide → tap *Check In* → row at top of *Recent Check-ins* with current timestamp.
8. **Check-in no-selection:** clear field → tap *Check In* → *"Select a member first."*
9. **Cross-tab persistence:** record a payment → switch to Members → switch back to Payments → new row still there. Same for check-ins.

### No automated tests
Pure UI/state, fast manual verification, low ROI for a class demo. Same call as v1.

### Visual fidelity
No new fonts, no layout shifts on existing screens, one new color (`Danger #B91C1C`) used only on inline error labels.

## 9. Risks

- **`ObservableCollection` mutation during rebuild.** Single-threaded UI thread on iOS makes a tap-during-render race a non-issue in practice. Not handled.
- **DI through `DataTemplate`.** Adding explicit `AddTransient<TPage>()` for every page in `MauiProgram` means a missing constructor surfaces at startup, not at first navigation.
- **Member name as the picker key.** Two members with the same name would break exact-match. Sample data has unique names; not worth coding around.
- **Status label re-use for error + success.** The single label flips colour per call. Acceptable for a class demo; consider two separate labels if validation grows past three rules per page.
