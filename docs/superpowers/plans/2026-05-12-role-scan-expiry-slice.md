# Role / Scan / Expiry Slice — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Close three README scope gaps in a single slice before tomorrow's deadline: (1) Staff-vs-Admin role differentiation persisted from login through navigation, (2) a simulated QR/ID scan check-in path on the Attendance page, and (3) a dashboard banner alerting Admin to members whose memberships are expiring soon.

**Architecture:** A new `Session` singleton (Services/Session.cs) holds the authenticated role and username; LoginPage writes to it before navigation; pages and controls read from it at construction time. `BottomTabBar` reflows from a 5-column grid (Admin) to a 4-column grid (Staff — Reports hidden). `DashboardPage` hides the Monthly Earnings KPI for Staff and shows a `Staff` badge in the trailing position of the top app bar. A new `ScanOverlay` Border on `AttendancePage` mocks an ID-scan viewfinder, resolves to a detected member after a short delay, and confirms via the existing `RecordCheckInAsync`. A new tappable `ExpirySoonBanner` Border at the top of the Dashboard ScrollView lists members whose `Status == "Expiring Soon"`; tap navigates to `//Members`.

**Tech Stack:** .NET MAUI 10 (net10.0-ios, net10.0-maccatalyst), imperative code-behind, no MVVM.

**Verification path:** Mac Catalyst smoke test per the project's MAUI memory (green build does not catch static-cctor crashes).

---

## File Structure

**Create:**
- `Gymers/Services/Session.cs` — singleton role+username holder with static `Current` accessor.

**Modify:**
- `Gymers/MauiProgram.cs` — register `Session` singleton, ensure DI resolves the same static instance.
- `Gymers/Pages/LoginPage.xaml.cs` — call `Session.Current.SignIn(...)` before `Shell.Current.GoToAsync("//Dashboard")`.
- `Gymers/Controls/BottomTabBar.xaml.cs` — read `Session.Current.IsAdmin`; reflow grid to 4 columns when Staff; hide ReportsPill.
- `Gymers/Pages/DashboardPage.xaml` — give the Monthly Earnings KPI an `x:Name="MonthlyEarningsKpi"`, prepend the `ExpirySoonBanner` Border at the top of the ScrollView VerticalStackLayout, add a trailing `RoleBadge` Label in the TopAppBar (or as a sibling).
- `Gymers/Pages/DashboardPage.xaml.cs` — hide `MonthlyEarningsKpi` for Staff; populate `ExpirySoonBanner` from `DataStore.GetExpiringSoonMembers()`; wire banner tap → `//Members`; set role badge text.
- `Gymers/Data/DataStore.cs` — add `IEnumerable<Member> GetExpiringSoonMembers()` returning members whose `Status` equals `"Expiring Soon"` (case-insensitive).
- `Gymers/Pages/AttendancePage.xaml` — add `ScanButton` PrimaryButton above the search field; add a fullscreen `ScanOverlay` Border (Grid.RowSpan covering the page) with viewfinder mock + detected-member card + Confirm/Cancel buttons.
- `Gymers/Pages/AttendancePage.xaml.cs` — wire `ScanButton.Clicked` to open the overlay, run a brief "Scanning..." state via `IDispatcherTimer`, then resolve to a deterministic next-member, wire Confirm → `RecordCheckInAsync` + close overlay + success toast, wire Cancel → close overlay.

**Status doc (after smoke-test):**
- `docs/status/screenshots/10-staff-dashboard.png` — Staff dashboard (4 tabs, no Monthly Earnings, Staff badge, expiry banner visible).
- `docs/status/screenshots/11-scan-overlay.png` — Attendance with the simulated scan overlay open mid-scan or post-detection.
- `docs/status/build_status_docx.py` — append three completed rows (`Role-based access control`, `Member ID scan check-in`, `Membership expiry alerts`) and update the summary paragraph + placeholders.
- `docs/status/gymers-mobile-app-status-update.html` — mirror those changes.

---

## Task 1: Session service

**Files:**
- Create: `Gymers/Services/Session.cs`
- Modify: `Gymers/MauiProgram.cs`

- [ ] **Step 1: Create the service**

```csharp
namespace Gymers.Services;

public sealed class Session
{
    public static Session Current { get; } = new();

    public bool   IsAdmin  { get; private set; } = true;
    public string Username { get; private set; } = "";
    public string RoleLabel => IsAdmin ? "Admin" : "Staff";

    public void SignIn(string username, bool isAdmin)
    {
        Username = username;
        IsAdmin  = isAdmin;
    }
}
```

Default `IsAdmin = true` so any callers that resolve `Session` before a sign-in (defensive case; the app launches at Login so this should be unreachable) render the full Admin UI.

- [ ] **Step 2: Register in DI**

In `Gymers/MauiProgram.cs`, after `builder.Services.AddSingleton<ReportService>();`, add:

```csharp
		builder.Services.AddSingleton<Session>(_ => Session.Current);
```

This makes DI resolve to the same static instance, so XAML controls reading `Session.Current` and DI-injected pages share state.

- [ ] **Step 3: Build clean**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -nologo -clp:ErrorsOnly`

---

## Task 2: Wire LoginPage to Session

**Files:** Modify `Gymers/Pages/LoginPage.xaml.cs`

- [ ] **Step 1: Set session before navigation**

Change the body of `OnSignIn` so that after the `ok` check succeeds (line 46+), it calls `Session.Current.SignIn(...)` before navigating:

```csharp
        ErrorLabel.IsVisible = false;
        Services.Session.Current.SignIn(u, _role == SelectedRole.Admin);
        await Shell.Current.GoToAsync("//Dashboard");
```

- [ ] **Step 2: Build clean**

---

## Task 3: Role-gate BottomTabBar

**Files:** Modify `Gymers/Controls/BottomTabBar.xaml.cs`

- [ ] **Step 1: Inject role reflow into the constructor**

Add a private `ApplyRole()` method, call it from the constructor before `ApplyActive()`:

```csharp
    public BottomTabBar()
    {
        InitializeComponent();
        ApplyRole();
        ApplyActive();
    }

    void ApplyRole()
    {
        if (Services.Session.Current.IsAdmin) return;

        // Staff layout: Dashboard / Members / Payments / Attendance (no Reports)
        var grid = (Grid)((Border)Content).Content;
        grid.ColumnDefinitions.Clear();
        for (int i = 0; i < 4; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        Grid.SetColumn(DashboardPill,  0);
        Grid.SetColumn(MembersPill,    1);
        Grid.SetColumn(PaymentsPill,   2);
        Grid.SetColumn(AttendancePill, 3);

        ReportsPill.IsVisible = false;
    }
```

Rationale: Per the README, Staff handles payment processing (walk-ins, receipts), so Payments tab stays visible for Staff. Reports + analytics are Admin-only.

- [ ] **Step 2: Build clean**

---

## Task 4: Dashboard role gating

**Files:**
- Modify: `Gymers/Pages/DashboardPage.xaml`
- Modify: `Gymers/Pages/DashboardPage.xaml.cs`

- [ ] **Step 1: Name the Monthly Earnings KPI**

In `DashboardPage.xaml`, on the third KPI card (`Label="Monthly Earnings"` at line 29), add `x:Name="MonthlyEarningsKpi"`.

- [ ] **Step 2: Add a role badge in the top app bar**

The existing `TopAppBar` only takes a `Title` + `TrailingIconGlyph`. To avoid changing the control surface, add a small overlay Label in the Dashboard XAML positioned at the same row as the TopAppBar, e.g. by wrapping the TopAppBar in a Grid with the role badge as a second column. Simpler: insert a small Label *under* the top app bar (still Grid.Row=0) showing `Signed in as Admin` / `Signed in as Staff` in muted color. Concretely, replace the existing single-element Grid.Row=0:

```xml
        <Grid Grid.Row="0" RowDefinitions="Auto,Auto">
            <c:TopAppBar Grid.Row="0" Title="Dashboard"
                         TrailingIconGlyph="{x:Static c:Icons.Bell}" />
            <Label x:Name="RoleBadge"
                   Grid.Row="1"
                   Style="{StaticResource BodySm}"
                   TextColor="{StaticResource TextMuted}"
                   HorizontalTextAlignment="End"
                   Padding="24,0,24,8"
                   Text="" />
        </Grid>
```

- [ ] **Step 3: Hide Monthly Earnings + populate role badge in code-behind**

In `DashboardPage.xaml.cs`, in the constructor (before `BuildClassList()`), add:

```csharp
        var session = Services.Session.Current;
        RoleBadge.Text = $"Signed in as {session.RoleLabel}";
        MonthlyEarningsKpi.IsVisible = session.IsAdmin;
```

- [ ] **Step 4: Build clean**

---

## Task 5: Smoke-test role differentiation + commit

- [ ] **Step 1: Launch on Mac Catalyst**

```
dotnet build Gymers/Gymers.csproj -t:Run -f net10.0-maccatalyst
```

- [ ] **Step 2: Verify Admin flow**
- Login as `admin / admin123` (Admin pill selected).
- Dashboard loads.
- Top bar shows `Signed in as Admin` under the title.
- All three KPI cards visible (Total Members, Today's Attendance, Monthly Earnings).
- BottomTabBar shows 5 pills (Dashboard, Members, Payments, Attendance, Reports).

- [ ] **Step 3: Verify Staff flow**
- Re-launch app (or sign out — since we don't have a logout button, force-quit + relaunch is the verification path).
- Login as `staff / staff123` (tap Staff pill first).
- Dashboard loads.
- Top bar shows `Signed in as Staff`.
- Two KPI cards visible (Total Members, Today's Attendance) — Monthly Earnings is hidden.
- BottomTabBar shows 4 pills (Dashboard, Members, Payments, Attendance), evenly spaced; Reports pill is gone.
- Tap Payments → opens Payments page (Staff can process walk-ins).
- Tap Dashboard / Members / Attendance → all work.

- [ ] **Step 4: Commit**

```bash
git add Gymers/Services/Session.cs Gymers/MauiProgram.cs \
        Gymers/Pages/LoginPage.xaml.cs \
        Gymers/Controls/BottomTabBar.xaml.cs \
        Gymers/Pages/DashboardPage.xaml Gymers/Pages/DashboardPage.xaml.cs
git commit -m "feat(auth): persist role from login and gate admin-only UI"
```

---

## Task 6: Simulated QR/ID scan

**Files:**
- Modify: `Gymers/Pages/AttendancePage.xaml`
- Modify: `Gymers/Pages/AttendancePage.xaml.cs`

- [ ] **Step 1: Add SCAN MEMBER ID button + scan overlay in XAML**

Insert above the existing "Check In" card a new `PrimaryButton` named `ScanButton`. Then wrap the outer Grid contents with an additional Border layer positioned via `Grid.RowSpan="3"` as the scan overlay. Concrete diff:

Inside the existing card (after `Label "Check In"`, before `SearchField`), add a secondary header:

```xml
                        <c:PrimaryButton x:Name="ScanButton" Text="SCAN MEMBER ID" />
                        <Label Style="{StaticResource BodySm}"
                               TextColor="{StaticResource TextMuted}"
                               HorizontalTextAlignment="Center"
                               Text="or search by name" />
```

(That gives staff two visible check-in paths — scan and name search — matching the README's "Member Check-In: scanning QR codes or IDs" + "Member Search" capabilities.)

Then, at the **end** of the outer Grid (after `<c:BottomTabBar Grid.Row="2" ActiveTab="Attendance" />`), insert the overlay (covers all three rows):

```xml
        <Border x:Name="ScanOverlay"
                Grid.Row="0" Grid.RowSpan="3"
                BackgroundColor="#CC0F1A2A"
                IsVisible="False"
                StrokeThickness="0">
            <VerticalStackLayout VerticalOptions="Center" Padding="32" Spacing="24">

                <!-- Mock viewfinder -->
                <Border WidthRequest="240" HeightRequest="240"
                        HorizontalOptions="Center"
                        BackgroundColor="Transparent"
                        Stroke="White"
                        StrokeThickness="3">
                    <Border.StrokeShape>
                        <RoundRectangle CornerRadius="24" />
                    </Border.StrokeShape>
                    <Label x:Name="ScanState"
                           Text="SCANNING…"
                           FontFamily="{StaticResource FontManropeBold}"
                           FontSize="20"
                           TextColor="White"
                           HorizontalTextAlignment="Center"
                           VerticalTextAlignment="Center" />
                </Border>

                <!-- Result card (hidden until resolved) -->
                <Border x:Name="ScanResultCard"
                        Style="{StaticResource Card}"
                        IsVisible="False">
                    <VerticalStackLayout Spacing="16">
                        <Label Style="{StaticResource BodySm}"
                               TextColor="{StaticResource TextMuted}"
                               Text="Detected member" />
                        <Label x:Name="ScanResultName"
                               Style="{StaticResource H3Card}"
                               Text="—" />
                        <Label x:Name="ScanResultMeta"
                               Style="{StaticResource BodyMd}"
                               TextColor="{StaticResource TextMuted}"
                               Text="" />
                        <c:PrimaryButton x:Name="ScanConfirmButton" Text="CONFIRM CHECK-IN" />
                    </VerticalStackLayout>
                </Border>

                <c:SecondaryButton x:Name="ScanCancelButton" Text="CANCEL" />
            </VerticalStackLayout>
        </Border>
```

- [ ] **Step 2: Wire the code-behind**

In `AttendancePage.xaml.cs`, in the constructor after `CheckInButton.Clicked += OnCheckIn;`, add:

```csharp
        ScanButton.Clicked         += OnScanTapped;
        ScanConfirmButton.Clicked  += OnScanConfirmed;
        ScanCancelButton.Clicked   += (_, _) => CloseScanOverlay();
```

Add an instance field `Member? _scanCandidate;` near `_selected`. Then add the methods:

```csharp
    void OnScanTapped(object? sender, EventArgs e)
    {
        if (_data.Members.Count == 0) { ShowError("No members to scan."); return; }

        ScanResultCard.IsVisible = false;
        ScanState.Text           = "SCANNING…";
        ScanOverlay.IsVisible    = true;

        var t = Dispatcher.CreateTimer();
        t.Interval    = TimeSpan.FromSeconds(1.2);
        t.IsRepeating = false;
        t.Tick += (_, _) => ResolveScan();
        t.Start();
    }

    void ResolveScan()
    {
        // Deterministic "scan": pick the next member who hasn't checked in yet today,
        // falling back to the first member if everyone has.
        var checkedInIds = _data.CheckIns
            .Where(c => c.At.Date == DateTime.Now.Date)
            .Select(c => c.MemberId)
            .ToHashSet();

        _scanCandidate =
            _data.Members.FirstOrDefault(m => !checkedInIds.Contains(m.Id))
            ?? _data.Members.First();

        ScanResultName.Text      = _scanCandidate.Name;
        ScanResultMeta.Text      = $"{_scanCandidate.Tier} · ID {_scanCandidate.Id}";
        ScanState.Text           = "CAPTURED";
        ScanResultCard.IsVisible = true;
    }

    async void OnScanConfirmed(object? sender, EventArgs e)
    {
        if (_scanCandidate is null) return;
        var m = _scanCandidate;
        var c = await _data.RecordCheckInAsync(m);
        CloseScanOverlay();
        ShowSuccess($"Scanned & checked in {m.Name} at {c.At:hh\\:mm tt}.");
    }

    void CloseScanOverlay()
    {
        ScanOverlay.IsVisible    = false;
        ScanResultCard.IsVisible = false;
        _scanCandidate           = null;
    }
```

(`Tier` is `MembershipTier` enum — its `ToString()` renders e.g. "Premium", "Elite", "Basic", which reads cleanly in the mock detected-card.)

- [ ] **Step 3: Build clean**

- [ ] **Step 4: Smoke-test**

- Launch app, login as Admin or Staff, navigate to Attendance.
- Tap SCAN MEMBER ID → dark overlay with `[ SCANNING… ]` viewfinder appears.
- After ~1.2s the viewfinder reads `CAPTURED`; below it a card shows a detected member name + tier + id, and a `CONFIRM CHECK-IN` button.
- Tap CONFIRM → overlay closes; green status toast "Scanned & checked in <Name> at HH:MM"; member appears at top of Recent Check-ins.
- Tap SCAN MEMBER ID again → next member who hasn't checked in (or wraps to first) is detected.
- Tap CANCEL during scan or after detection → overlay closes; no check-in recorded.

- [ ] **Step 5: Commit**

```bash
git add Gymers/Pages/AttendancePage.xaml Gymers/Pages/AttendancePage.xaml.cs
git commit -m "feat(attendance): simulated QR/ID scan with viewfinder overlay"
```

---

## Task 7: Expiring soon banner

**Files:**
- Modify: `Gymers/Data/DataStore.cs`
- Modify: `Gymers/Pages/DashboardPage.xaml`
- Modify: `Gymers/Pages/DashboardPage.xaml.cs`

- [ ] **Step 1: Add `GetExpiringSoonMembers()` to DataStore**

Append to `DataStore` after `RecordCheckInAsync`:

```csharp
    public IEnumerable<Member> GetExpiringSoonMembers() =>
        Members.Where(m =>
            string.Equals(m.Status, "Expiring Soon", StringComparison.OrdinalIgnoreCase));
```

- [ ] **Step 2: Prepend the banner in DashboardPage.xaml**

Inside the `ScrollView` → `VerticalStackLayout` (line 15), as the very first child *before* the Total Members KPI, insert:

```xml
                <Border x:Name="ExpirySoonBanner"
                        IsVisible="False"
                        BackgroundColor="#FFF4D5"
                        StrokeThickness="0"
                        Padding="16">
                    <Border.StrokeShape>
                        <RoundRectangle CornerRadius="16" />
                    </Border.StrokeShape>
                    <VerticalStackLayout Spacing="4">
                        <Label x:Name="ExpirySoonTitle"
                               FontFamily="{StaticResource FontManropeBold}"
                               FontSize="16"
                               TextColor="{StaticResource NavyHeading}"
                               Text="" />
                        <Label x:Name="ExpirySoonBody"
                               Style="{StaticResource BodyMd}"
                               TextColor="{StaticResource NavyDeep}"
                               Text="" />
                    </VerticalStackLayout>
                    <Border.GestureRecognizers>
                        <TapGestureRecognizer Tapped="OnExpirySoonTapped" />
                    </Border.GestureRecognizers>
                </Border>
```

- [ ] **Step 3: Populate the banner in code-behind**

In `DashboardPage.xaml.cs` constructor, after the role-gate block from Task 4, add:

```csharp
        ApplyExpirySoonBanner();
```

Append the method:

```csharp
    void ApplyExpirySoonBanner()
    {
        var expiring = _data.GetExpiringSoonMembers().ToList();
        if (expiring.Count == 0)
        {
            ExpirySoonBanner.IsVisible = false;
            return;
        }

        ExpirySoonBanner.IsVisible = true;
        ExpirySoonTitle.Text = expiring.Count == 1
            ? "1 membership expiring soon"
            : $"{expiring.Count} memberships expiring soon";
        ExpirySoonBody.Text  = "Tap to review: " + string.Join(", ", expiring.Select(m => m.Name));
    }

    async void OnExpirySoonTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync("//Members");
```

- [ ] **Step 4: Build clean**

- [ ] **Step 5: Smoke-test**

- Launch as Admin → Dashboard shows yellow banner at top: `1 membership expiring soon` + `Tap to review: Sam Chen`.
- Tap banner → navigates to Members page.
- Launch as Staff → banner still appears (operational alert, not financial).
- If sample data has 0 expiring members, banner is hidden (`IsVisible=False`).

- [ ] **Step 6: Commit**

```bash
git add Gymers/Data/DataStore.cs Gymers/Pages/DashboardPage.xaml Gymers/Pages/DashboardPage.xaml.cs
git commit -m "feat(dashboard): expiring soon banner with member roll-up"
```

---

## Task 8: Status doc + final commit

**Files:**
- Create: `docs/status/screenshots/10-staff-dashboard.png`
- Create: `docs/status/screenshots/11-scan-overlay.png`
- Modify: `docs/status/build_status_docx.py`
- Modify: `docs/status/gymers-mobile-app-status-update.html`

- [ ] **Step 1: Capture screenshots**

With the running app on Mac Catalyst:
1. Sign in as Staff → capture the Dashboard with 4-tab bar, no Monthly Earnings, Staff badge, expiry banner visible → save as `docs/status/screenshots/10-staff-dashboard.png`.
2. Sign in as Admin, navigate to Attendance, tap SCAN MEMBER ID, wait for the CAPTURED state → screenshot the overlay with the detected member card → save as `docs/status/screenshots/11-scan-overlay.png`.

- [ ] **Step 2: Append rows in `build_status_docx.py`**

Inside `completed_rows`, after the existing Equipment row, append:

```python
        ["Role-based access control",
         "Completed",
         "Login persists the selected role through a Session singleton; the bottom tab bar reflows from five tabs (Admin) to four (Staff) hiding Reports; the Dashboard suppresses the Monthly Earnings KPI for Staff and displays a 'Signed in as <Role>' badge under the top bar. Staff retains Payments per README scope (walk-ins, receipts)."],
        ["Member ID scan check-in",
         "Completed",
         "Attendance now exposes a SCAN MEMBER ID primary path alongside name search. Tapping it opens a viewfinder overlay that resolves to the next member who hasn't checked in today; CONFIRM CHECK-IN inserts a CheckIn via the existing RecordCheckInAsync, the overlay dismisses, and a success toast confirms the entry. Simulates the README's 'QR code or ID scanning' attendance path on hardware where a real camera workflow is out of scope."],
        ["Membership expiry alerts",
         "Completed",
         "The Dashboard prepends a yellow alert banner listing members whose Status is 'Expiring Soon' (Sam Chen in the seed); tap navigates to the Members screen for follow-up. The banner is hidden when no members are expiring, surfacing only when action is required."],
```

Also append placeholders after the existing Screenshot 10 (Login Error State):

```python
        placeholder("Screenshot 11: Staff Dashboard",
                    "screenshots/10-staff-dashboard.png — Dashboard signed in as Staff: 4-pill bottom tab bar (no Reports), 'Signed in as Staff' badge, no Monthly Earnings KPI, expiry banner visible at top."),
        placeholder("Screenshot 12: Member ID Scan Overlay",
                    "screenshots/11-scan-overlay.png — Attendance screen with the SCAN MEMBER ID overlay in its captured state — viewfinder mock + detected-member card with the CONFIRM CHECK-IN button."),
```

Update the "Ongoing Tasks" / summary paragraph language to reflect that role-based access + scanning + expiry alerts are now shipped (these were previously listed as gaps).

- [ ] **Step 3: Mirror in the HTML status doc**

In `docs/status/gymers-mobile-app-status-update.html`:
- Append three completed-feature rows matching the Python block.
- Insert two new `<h3>` + `<img>` blocks for screenshots 11 and 12 after the existing Login Error State block (renumbering if needed).
- Update the summary paragraph if it claimed gaps in role / scan / expiry.

- [ ] **Step 4: Regenerate .docx locally (sanity check)**

```
python3 docs/status/build_status_docx.py
```

(Output is gitignored.)

- [ ] **Step 5: Commit**

```bash
git add docs/status/screenshots/10-staff-dashboard.png \
        docs/status/screenshots/11-scan-overlay.png \
        docs/status/build_status_docx.py \
        docs/status/gymers-mobile-app-status-update.html
git commit -m "docs(status): mark role + scan + expiry alerts as completed + add screenshots"
```

---

## Self-Review

| Concern | Addressed by |
|---|---|
| Role persists across navigation | Static `Session.Current` + DI singleton (Task 1, 2) |
| Tab bar reflows cleanly for Staff | `ApplyRole()` programmatic column rebuild (Task 3) |
| Admin-only financial view hidden for Staff | `MonthlyEarningsKpi.IsVisible = session.IsAdmin` (Task 4) |
| Staff can still process payments | Payments pill remains; only Reports is hidden (Task 3 rationale) |
| Scan flow uses existing check-in pipeline | `RecordCheckInAsync(m)` in `OnScanConfirmed` (Task 6) |
| Scan flow degrades if no members | Early `ShowError` guard (Task 6) |
| Scan doesn't repeat the same member back-to-back | "Next not-yet-checked-in member" selector (Task 6) |
| Expiry banner stays hidden when empty | `IsVisible = expiring.Count > 0` (Task 7) |
| Smoke-test memory respected (Mac Catalyst launch) | Task 5, 6.4, 7.5 all run the app, not just build (project memory) |
| Commit-direct-to-main pattern | Each task commits to main; no branches (project memory) |
