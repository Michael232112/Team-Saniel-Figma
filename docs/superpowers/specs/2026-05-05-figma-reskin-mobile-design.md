# Gymers Mobile App — Figma-Inspired iOS Redesign

**Spec date:** 2026-05-05
**Status:** Approved (all sections agreed in brainstorm)
**Source design:** Figma file `oHbkZ84PO1aKzx3ExDeXRI` (Untitled), 2 reference frames — *Create Account* (`1:170`) and *Dashboard* (`9:83`).

## 1. Goal

Build a 5-screen iOS demo of the **Gymers** admin/staff gym-management app using the Figma file as the **visual reference**: typography, color palette, card system, and layout language. The current MAUI prototype is empty scaffolding (verified during brainstorm) — this spec treats v1 as a fresh build using that scaffold, not a reskin.

## 2. Scope

### In scope (v1)
- 5 screens: **Login**, **Dashboard**, **Members**, **Payments**, **Attendance**.
- Audience: **Admin/Staff only**. Member-facing flows (self-signup, paid tiers, member dashboard) are not built; the Figma's *Create Account* frame is used only as a source of design tokens, not as a screen we ship.
- Data: **Hardcoded sample data** in `Data/SampleData.cs`. No persistence.
- Platform target: **iOS only**, `net10.0-ios`. Tested in iOS 26.2 simulator on macOS.
- Pixel-faithful match to the Figma's color palette, type system, spacing, and corner-radius / shadow conventions.

### Out of scope (explicitly NOT in v1)
- SQLite or any persistence layer.
- Sign-out / session management.
- Form validation, duplicate checks, role permissions.
- Receipt PDF generation.
- Reports export.
- Android target (the project compiles for Android but is untested).
- Mac Catalyst (de-targeted from `Gymers.csproj`).
- Real Settings / Classes tab (the Figma's tab bar shows them; we replace with Payments / Attendance).
- Automated tests.

## 3. Architecture

### Build target
- `Gymers.csproj` `<TargetFramework>` switches from `net10.0-maccatalyst` to `net10.0-ios`.
- `<SupportedOSPlatformVersion>` is `iOS 17.0` minimum (compatible with iOS 26.2 simulator).
- Existing `Platforms/MacCatalyst/`, `Platforms/Android/`, `Platforms/Windows/` folders stay on disk but are not built.

### App entry
- `App.xaml` keeps its `MergedDictionaries` pattern. Adds `Typography.xaml`, `Spacing.xaml`, `Shadows.xaml` to the merged list alongside the existing `Colors.xaml` + `Styles.xaml`.

### Folder layout (under `Gymers/`)
```
Gymers/
├── Resources/
│   ├── Styles/
│   │   ├── Colors.xaml          (rewritten — Figma tokens)
│   │   ├── Typography.xaml      (new)
│   │   ├── Spacing.xaml         (new)
│   │   ├── Shadows.xaml         (new)
│   │   └── Styles.xaml          (rewritten — Card, Pill, Entry, Button styles)
│   ├── Fonts/
│   │   ├── Manrope-Bold.ttf         (new — Google Fonts, OFL)
│   │   ├── Manrope-ExtraBold.ttf    (new)
│   │   ├── Manrope-SemiBold.ttf     (new)
│   │   ├── Inter-Regular.ttf        (new)
│   │   ├── Inter-Medium.ttf         (new)
│   │   ├── Inter-SemiBold.ttf       (new)
│   │   └── Lucide.ttf               (new — icon font, OFL)
│   │   (OpenSans-*.ttf removed)
│   └── Images/
│       ├── admin_avatar.png         (new — from Figma export)
│       └── coach_marcus.png         (new — from Figma export)
├── Controls/
│   ├── Icons.cs                  (Lucide glyph constants)
│   ├── KpiCard.xaml + .cs
│   ├── DeltaChip.xaml + .cs
│   ├── TopAppBar.xaml + .cs
│   ├── BottomTabBar.xaml + .cs
│   ├── LabeledInput.xaml + .cs
│   ├── PrimaryButton.xaml + .cs
│   ├── SecondaryButton.xaml + .cs
│   ├── SearchField.xaml + .cs
│   └── ListRow.xaml + .cs
├── Pages/
│   ├── LoginPage.xaml + .cs
│   ├── DashboardPage.xaml + .cs
│   ├── MembersPage.xaml + .cs
│   ├── PaymentsPage.xaml + .cs
│   └── AttendancePage.xaml + .cs
├── Models/
│   ├── Member.cs
│   ├── Payment.cs
│   ├── CheckIn.cs
│   ├── ClassSession.cs
│   └── MembershipTier.cs        (enum: Basic, Premium, Elite)
├── Data/
│   └── SampleData.cs            (static seed data)
├── App.xaml + .cs               (existing — merged dictionaries updated)
├── AppShell.xaml + .cs          (rewritten — see Navigation)
├── MauiProgram.cs               (existing — register fonts)
└── Gymers.csproj                (target framework changed)
```

`MainPage.xaml` and `MainPage.xaml.cs` are deleted.

## 4. Design Tokens

### Colors (`Resources/Styles/Colors.xaml`)

| Token             | Hex / value                  | Usage                                            |
|-------------------|------------------------------|--------------------------------------------------|
| `BgApp`           | `#F9F9F9`                    | App / page background                            |
| `Surface`         | `#FFFFFF`                    | Card background                                  |
| `SurfaceMuted`    | `#F4F3F3`                    | List-item bg, progress-bar track                 |
| `BorderSoft`      | `rgba(196,198,210,0.10)`     | Hairline dividers                                |
| `NavyDeep`        | `#002159`                    | Featured card bg, large stat, primary button     |
| `NavyMid`         | `#10367D`                    | Gradient mid-stop, date-pill text                |
| `NavyHeading`     | `#1E3A8A`                    | Page title                                       |
| `Periwinkle`      | `#B1C5FF`                    | Text on `NavyDeep` (label, caption)              |
| `PeriwinkleLight` | `#DAE2FF`                    | Button text on navy                              |
| `PaleBlue`        | `#DBEAFE`                    | Active tab pill bg                               |
| `Lime`            | `#C7F339`                    | Accent — delta chip on navy, class chevron       |
| `LimeSoft`        | `rgba(199,243,57,0.30)`      | Delta chip on white                              |
| `Olive`           | `#516600`                    | Delta-chip text, gradient end, "Peak Hour" label |
| `OliveDark`       | `#161E00`                    | Delta-chip text on solid Lime                    |
| `TextPrimary`     | `#1A1C1C`                    | Body headings, KPI numbers                       |
| `TextSecondary`   | `#444651`                    | Subtext, labels, body copy                       |
| `TextMuted`       | `#64748B`                    | Inactive tab label                               |

### Typography (`Resources/Styles/Typography.xaml`)

Two font families: **Manrope** (headings, big numbers) and **Inter** (body, labels, captions). Both downloaded from Google Fonts (OFL license).

| Style         | Font / weight        | Size / line / tracking | Notes                              |
|---------------|----------------------|------------------------|------------------------------------|
| `DisplayKpi`  | Manrope ExtraBold    | 48 / 48 / 0            | Hero KPI numbers                   |
| `H1Page`      | Manrope Bold         | 30 / 36 / −0.75        | Page title                         |
| `H2Section`   | Manrope Bold         | 24 / 32 / 0            | Section headings                   |
| `H3Card`      | Manrope Bold         | 20 / 28 / 0            | Card heading                       |
| `H4Item`      | Manrope Bold         | 16 / 24 / 0            | List item title                    |
| `StatLg`      | Manrope Bold         | 18 / 28 / 0            | Zone numeric values                |
| `BodyMd`      | Inter Regular        | 14 / 20 / 0            | Body                               |
| `BodySm`      | Inter Regular        | 12 / 16 / 0            | Tertiary text                      |
| `LabelKpi`    | Inter SemiBold       | 12 / 16 / 0.6          | UPPERCASE — KPI labels             |
| `LabelZone`   | Inter SemiBold       | 10 / 15 / 1.0          | UPPERCASE — zone labels            |
| `LabelTab`    | Inter Medium         | 10 / 15 / 0.5          | UPPERCASE — bottom-tab labels      |
| `ButtonLg`    | Inter SemiBold       | 14 / 20 / 0.35         | Primary button text                |
| `Caption`     | Inter Medium         | 12 / 16 / 0            | Class metadata, etc.               |

### Spacing scale (`Resources/Styles/Spacing.xaml`)
8-pt rhythm: `Sp1=4`, `Sp2=8`, `Sp3=12`, `Sp4=16`, `Sp6=24`, `Sp8=32`, `Sp12=48`.

### Radii
`RadiusChip = 8`, `RadiusCard = 24`, `RadiusPill = 9999` (treated as half-height in XAML — `CornerRadius="999"` on a 32-pt control gives a circle-end pill).

### Shadow (`Resources/Styles/Shadows.xaml`)
Single elevation `Card`: `Offset (0, 10)`, `Radius 30`, `Brush #1A1C1C`, `Opacity 0.06`. Realized via `Shadow` on the card `Border`.

### Gradient
`LiveCapacityGradient`: linear, 172.6° (top-left → bottom-right), stop 0 = `NavyMid #10367D`, stop 1 = `Olive #516600`. Used on the Live Capacity progress fill, and on the Primary Button.

## 5. Component Vocabulary

All XAML UserControls under `Gymers/Controls/`. One file per control, bindable properties only.

### Atoms (styles, no UserControl)
- **`Card` style** on `Border` — `Surface` bg, `RadiusCard`, `Card` shadow, `Padding=Sp8`.
- **`CardMuted` style** on `Border` — `SurfaceMuted` bg, no shadow, `Padding=Sp4`.
- **`Pill` style** on `Border` — `RadiusPill`, bg overridable.

### Components
| Control          | Bindable props                                                                  | Behavior                                                                                                                                                                                                                                                          |
|------------------|---------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `KpiCard`        | `Label`, `Value`, `DeltaText`, `DeltaDirection` (Up/Down), `Caption`, `Variant` (Light/Dark), `TrailingIconGlyph` | Light = white card, `TextPrimary` value, `LimeSoft` delta. Dark = `NavyDeep` bg, white value, `Periwinkle` label/caption, solid `Lime` delta. Layout: label row (label + trailing icon) → big value → delta chip + caption. |
| `DeltaChip`      | `Text`, `Direction`, `OnDarkSurface` (bool)                                     | Pill shape with up/down arrow + percentage. On white: `LimeSoft` bg, `Olive` text. On navy: solid `Lime` bg, `OliveDark` text. Padding `Sp2 Sp1` (8px × 2px), `RadiusChip`.                                                                                          |
| `TopAppBar`      | `Title`, `ShowAvatar`, `AvatarSource`, `TrailingIconGlyph`, `TrailingCommand`   | 64-pt height. White at 95% alpha. Layout: avatar circle (40-pt) + `H1Page` title + 40-pt icon button. Bottom edge has subtle shadow `0 10 30 -5 / 6%`.                                                                                                              |
| `BottomTabBar`   | `ActiveTab` (enum)                                                              | 4-tab pill nav. Translucent white bg. Each tab: icon glyph + uppercase label. Active = `PaleBlue` bg pill, `NavyHeading` glyph/text. Inactive = `TextMuted`. Tab tap → `Shell.Current.GoToAsync("//<route>")`.                                                       |
| `LabeledInput`   | `Label`, `Placeholder`, `Text` (TwoWay), `IsPassword`, `Keyboard`               | Stack: `BodyMd` label (`TextSecondary`) + `Entry` inside `SurfaceMuted` rounded container, 56-pt tall, `Sp4` horizontal padding, `RadiusChip` × 2 (16) corners.                                                                                                     |
| `PrimaryButton`  | `Text`, `Command`, `IsEnabled`                                                  | 44-pt tall, gradient `NavyDeep → NavyMid` fill, `RadiusChip`, `PeriwinkleLight` text in `ButtonLg` style. Disabled = 40% opacity.                                                                                                                                  |
| `SecondaryButton`| `Text`, `Command`                                                               | Ghost: transparent bg, `NavyDeep` text in `ButtonLg`. Used for "View Calendar"-style links.                                                                                                                                                                         |
| `SearchField`    | `Placeholder`, `Text` (TwoWay)                                                  | 56-pt input, leading magnifying-glass glyph, `SurfaceMuted` bg, `RadiusChip` × 2 corners, `Sp4` horizontal padding.                                                                                                                                                |
| `ListRow`        | `LeadingContent` (ContentPresenter), `Title`, `Subtitle`, `TrailingChevron`    | `SurfaceMuted` 50% bg, `RadiusCard`, `Sp4` padding. Layout: leading slot (date pill / avatar / status dot) + title (`H4Item`) + subtitle (`Caption`, `TextSecondary`) + optional `Lime` 32-pt circle with chevron-right glyph.                                       |

### Icons
`Resources/Fonts/Lucide.ttf` registered as `LucideIcons` font family. `Gymers/Controls/Icons.cs` exposes nine glyph string constants — `Users`, `Calendar`, `DollarSign`, `Search`, `Bell`, `ChevronRight`, `ArrowUp`, `LogIn`, `Plus`. Each is a one-character `\uXXXX` escape.

The codepoints come from the `lucide-static` npm package's `font/info.json` file (each icon entry has a `unicode` field). The implementer fetches it once and pastes the nine values:
- Source: `https://unpkg.com/lucide-static@latest/font/info.json`
- The `Lucide.ttf` itself goes from `https://unpkg.com/lucide-static@latest/font/lucide.ttf` into `Resources/Fonts/`.

## 6. Navigation & Information Architecture

`AppShell.xaml` structure:
```
<Shell ...>
  <ShellContent Title="Login" Route="Login" ContentTemplate="{DataTemplate pages:LoginPage}" />
  <TabBar>
    <ShellContent Route="Dashboard"  ContentTemplate="{DataTemplate pages:DashboardPage}" />
    <ShellContent Route="Members"    ContentTemplate="{DataTemplate pages:MembersPage}" />
    <ShellContent Route="Payments"   ContentTemplate="{DataTemplate pages:PaymentsPage}" />
    <ShellContent Route="Attendance" ContentTemplate="{DataTemplate pages:AttendancePage}" />
  </TabBar>
</Shell>
```

Shell-wide attached properties:
- `Shell.NavBarIsVisible="False"` — we render our own `TopAppBar`.
- `Shell.TabBarIsVisible="False"` — we render our own `BottomTabBar`.

### Flows
- **App start** → `LoginPage` (full-bleed, no top/bottom bar).
- **Sign In tap** → `Shell.Current.GoToAsync("//Dashboard")` — clears the back stack so Login is not in nav history.
- **Tab switches** use absolute routes: `//Dashboard`, `//Members`, `//Payments`, `//Attendance`. No stack accumulation.
- **Sign-out** is out of scope; would be `GoToAsync("//Login")` later.

### Per-page chrome (every tab page)
```
ContentPage  BackgroundColor="{StaticResource BgApp}"
└── Grid  RowDefinitions="Auto, *, Auto"
    ├── TopAppBar         (Grid.Row 0)
    ├── ScrollView        (Grid.Row 1) — page-specific content
    └── BottomTabBar      (Grid.Row 2) ActiveTab=<this page>
```
Safe area respected via `Page.UseSafeArea="true"`.

### TopAppBar configuration per tab
| Page         | Title         | Trailing icon |
|--------------|---------------|---------------|
| Dashboard    | "Dashboard"   | `Bell`        |
| Members      | "Members"     | `Plus`        |
| Payments     | "Payments"    | `Plus`        |
| Attendance   | "Attendance"  | `Calendar`    |

## 7. Per-Screen Content

### 7.1 LoginPage
Full-bleed `BgApp`. No `TopAppBar` / `BottomTabBar`.

```
GYMERS                                  ← Manrope ExtraBold 30pt, NavyHeading, top center
                                          (text wordmark — no logo asset)

Welcome back                            ← H1Page, NavyHeading
Sign in to manage your gym              ← BodyMd, TextSecondary

┌──────────────────────────────────┐    ← Card style
│ Username                          │
│ ┌──────────────────────────────┐ │    ← LabeledInput
│ │ admin                        │ │
│ └──────────────────────────────┘ │
│ Password                          │
│ ┌──────────────────────────────┐ │    ← LabeledInput, IsPassword=true
│ │ ••••••••                     │ │
│ └──────────────────────────────┘ │
│                                   │
│  ( Admin )  ( Staff )             │    ← 2-pill segment selector
│                                   │
│ ┌──────────────────────────────┐ │
│ │       SIGN IN                │ │    ← PrimaryButton
│ └──────────────────────────────┘ │
└──────────────────────────────────┘

Demo: any username/password works       ← BodySm, TextMuted, centered
```

Sign-in tap navigates to `//Dashboard`. The Admin/Staff selector is purely visual; both lead to the same screens.

**Admin/Staff selector implementation note:** built inline on `LoginPage` (not a reusable component in v1) — a horizontal `StackLayout` of two `Border`+`Label`+`TapGestureRecognizer` pills. Active pill = `NavyDeep` bg, white text in `ButtonLg`. Inactive pill = transparent bg, `TextSecondary` text. Both pills `RadiusPill`, `Sp4 Sp2` padding. State held in the page's view-model.

### 7.2 DashboardPage
TopAppBar (avatar / "Dashboard" / Bell). Then `ScrollView`:

1. `KpiCard` Light — `TOTAL MEMBERS` / `1,250` / `+5%` "this month".
2. `KpiCard` Light — `TODAY'S ATTENDANCE` / `350` / `+12%` "vs yesterday".
3. `KpiCard` Dark — `MONTHLY EARNINGS` / `$45,000` / `+8%` "projected growth".
4. **Live Capacity card** — H3 "Live Capacity" (left) + caption "Real-time gym floor occupancy" + right-aligned `78%` (NavyDeep, 30pt SemiBold) above "PEAK HOUR" (Olive, uppercase). Gradient progress bar (`NavyMid → Olive`, 78% width). 2×2 grid: `CARDIO ZONE 92%`, `WEIGHT ROOM 65%`, `YOGA STUDIO 40%`, `POOL AREA 15%`.
5. **Coach Spotlight card** — 80×80 avatar (`coach_marcus.png`) + H3 "Marcus Sterling" + role "Lead Performance Coach" (NavyDeep, 14sb). Two between-rule rows: "Client Rating … 4.9/5.0", "Sessions Completed … 142". `PrimaryButton` "View Performance Profile" (no-op on tap in v1).
6. **Today's Classes card** — H2 "Today's Classes" (left) + `SecondaryButton` "View Schedule" (right). 3 `ListRow`s with date-pill leading:
   - `14 OCT` · *High-Intensity Power Blast* · "Studio A • 10:30–11:30 AM"
   - `14 OCT` · *Zen Flow Vinyasa* · "Yoga Loft • 12:00–01:15 PM"
   - `14 OCT` · *Advanced Squat Workshop* · "Performance Zone • 01:30–03:00 PM"

### 7.3 MembersPage
TopAppBar (avatar / "Members" / Plus). Then:
1. `SearchField` "Search by name…"
2. `KpiCard` Light — `ACTIVE MEMBERS` / `1,250` / `+5%` "this month".
3. H3 "All Members" + 6 `ListRow`s with initial-circle avatar (no chevron):

| Name              | Tier    | Status         | Expires     |
|-------------------|---------|----------------|-------------|
| Marcus Sterling   | Premium | Active         | 12/15/2026  |
| Lena Park         | Elite   | Active         | 03/04/2027  |
| Diego Alvarez     | Basic   | Active         | 06/22/2026  |
| Aisha Khan        | Premium | Active         | 11/01/2026  |
| Sam Chen          | Basic   | Expiring Soon  | 05/30/2026  |
| Priya Nair        | Elite   | Active         | 08/14/2027  |

Subtitle format: `"<Tier> · <Status> · Expires <Date>"`.

### 7.4 PaymentsPage
TopAppBar (avatar / "Payments" / Plus). Then:
1. `KpiCard` Dark — `TODAY'S EARNINGS` / `$1,250` / `+8%` "vs yesterday".
2. Card "Record Payment" — `LabeledInput`s for Member, Amount, Method (text). `PrimaryButton` "Record Payment" (no-op in v1).
3. H3 "Recent Payments" + 5 `ListRow`s:

| Member            | Amount   | Method | Receipt # |
|-------------------|----------|--------|-----------|
| Marcus Sterling   | $99.00   | Card   | 1042      |
| Lena Park         | $149.00  | Card   | 1041      |
| Diego Alvarez     | $49.00   | Cash   | 1040      |
| Aisha Khan        | $99.00   | Bank   | 1039      |
| Sam Chen          | $49.00   | Cash   | 1038      |

Subtitle format: `"$<Amount> · <Method> · Receipt #<N>"`.

### 7.5 AttendancePage
TopAppBar (avatar / "Attendance" / Calendar). Then:
1. `KpiCard` Light — `TODAY'S CHECK-INS` / `350` / `+12%` "vs yesterday".
2. Card "Check In" — `SearchField` "Search member by name…" + `PrimaryButton` "Check In" (no-op in v1).
3. H3 "Recent Check-ins" + 6 `ListRow`s with green status dot leading (12-pt circle, `Olive`):

| Member            | Time     |
|-------------------|----------|
| Marcus Sterling   | 9:42 AM  |
| Lena Park         | 9:38 AM  |
| Diego Alvarez     | 9:21 AM  |
| Aisha Khan        | 9:15 AM  |
| Priya Nair        | 9:08 AM  |
| Sam Chen          | 8:51 AM  |

Subtitle format: `"Checked in · <Time>"`.

## 8. Data Model

### POCO Models (`Gymers/Models/`)
```csharp
public enum MembershipTier { Basic, Premium, Elite }

public record Member(string Id, string Name, MembershipTier Tier, string Status, DateOnly Expires);
public record Payment(int Id, string MemberId, decimal Amount, string Method, int ReceiptNumber, DateTime At);
public record CheckIn(int Id, string MemberId, DateTime At);
public record ClassSession(string Id, string Title, string Location, DateTime Start, DateTime End);
```

### Sample data (`Gymers/Data/SampleData.cs`)
Static `IReadOnlyList<>` collections seeded with the rows in Section 7. No mutation in v1 (buttons are no-ops).

## 9. Verification & Success Criteria

### Build / runtime
1. `dotnet build Gymers/Gymers.csproj -f net10.0-ios` — 0 warnings, 0 errors.
2. App launches in iOS 26.2 simulator (e.g. `iPhone 15 Pro`).
3. All 5 screens render without visual glitches.
4. `LoginPage` → tap *Sign In* → lands on `DashboardPage`.
5. Tapping each of the 4 bottom tabs from any tab page swaps to that page; the active tab pill updates correctly.
6. Sample data appears as specified in Section 7.
7. No crashes navigating any combination of tabs in any order.

### Visual fidelity
- Side-by-side: simulator screenshot at iPhone 15 Pro resolution next to the corresponding Figma frame.
- Spot-check primary colors with macOS Digital Color Meter (every primary token within ΔE < 5 of the Figma hex).
- Manrope and Inter render correctly (not falling back to system fonts) — verify in MAUI font registration log on first launch.

### Manual test script
1. Boot iOS 26.2 simulator (`xcrun simctl list devices`).
2. Run via `dotnet build -t:Run -f net10.0-ios -p:_DeviceName=:v2:udid=<simulator-udid>` or via VS Code with the simulator selected.
3. Walk: Login → Dashboard → Members → Dashboard → Payments → Attendance → Dashboard.
4. Capture 5 screenshots (one per screen) and compare to the Figma frames.

### No automated tests in v1
The work is XAML/visual + hardcoded data; unit tests aren't valuable here. If test coverage matters later, that is a v2 conversation.

## 10. Risks

- **Lucide icon codepoints** — fetched at implementation time from `lucide-static`'s `font/info.json`. Low risk, mechanical lookup.
- **MAUI font registration on iOS 26.2 / .NET 10** — custom fonts can fail silently. First build should print `[Microsoft.Maui]` font-registration logs to confirm Manrope/Inter/Lucide loaded.
- **Backdrop blur** — Figma uses `backdrop-blur(12px)` on top/bottom bars. MAUI does not have first-class blur without a platform handler; v1 falls back to opaque white at 95% alpha. Acceptable visual compromise.
- **Demo dataset realism** — sample dates/numbers are fixed. If the prof asks "why does the dashboard say `+12% vs yesterday` regardless of when I open it?", the answer is "this is a demo build with hardcoded data; v2 wires SQLite."
