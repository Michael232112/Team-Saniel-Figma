# Gymers — Gym Management System Design

| | |
|---|---|
| **Date** | 2026-05-02 |
| **Status** | Approved (pending user review) |
| **Team** | Fafa (Gonzaga, Magallones, Medillo, Naces, Saniel) |
| **Course** | College of Technology and Engineering — Information Technology Department |

## 1. Context

Gyms manage members, payments, trainers, equipment, and workout plans daily. Manual logbooks and spreadsheets are slow and error-prone. This system digitizes those operations into a single cross-platform desktop application built with .NET MAUI, targeting Mac Catalyst as the primary demo surface.

The app has two login roles — **Admin** (gym owner/manager) and **Staff** (front-desk receptionist) — sharing a local SQLite database.

## 2. Goals

1. Manage member profiles, photos, and membership lifecycle (registration → renewal → expiry).
2. Record payments and generate PDF receipts.
3. Track member attendance via name search.
4. Manage trainer profiles and assignments.
5. Build and assign structured workout plans (exercises with sets/reps/weight).
6. Maintain equipment inventory with maintenance scheduling.
7. Surface KPIs and tabular reports for business decisions.

## 3. Non-Goals (v1)

- QR-code check-in (deferred — search-by-name only).
- Member self-service login.
- Multi-device synchronization (single SQLite file, single device).
- Charts (KPI cards + tables only).
- Online payments / payment-gateway integration.
- iOS / Android / Windows builds (codebase will be portable, but only Mac Catalyst is built and demoed).
- Multi-gym / multi-tenant support.

## 4. Locked Decisions

| # | Decision | Rationale |
|---|---|---|
| 1 | **Two login roles, members are profile-only** | Matches the proposal; smallest auth surface. |
| 2 | **Mac Catalyst desktop primary** | Team is on macOS; no emulator; webcam works for photo capture. |
| 3 | **Search-by-name check-in (no QR)** | Simpler; no scanner library; can be added in a later phase. |
| 4 | **Default admin `admin / admin123`, must change on first login; admin creates staff** | Standard pattern, easy demo. |
| 5 | **Photos: file picker + webcam capture** | Both supported by MAUI's `MediaPicker` on Mac Catalyst. |
| 6 | **Receipts: PDF saved + auto-open** | Polished demo; uses QuestPDF. |
| 7 | **Reports: KPI cards + tables (no charts)** | Avoids extra charting library; sufficient for v1. |
| 8 | **Workout plans: structured exercises with sets/reps/weight** | Realistic gym-app feel; small extra schema. |
| 9 | **Currency: Philippine Peso (₱)** | Matches the team's locale. |

## 5. Architecture

**Approach A — Single MAUI project, layered folders.**

### 5.1 Tech stack

| Concern | Choice |
|---|---|
| Framework | .NET 8 MAUI |
| Target | `net8.0-maccatalyst` |
| MVVM | `CommunityToolkit.Mvvm` (source generators for `[ObservableProperty]`, `[RelayCommand]`) |
| UI helpers | `CommunityToolkit.Maui` (Snackbar, Toast) |
| Database | SQLite via `sqlite-net-pcl` + `SQLitePCLRaw.bundle_green` |
| PDF | `QuestPDF` |
| Auth hashing | `BCrypt.Net-Next` |
| Logging | `Microsoft.Extensions.Logging` + `Serilog.Sinks.File` |
| Testing | xUnit + NSubstitute |

### 5.2 Project structure

```
Gymers.sln
├── Gymers/                                # .NET 8 MAUI app
│   ├── App.xaml, AppShell.xaml, MauiProgram.cs
│   ├── Models/                            # POCOs
│   ├── Data/
│   │   ├── GymDatabase.cs                 # SQLiteAsyncConnection wrapper
│   │   └── Repositories/                  # one per aggregate
│   ├── Services/                          # business logic, no MAUI types
│   ├── ViewModels/                        # one per page
│   ├── Views/
│   │   ├── Auth/, Admin/, Staff/, Shared/
│   ├── Converters/                        # CurrencyConverter, StatusToColorConverter
│   ├── Resources/Styles/                  # Colors.xaml, Styles.xaml
│   └── Platforms/MacCatalyst/Info.plist
└── Gymers.Tests/                          # xUnit, references Gymers
    ├── Services/
    └── Data/
```

### 5.3 Dependency injection

Registered in `MauiProgram.CreateMauiApp()`:

| Lifetime | Types |
|---|---|
| Singleton | `GymDatabase`, all repositories, all services, `UserSession`, `INavigationService` |
| Transient | All ViewModels, all Views/Pages |

Routes registered with `Routing.RegisterRoute(...)` for each page reachable via `Shell.Current.GoToAsync`.

## 6. Database Schema

All tables stored in a single SQLite file at `FileSystem.AppDataDirectory/gymers.db3`.

### Users
```
Id INTEGER PK AUTOINCREMENT
Username TEXT UNIQUE NOT NULL
PasswordHash TEXT NOT NULL                 -- BCrypt
Role TEXT NOT NULL CHECK (Role IN ('Admin','Staff'))
FullName TEXT NOT NULL
Email TEXT
MustChangePassword INTEGER NOT NULL DEFAULT 0
IsActive INTEGER NOT NULL DEFAULT 1        -- soft-delete
CreatedAt TEXT NOT NULL                    -- ISO-8601 UTC
```

Seed: one Admin row `(admin, BCrypt('admin123'), 'Admin', 'Default Admin', MustChangePassword=1)`.

### Members
```
Id INTEGER PK AUTOINCREMENT
FullName TEXT NOT NULL
ContactNumber TEXT NOT NULL
Email TEXT
DateOfBirth TEXT                            -- ISO date
Address TEXT
PhotoPath TEXT                              -- relative to AppData/MemberPhotos/
MembershipType TEXT NOT NULL CHECK (MembershipType IN ('Monthly','Quarterly','Annual'))
MembershipStartDate TEXT NOT NULL
MembershipEndDate TEXT NOT NULL
Status TEXT NOT NULL CHECK (Status IN ('Active','Inactive','Expired'))   -- cached, see precedence below
Notes TEXT
IsActive INTEGER NOT NULL DEFAULT 1          -- soft-delete; distinct from Status
CreatedAt TEXT NOT NULL
UpdatedAt TEXT NOT NULL

INDEX idx_members_status (Status, IsActive)
INDEX idx_members_endDate (MembershipEndDate)
```

**`Status` vs `IsActive`** — distinct fields:
- `IsActive` (0/1): soft-delete flag. When 0, the member is hidden from all lists and their record is preserved only for historical reporting. The "Mark as Removed" button on `MemberDetailPage` sets this to 0.
- `Status` (enum): the member's *current* membership/activity state. Computed on save and on app-startup background task with this precedence:
  1. `Expired` — if `MembershipEndDate < today`
  2. `Inactive` — else if no `Attendance` row in the last 30 days
  3. `Active` — otherwise

### Trainers
```
Id INTEGER PK AUTOINCREMENT
FullName TEXT NOT NULL
ContactNumber TEXT
Email TEXT
Specialization TEXT
AvailabilityNotes TEXT                      -- e.g. "Mon–Fri 9am–5pm"
HourlyRate REAL
HireDate TEXT
PhotoPath TEXT
IsActive INTEGER NOT NULL DEFAULT 1
CreatedAt TEXT NOT NULL
```

### TrainerAssignments
```
Id INTEGER PK AUTOINCREMENT
TrainerId INTEGER NOT NULL FK→Trainers
MemberId INTEGER NOT NULL FK→Members
StartDate TEXT NOT NULL
EndDate TEXT                                -- null = active
Notes TEXT

INDEX idx_assignments_member (MemberId, StartDate DESC)
```

### Payments
```
Id INTEGER PK AUTOINCREMENT
MemberId INTEGER NOT NULL FK→Members
Amount REAL NOT NULL
PaymentType TEXT NOT NULL CHECK (PaymentType IN ('NewMembership','Renewal','WalkIn','PersonalTraining','Other'))
PaymentMethod TEXT NOT NULL CHECK (PaymentMethod IN ('Cash','GCash','Card','BankTransfer'))
ReceiptNumber TEXT UNIQUE NOT NULL          -- 'RCPT-2026-0001'
ReceiptPdfPath TEXT
ProcessedByUserId INTEGER NOT NULL FK→Users
ProcessedAt TEXT NOT NULL
Notes TEXT

INDEX idx_payments_member (MemberId, ProcessedAt DESC)
INDEX idx_payments_date (ProcessedAt DESC)
```

### Attendance
```
Id INTEGER PK AUTOINCREMENT
MemberId INTEGER NOT NULL FK→Members
CheckInTime TEXT NOT NULL
CheckOutTime TEXT
ProcessedByUserId INTEGER NOT NULL FK→Users

INDEX idx_attendance_member (MemberId, CheckInTime DESC)
INDEX idx_attendance_date (CheckInTime DESC)
```

### Exercises
```
Id INTEGER PK AUTOINCREMENT
Name TEXT UNIQUE NOT NULL
MuscleGroup TEXT
Description TEXT
```

### WorkoutPlans
```
Id INTEGER PK AUTOINCREMENT
Name TEXT NOT NULL
Description TEXT
IsTemplate INTEGER NOT NULL                 -- 1=template, 0=assigned-to-member
AssignedToMemberId INTEGER FK→Members       -- null when IsTemplate=1
AssignedByTrainerId INTEGER FK→Trainers
CreatedAt TEXT NOT NULL
UpdatedAt TEXT NOT NULL

INDEX idx_plans_member (AssignedToMemberId)
INDEX idx_plans_template (IsTemplate)
```

### WorkoutPlanExercises
```
Id INTEGER PK AUTOINCREMENT
WorkoutPlanId INTEGER NOT NULL FK→WorkoutPlans (ON DELETE CASCADE)
ExerciseId INTEGER NOT NULL FK→Exercises
Sets INTEGER NOT NULL
Reps INTEGER NOT NULL
Weight REAL                                 -- nullable (bodyweight exercises)
"Order" INTEGER NOT NULL
Notes TEXT

INDEX idx_planex_plan (WorkoutPlanId, "Order")
```

### Equipment
```
Id INTEGER PK AUTOINCREMENT
Name TEXT NOT NULL
SerialNumber TEXT
Category TEXT NOT NULL CHECK (Category IN ('Cardio','Strength','FreeWeights','Other'))
Status TEXT NOT NULL CHECK (Status IN ('Available','InUse','Maintenance','OutOfOrder'))
Condition TEXT NOT NULL CHECK (Condition IN ('Excellent','Good','Fair','Poor'))
PurchaseDate TEXT
LastMaintenanceDate TEXT
NextMaintenanceDate TEXT
Notes TEXT
IsActive INTEGER NOT NULL DEFAULT 1

INDEX idx_equipment_status (Status, IsActive)
INDEX idx_equipment_maintenance (NextMaintenanceDate)
```

### Conventions

- **Foreign keys enforced.** SQLite requires `PRAGMA foreign_keys = ON;` per connection. `GymDatabase.InitAsync` will issue this on every connection acquired so that `ON DELETE CASCADE` on `WorkoutPlanExercises.WorkoutPlanId` actually fires.
- **No hard deletes for top-level aggregates.** `IsActive=0` on Users / Members / Trainers / Equipment. Hard delete is only used for `WorkoutPlanExercises` (cascaded when a plan is deleted) and `TrainerAssignments` superseded by an end-dated row.
- **Membership renewal:** Recording a `Renewal` Payment extends `Member.MembershipEndDate` by the membership-type period (`+1 month` / `+3 months` / `+1 year`) inside the same transaction as the Payment insert. Old Payment rows are kept as history; `MembershipStartDate` is **not** changed on renewal.
- **`Member.Status`** precedence is defined in the Members section above (Expired > Inactive > Active). Recalculated on member save, on payment save, on attendance insert, and via the app-startup background task.
- **Receipt numbers:** `RCPT-{year}-{4-digit-counter}`, generated atomically inside the Payment-insert transaction by selecting `MAX(counter)` for the current year and adding 1. The combination is enforced unique by the column constraint.

## 7. Modules

### 7.1 Shared (no auth)

| Page | Purpose |
|---|---|
| `LoginPage` | Username + password. Validates via `AuthService`. Routes to Admin or Staff shell. |
| `ChangePasswordPage` | Forced when `User.MustChangePassword=1`. Validates old password; new + confirm. |

### 7.2 Admin shell (Flyout)

| Page | Purpose |
|---|---|
| `AdminDashboard` | KPIs: Total Active Members · Today's Check-ins · Expiring This Week · Monthly Revenue. Recent activity. |
| `MembersListPage` | Searchable + filterable. Tap → detail. "+ New Member". |
| `MemberFormPage` | Add/edit. Photo via webcam **or** file picker. Membership type + start date → auto end date. |
| `MemberDetailPage` | Profile + tabs: **Payments** (full history of payments tied to this member), **Attendance** (last 30 days), **Workout Plan** (the *current* assigned plan — `WorkoutPlans` row with `IsTemplate=0` and `AssignedToMemberId=this`, latest `CreatedAt`), **Trainer** (the *current* assignment — `TrainerAssignments` row with `EndDate IS NULL`). Buttons: **Edit** · **Renew** (opens PaymentFormPage prefilled with type=Renewal) · **Mark as Removed** (sets `IsActive=0`; does not affect `Status`). |
| `PaymentsListPage` | Filterable by date range. Tap → opens saved PDF. |
| `PaymentFormPage` | Choose member → type → amount → method → Save → generate PDF + extend membership if Renewal. |
| `AttendanceListPage` | Default = today. Filter by member or date range. |
| `TrainersListPage` / `TrainerFormPage` / `TrainerDetailPage` | CRUD; detail shows assigned members + history. |
| `TrainerAssignmentPage` | Pick member → trainer → start date → save. |
| `WorkoutTemplatesListPage` / `WorkoutPlanFormPage` | CRUD templates. Add exercises (search Exercises master). |
| `AssignWorkoutPlanPage` | Pick template → pick member → optional tweaks → save (copies to a new non-template plan). |
| `ExercisesListPage` / `ExerciseFormPage` | CRUD master library. |
| `EquipmentListPage` / `EquipmentFormPage` | CRUD; list highlights `NextMaintenanceDate ≤ today + 7`. |
| `ReportsPage` | Tabs: Membership Summary, Revenue, Attendance. KPI cards + tables. Date-range pickers. |
| `SettingsPage` | Manage Staff accounts (create / disable / reset password). About + DB file location. |

### 7.3 Staff shell (Tab bar)

| Page | Purpose |
|---|---|
| `StaffDashboard` | Quick actions + today's check-ins. |
| `CheckInPage` | Typeahead search by name/contact. Tap → confirm → record Attendance. |
| `MemberSearchPage` | Read-only member directory. |
| `PaymentFormPage` | Same as Admin's. |
| `TrainerScheduleViewPage` | Read-only trainer list with `AvailabilityNotes`. |

### 7.4 App-startup background task

Runs once when the app launches (after DB init, before the first page renders):

- Recalculate `Member.Status` for every active member, applying the precedence in the Members schema section (Expired → Inactive → Active).

Per-page KPI queries (e.g., "Expiring This Week", "Today's Check-ins", "Monthly Revenue") are computed on-demand inside the dashboard ViewModels — not in this background task.

## 8. Auth & Navigation Flow

```
App start
  └─► AppShell → "//Login"
        └─► AuthService.LoginAsync
              ├─► fetch User by Username
              ├─► verify BCrypt
              ├─► check IsActive
              └─► UserSession.CurrentUser = user

      MustChangePassword? ──yes──► "//ChangePassword" ──► back to login flow
                          ──no───► branch on Role:
                                     Admin → "//admin/dashboard"
                                     Staff → "//staff/dashboard"
```

- `UserSession` (singleton) exposes `CurrentUser`, `IsAdmin`, `IsStaff`, raises `Changed`.
- `AppShell` binds `IsVisible` of the Admin `FlyoutItem` and Staff `TabBar` to `IsAdmin`/`IsStaff`.
- `INavigationService` wraps `Shell.Current.GoToAsync` so ViewModels stay free of static MAUI calls.
- Detail pages receive parameters via query: `GoToAsync($"memberDetail?id={member.Id}")`.
- Logout (Admin Settings + Staff dashboard footer): clears `UserSession`, navigates to `//Login`.

## 9. MVVM Pattern

```
View (.xaml, x:DataType=…)
   │  data-binding
   ▼
ViewModel (CommunityToolkit.Mvvm)
   │  service calls
   ▼
Service (business logic, no MAUI types)
   │
   ▼
Repository (data access only, uses SQLiteAsyncConnection)
   │
   ▼
Models (POCOs)
```

**Rules:**
- Views reference only ViewModels.
- ViewModels never reference SQLite or repositories — only services.
- Services never reference MAUI types — keeps them unit-testable in `Gymers.Tests`.

## 10. Error Handling

| Layer | Strategy |
|---|---|
| Validation | In ViewModel before service calls; bound to fields with red-border + error label. No exceptions for validation. |
| Service errors | Typed exceptions (`MembershipExpiredException`, `DuplicateUsernameException`) caught by ViewModel → `Snackbar.Make(message)`. |
| Repository errors | Caught at service boundary; logged; surfaced as generic "Could not save — try again". |
| File I/O | Try/catch around `File.*`; specific user messages. |
| Async commands | Every `[RelayCommand]` async wraps body in try/catch. Unhandled → `App.OnUnhandledException` → log + crash dialog. |
| Logging | Serilog file sink at `AppData/Logs/gymers-{date}.log`. |

## 11. Testing Strategy

| Test type | Tooling | Coverage |
|---|---|---|
| Service unit tests | xUnit + NSubstitute | Auth (login success/fail/locked/force-change), Membership (renewal extends end date, expiry calc), Payment (sequential receipt numbers, period extension on Renewal). |
| Repository tests | xUnit + real SQLite (temp file, deleted in `Dispose`) | CRUD + soft-delete + index-backed queries. |
| ViewModel tests | xUnit + NSubstitute | Command behavior, observable property change, navigation calls. |
| UI / automation | — | Out of scope for v1; manual smoke-test checklist instead. |

**TDD:** Service + repository code is test-first per the `test-driven-development` superpowers skill. ViewModels: tests after the page works in dev. Views: untested.

## 12. Visual Styling

Until Figma PNG exports arrive, screens use placeholder palette extracted from the thumbnail of `Team saniel.fig`:

```xml
<Color x:Key="Primary">#1A2A4A</Color>     <!-- navy -->
<Color x:Key="Accent">#3CD278</Color>      <!-- green -->
<Color x:Key="Background">#FFFFFF</Color>
<Color x:Key="TextPrimary">#111111</Color>
<Color x:Key="TextMuted">#6B7280</Color>
<Color x:Key="Surface">#F8F9FB</Color>
<Color x:Key="Danger">#E5484D</Color>
<Color x:Key="Border">#E5E7EB</Color>
```

A separate phase (Phase 7 in the implementation plan) refines the palette, spacing, and typography against the actual Figma exports once the team drops them into a `designs/` folder.

## 13. Risks & Open Questions

- **QR check-in dropped from v1.** If the professor pushes back on the missing proposal objective, we add it as a Phase 8 (camera permission + `ZXing.Net.Maui.Controls` + per-member QR PNG generation).
- **Camera on Mac Catalyst.** `MediaPicker.CapturePhotoAsync` works on Mac Catalyst 13+; if the grading machine is older, file-picker still works.
- **QuestPDF community license** is free for non-commercial use; that fits an academic project.
- **App-startup status recalculation** is O(N members). Fine for hundreds; if the dataset grows past low thousands, we'd replace the per-row read+save loop with a single SQL `UPDATE` driven by joined attendance counts.
- **Figma exports not yet in the repo** — the visual styling phase will block on this.

## 14. Out of Scope (explicitly)

- Cloud sync / multi-device.
- Member self-service login or mobile member-facing app.
- iOS, Android, Windows builds.
- Online payments / payment-gateway integrations.
- Advanced analytics, charts, exports beyond receipts.
- Multi-gym / multi-tenant.
- I18n beyond Philippine Peso formatting.
- Offline-mode reconciliation (single device, always online to its local DB).

## 15. Implementation Plan Handoff

Once this spec is approved by the team, the next step is the `superpowers:writing-plans` skill, which will produce a phased, step-by-step `docs/superpowers/plans/2026-05-02-gym-management-plan.md` covering:

1. Project init, NuGet packages, AppShell skeleton, theme placeholder.
2. Database layer (GymDatabase + repositories) — TDD.
3. Auth flow (Login + ChangePassword + UserSession + role routing) — TDD on AuthService.
4. Admin modules — Members, Payments, Attendance.
5. Admin modules — Trainers, Workout Plans, Equipment.
6. Admin Reports + Settings.
7. Staff shell (Dashboard, Check-In, Search, Payment, Trainer Schedule).
8. Visual styling pass against Figma exports.
9. Manual smoke-test checklist + final polish.
