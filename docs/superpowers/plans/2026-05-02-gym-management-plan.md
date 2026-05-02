# Gymers — Gym Management System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Mac Catalyst desktop gym-management app (.NET MAUI + SQLite) with two login roles (Admin, Staff) covering members, payments, attendance, trainers, workout plans, equipment, and reports.

**Architecture:** Single-project MAUI app with layered folders (Models / Data / Services / ViewModels / Views). MVVM via CommunityToolkit.Mvvm source generators. All business logic in services with no MAUI dependencies, so the entire `Services/` and `Data/` layers are unit-testable from a separate xUnit test project.

**Tech Stack:** .NET 8, MAUI (`net8.0-maccatalyst`), `sqlite-net-pcl`, `CommunityToolkit.Mvvm`, `CommunityToolkit.Maui`, `QuestPDF`, `BCrypt.Net-Next`, Serilog file sink, xUnit + NSubstitute.

**Spec reference:** `docs/superpowers/specs/2026-05-02-gym-management-design.md`

---

## File Structure

The solution has **three projects**. The split is required because the MAUI app project targets `net8.0-maccatalyst` (a TPM-bound framework that can't be referenced from a `net8.0` test project). Pulling testable code into a plain `net8.0` library (`Gymers.Core`) lets the test project reference it without the TFM mismatch.

```
Gymers.sln
├── Gymers.Core/                                  # net8.0 class library (no MAUI types)
│   ├── Gymers.Core.csproj
│   ├── Models/
│   │   ├── Enums.cs                              # Role, MembershipType, MemberStatus, PaymentType, PaymentMethod, EquipmentCategory, EquipmentStatus, EquipmentCondition
│   │   ├── User.cs, Member.cs, Trainer.cs, TrainerAssignment.cs
│   │   ├── Payment.cs, Attendance.cs
│   │   ├── Exercise.cs, WorkoutPlan.cs, WorkoutPlanExercise.cs
│   │   └── Equipment.cs
│   ├── Data/
│   │   ├── GymDatabase.cs                        # connection + init
│   │   └── Repositories/
│   │       ├── IUserRepository.cs   / UserRepository.cs
│   │       ├── IMemberRepository.cs / MemberRepository.cs
│   │       ├── ITrainerRepository.cs / TrainerRepository.cs
│   │       ├── ITrainerAssignmentRepository.cs / TrainerAssignmentRepository.cs
│   │       ├── IPaymentRepository.cs / PaymentRepository.cs
│   │       ├── IAttendanceRepository.cs / AttendanceRepository.cs
│   │       ├── IExerciseRepository.cs / ExerciseRepository.cs
│   │       ├── IWorkoutPlanRepository.cs / WorkoutPlanRepository.cs
│   │       └── IEquipmentRepository.cs / EquipmentRepository.cs
│   └── Services/                                 # business logic only — no MAUI types
│       ├── IClock.cs / SystemClock.cs
│       ├── IUserSession.cs / UserSession.cs
│       ├── INavigationService.cs                 # interface only; impl lives in Gymers
│       ├── IAuthService.cs / AuthService.cs / AuthExceptions.cs
│       ├── IMembershipService.cs / MembershipService.cs
│       ├── IPaymentService.cs / PaymentService.cs
│       ├── IReceiptPdfService.cs / ReceiptPdfService.cs       # constructor takes output dir explicitly
│       ├── IAttendanceService.cs / AttendanceService.cs
│       ├── IPhotoStorageService.cs / PhotoStorageService.cs   # constructor takes root dir explicitly
│       ├── ITrainerService.cs / TrainerService.cs
│       ├── IWorkoutPlanService.cs / WorkoutPlanService.cs
│       ├── IEquipmentService.cs / EquipmentService.cs
│       ├── IReportService.cs / ReportService.cs
│       └── StartupService.cs
│
├── Gymers/                                       # MAUI app, net8.0-maccatalyst
│   ├── Gymers.csproj                             # references Gymers.Core
│   ├── App.xaml(.cs), AppShell.xaml(.cs), MauiProgram.cs
│   ├── Services/
│   │   └── NavigationService.cs                  # implementation — uses Shell.Current
│   ├── ViewModels/                               # MAUI-coupled VMs (FilePicker, MediaPicker, QueryProperty, Launcher, DisplayAlert)
│   │   ├── Admin/ (form VMs that use MAUI APIs)
│   │   └── Staff/ (VMs that use MAUI APIs)
│   ├── Views/
│   │   ├── Auth/, Admin/, Staff/, Shared/
│   ├── Converters/  StringNotEmptyToBoolConverter.cs, InverseBoolConverter.cs, CurrencyConverter.cs, StatusToColorConverter.cs
│   ├── Resources/Styles/  Colors.xaml, Styles.xaml
│   └── Platforms/MacCatalyst/Info.plist
│
└── Gymers.Tests/                                 # net8.0 xUnit project; references ONLY Gymers.Core
    ├── Gymers.Tests.csproj
    ├── TestHelpers/
    │   ├── TempDatabaseFixture.cs
    │   └── FakeClock.cs
    ├── Services/    *Tests.cs
    ├── Data/        *RepositoryTests.cs
    └── ViewModels/  LoginViewModelTests.cs, ChangePasswordViewModelTests.cs   # auth VMs — they have no MAUI deps and live in Core
```

In addition, `Gymers.Core/ViewModels/` holds the **MAUI-free** ViewModels:

```
Gymers.Core/ViewModels/
├── BaseViewModel.cs
└── Auth/
    ├── LoginViewModel.cs
    └── ChangePasswordViewModel.cs
```

**Namespaces** stay simple: code in `Gymers.Core/` uses `Gymers.Models`, `Gymers.Data`, `Gymers.Data.Repositories`, `Gymers.Services`, `Gymers.ViewModels.*` (no `.Core` suffix in the namespace). Code in `Gymers/` uses `Gymers`, `Gymers.ViewModels.*` (with namespace shared across assemblies), `Gymers.Views.*`, `Gymers.Services` (the `NavigationService` impl shares the `Gymers.Services` namespace with its interface in Core — that is fine, they are just two assemblies contributing to the same namespace).

**XAML caveat:** because `LoginViewModel` lives in `Gymers.Core` while `LoginPage.xaml` lives in `Gymers`, the page's XAML must reference the VM's assembly:
```xml
xmlns:vm="clr-namespace:Gymers.ViewModels.Auth;assembly=Gymers.Core"
```
Pages whose VMs live alongside them in `Gymers/` use the bare `xmlns:vm="clr-namespace:..."` form (no `assembly=` needed).

**ViewModel testability split:** auth VMs (Login, ChangePassword) and any other VM with no MAUI deps live in `Gymers.Core` and are unit-tested in `Gymers.Tests`. VMs that use MAUI APIs (`FilePicker`, `MediaPicker`, `Launcher`, `DisplayAlert`, `QueryProperty`) live in `Gymers/` and are validated via the manual smoke checklist in Phase 16 — matches the spec's `"ViewModels: tests after the page works in dev"` guidance.

---

## Phase 1 — Foundation

Goal: a working .NET MAUI Mac Catalyst project that opens an empty window with the app theme loaded and a test project that runs.

### Task 1.1 — Verify .NET SDK + MAUI workload

**Files:** none (environment check)

- [ ] **Step 1: Check .NET SDK is installed**

```bash
dotnet --version
```

Expected: `8.0.x` or higher. If missing, install from https://dotnet.microsoft.com/download/dotnet/8.0.

- [ ] **Step 2: Install/verify MAUI workload**

```bash
dotnet workload install maui
dotnet workload list
```

Expected: `maui` listed.

- [ ] **Step 3: Verify Xcode command-line tools (needed for Mac Catalyst)**

```bash
xcode-select -p
```

Expected: a path like `/Applications/Xcode.app/Contents/Developer` or `/Library/Developer/CommandLineTools`. If not, run `xcode-select --install`.

### Task 1.2 — Create solution and projects

**Files:**
- Create: `Gymers.sln`, `Gymers/Gymers.csproj`, `Gymers.Core/Gymers.Core.csproj`, `Gymers.Tests/Gymers.Tests.csproj`

- [ ] **Step 1: Create the MAUI app**

```bash
cd /Users/michaelthomasm.gonzaga/Downloads/Gymers/Team-Saniel-Figma
dotnet new maui -n Gymers -o Gymers
```

- [ ] **Step 2: Trim the MAUI project to Mac Catalyst only**

Open `Gymers/Gymers.csproj`. Inside the first `<PropertyGroup>`:

1. Replace the `<TargetFrameworks>...</TargetFrameworks>` line with:
   ```xml
   <TargetFramework>net8.0-maccatalyst</TargetFramework>
   ```
2. Delete the `<TargetFrameworks>...$(TargetFrameworks);net8.0-windows...` conditional block (Windows TFM, only present when generated on Windows — harmless to delete on Mac).
3. Make sure these single-platform values remain:
   ```xml
   <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'maccatalyst'">14.0</SupportedOSPlatformVersion>
   <OutputType>Exe</OutputType>
   <RootNamespace>Gymers</RootNamespace>
   <UseMaui>true</UseMaui>
   ```
4. Delete the entire `Platforms/Android/`, `Platforms/iOS/`, `Platforms/Tizen/`, and `Platforms/Windows/` folders the MAUI template generated. Keep only `Platforms/MacCatalyst/`.

```bash
rm -rf Gymers/Platforms/Android Gymers/Platforms/iOS Gymers/Platforms/Tizen Gymers/Platforms/Windows
```

- [ ] **Step 3: Create the `Gymers.Core` class library** (net8.0, no MAUI)

```bash
dotnet new classlib -n Gymers.Core -o Gymers.Core --framework net8.0
rm Gymers.Core/Class1.cs
```

Edit `Gymers.Core/Gymers.Core.csproj` so the `<PropertyGroup>` looks like:

```xml
<PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Gymers</RootNamespace>
</PropertyGroup>
```

`<RootNamespace>Gymers</RootNamespace>` is intentional — files in `Gymers.Core/Models/` will live in the `Gymers.Models` namespace, matching how the MAUI project would declare them.

- [ ] **Step 4: Have the MAUI app reference `Gymers.Core`**

```bash
dotnet add Gymers/Gymers.csproj reference Gymers.Core/Gymers.Core.csproj
```

- [ ] **Step 5: Create the xUnit test project** (net8.0, references `Gymers.Core` only)

```bash
dotnet new xunit -n Gymers.Tests -o Gymers.Tests --framework net8.0
rm Gymers.Tests/UnitTest1.cs
dotnet add Gymers.Tests/Gymers.Tests.csproj reference Gymers.Core/Gymers.Core.csproj
```

`Gymers.Tests` does **not** reference `Gymers` — that would re-introduce the TFM mismatch. ViewModels (which live in `Gymers/`) are covered by manual smoke testing in Phase 16, not unit tests.

- [ ] **Step 6: Create the solution and add all three projects**

```bash
dotnet new sln -n Gymers
dotnet sln add Gymers/Gymers.csproj
dotnet sln add Gymers.Core/Gymers.Core.csproj
dotnet sln add Gymers.Tests/Gymers.Tests.csproj
```

- [ ] **Step 7: Build to verify**

```bash
dotnet build
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. (The MAUI default template's `MainPage.xaml` may show informational hints about deleted platforms — those are not errors.)

- [ ] **Step 8: Commit**

```bash
git add Gymers.sln Gymers/ Gymers.Core/ Gymers.Tests/
git commit -m "chore: scaffold MAUI app + Gymers.Core library + xunit tests (Mac Catalyst only)"
```

### Task 1.3 — Add NuGet packages

**Files:** modify `Gymers/Gymers.csproj`, `Gymers.Core/Gymers.Core.csproj`, `Gymers.Tests/Gymers.Tests.csproj`

Packages are split by project so the test project pulls only what it needs (and never anything MAUI-specific).

- [ ] **Step 1: Add Core library packages** (data, hashing, PDF, services)

```bash
cd Gymers.Core
dotnet add package sqlite-net-pcl --version 1.9.172
dotnet add package SQLitePCLRaw.bundle_green --version 2.1.10
dotnet add package BCrypt.Net-Next --version 4.0.3
dotnet add package QuestPDF --version 2024.10.3
dotnet add package CommunityToolkit.Mvvm --version 8.3.2
cd ..
```

`CommunityToolkit.Mvvm` is in Core because `BaseViewModel` lives in Gymers (MAUI) but a few service classes (e.g., `UserSession`) raise events; we add the package here so simple `ObservableObject` helpers are available if needed. (If unused after Phase 4, you can drop it.)

- [ ] **Step 2: Add MAUI app packages** (UI helpers + logging — note `sqlite-net-pcl` etc. flow through the project reference to Core)

```bash
cd Gymers
dotnet add package CommunityToolkit.Mvvm --version 8.3.2
dotnet add package CommunityToolkit.Maui --version 9.1.0
dotnet add package Serilog --version 4.0.2
dotnet add package Serilog.Extensions.Logging --version 8.0.0
dotnet add package Serilog.Sinks.File --version 6.0.0
cd ..
```

- [ ] **Step 3: Add test project packages**

```bash
cd Gymers.Tests
dotnet add package NSubstitute --version 5.1.0
dotnet add package Microsoft.NET.Test.Sdk --version 17.11.1
cd ..
```

`sqlite-net-pcl` and `BCrypt.Net-Next` are *not* added here — they flow in transitively from the `Gymers.Core` project reference.

- [ ] **Step 4: Verify the build still passes**

```bash
dotnet build
```

Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add Gymers/Gymers.csproj Gymers.Core/Gymers.Core.csproj Gymers.Tests/Gymers.Tests.csproj
git commit -m "chore: add nuget dependencies split across Core, MAUI app, and tests"
```

### Task 1.4 — Configure QuestPDF community license

**Files:** modify `Gymers/MauiProgram.cs`

QuestPDF requires explicit license declaration on first use.

- [ ] **Step 1: Add license declaration in `MauiProgram.cs`**

At the top of `CreateMauiApp()` (before any other code), add:

```csharp
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
```

Add `using QuestPDF;` at the top of the file as needed.

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

- [ ] **Step 3: Commit**

```bash
git add Gymers/MauiProgram.cs
git commit -m "chore: declare QuestPDF community license"
```

### Task 1.5 — Folder skeleton + theme placeholders

**Files:**
- Create: `Gymers.Core/Models/`, `Gymers.Core/Data/`, `Gymers.Core/Data/Repositories/`, `Gymers.Core/Services/`, `Gymers.Core/ViewModels/`, `Gymers.Core/ViewModels/Auth/`
- Create: `Gymers/Services/`, `Gymers/ViewModels/`, `Gymers/ViewModels/Admin/`, `Gymers/ViewModels/Staff/`, `Gymers/Views/Auth/`, `Gymers/Views/Admin/`, `Gymers/Views/Staff/`, `Gymers/Views/Shared/`, `Gymers/Converters/`
- Create: `Gymers/Resources/Styles/Colors.xaml`, `Gymers/Resources/Styles/Styles.xaml`
- Modify: `Gymers/App.xaml`

- [ ] **Step 1: Create folder skeleton**

```bash
cd Gymers.Core
mkdir -p Models Data Data/Repositories Services ViewModels/Auth
cd ..

cd Gymers
mkdir -p Services
mkdir -p ViewModels/Admin ViewModels/Staff
mkdir -p Views/Auth Views/Admin Views/Staff Views/Shared
mkdir -p Converters
mkdir -p Resources/Styles
cd ..
```

- [ ] **Step 2: Write `Resources/Styles/Colors.xaml`**

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<ResourceDictionary xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
    <Color x:Key="Primary">#1A2A4A</Color>
    <Color x:Key="Accent">#3CD278</Color>
    <Color x:Key="Background">#FFFFFF</Color>
    <Color x:Key="Surface">#F8F9FB</Color>
    <Color x:Key="TextPrimary">#111111</Color>
    <Color x:Key="TextMuted">#6B7280</Color>
    <Color x:Key="Border">#E5E7EB</Color>
    <Color x:Key="Danger">#E5484D</Color>
    <Color x:Key="Success">#16A34A</Color>
</ResourceDictionary>
```

- [ ] **Step 3: Write `Resources/Styles/Styles.xaml`**

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<ResourceDictionary xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">

    <Style TargetType="ContentPage" ApplyToDerivedTypes="True">
        <Setter Property="BackgroundColor" Value="{StaticResource Background}" />
    </Style>

    <Style TargetType="Label" x:Key="H1">
        <Setter Property="FontSize" Value="28" />
        <Setter Property="FontAttributes" Value="Bold" />
        <Setter Property="TextColor" Value="{StaticResource TextPrimary}" />
    </Style>

    <Style TargetType="Label" x:Key="H2">
        <Setter Property="FontSize" Value="20" />
        <Setter Property="FontAttributes" Value="Bold" />
        <Setter Property="TextColor" Value="{StaticResource TextPrimary}" />
    </Style>

    <Style TargetType="Label" x:Key="Body">
        <Setter Property="FontSize" Value="14" />
        <Setter Property="TextColor" Value="{StaticResource TextPrimary}" />
    </Style>

    <Style TargetType="Label" x:Key="Muted">
        <Setter Property="FontSize" Value="12" />
        <Setter Property="TextColor" Value="{StaticResource TextMuted}" />
    </Style>

    <Style TargetType="Button" x:Key="PrimaryButton">
        <Setter Property="BackgroundColor" Value="{StaticResource Primary}" />
        <Setter Property="TextColor" Value="White" />
        <Setter Property="FontAttributes" Value="Bold" />
        <Setter Property="CornerRadius" Value="8" />
        <Setter Property="HeightRequest" Value="44" />
    </Style>

    <Style TargetType="Button" x:Key="AccentButton">
        <Setter Property="BackgroundColor" Value="{StaticResource Accent}" />
        <Setter Property="TextColor" Value="White" />
        <Setter Property="FontAttributes" Value="Bold" />
        <Setter Property="CornerRadius" Value="8" />
        <Setter Property="HeightRequest" Value="44" />
    </Style>

    <Style TargetType="Border" x:Key="Card">
        <Setter Property="BackgroundColor" Value="{StaticResource Surface}" />
        <Setter Property="StrokeShape" Value="RoundRectangle 12" />
        <Setter Property="Stroke" Value="{StaticResource Border}" />
        <Setter Property="StrokeThickness" Value="1" />
        <Setter Property="Padding" Value="16" />
    </Style>

    <Style TargetType="Entry">
        <Setter Property="BackgroundColor" Value="White" />
        <Setter Property="TextColor" Value="{StaticResource TextPrimary}" />
        <Setter Property="FontSize" Value="14" />
        <Setter Property="HeightRequest" Value="44" />
    </Style>

</ResourceDictionary>
```

- [ ] **Step 4: Wire styles into `App.xaml`**

Replace the existing `<Application.Resources>` block with:

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="Resources/Styles/Colors.xaml" />
            <ResourceDictionary Source="Resources/Styles/Styles.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

- [ ] **Step 5: Build and run on Mac Catalyst**

```bash
dotnet build -t:Run -f net8.0-maccatalyst
```

Expected: a window opens showing the default MAUI welcome page styled with the new background color.

- [ ] **Step 6: Commit**

```bash
git add Gymers.Core Gymers/Resources Gymers/App.xaml Gymers/Services Gymers/ViewModels Gymers/Views Gymers/Converters
git commit -m "chore: scaffold folder layout + theme placeholders"
```

---

## Phase 2 — Models & Enums

### Task 2.1 — Define enums

**Files:**
- Create: `Gymers.Core/Models/Enums.cs`

- [ ] **Step 1: Write enum definitions**

```csharp
namespace Gymers.Models;

public enum Role { Admin, Staff }

public enum MembershipType { Monthly, Quarterly, Annual }

public enum MemberStatus { Active, Inactive, Expired }

public enum PaymentType { NewMembership, Renewal, WalkIn, PersonalTraining, Other }

public enum PaymentMethod { Cash, GCash, Card, BankTransfer }

public enum EquipmentCategory { Cardio, Strength, FreeWeights, Other }

public enum EquipmentStatus { Available, InUse, Maintenance, OutOfOrder }

public enum EquipmentCondition { Excellent, Good, Fair, Poor }
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build
```

Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add Gymers.Core/Models/Enums.cs
git commit -m "feat: add domain enums"
```

### Task 2.2 — Define entity models

**Files:**
- Create: `Gymers.Core/Models/User.cs`, `Member.cs`, `Trainer.cs`, `TrainerAssignment.cs`, `Payment.cs`, `Attendance.cs`, `Exercise.cs`, `WorkoutPlan.cs`, `WorkoutPlanExercise.cs`, `Equipment.cs`

`sqlite-net-pcl` requires `[PrimaryKey]`, `[AutoIncrement]`, `[Indexed]` attributes from `SQLite` namespace. Enums are stored as TEXT via the `[StoreAsText]` attribute on enum properties.

- [ ] **Step 1: `User.cs`**

```csharp
using SQLite;

namespace Gymers.Models;

public class User
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Unique = true)]
    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    [StoreAsText]
    public Role Role { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool MustChangePassword { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: `Member.cs`**

```csharp
using SQLite;

namespace Gymers.Models;

public class Member
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? PhotoPath { get; set; }

    [StoreAsText]
    public MembershipType MembershipType { get; set; }

    public DateTime MembershipStartDate { get; set; }
    public DateTime MembershipEndDate { get; set; }

    [StoreAsText, Indexed]
    public MemberStatus Status { get; set; }

    public string? Notes { get; set; }

    [Indexed]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 3: `Trainer.cs`**

```csharp
using SQLite;

namespace Gymers.Models;

public class Trainer
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public string? Specialization { get; set; }
    public string? AvailabilityNotes { get; set; }
    public decimal HourlyRate { get; set; }
    public DateTime? HireDate { get; set; }
    public string? PhotoPath { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 4: `TrainerAssignment.cs`**

```csharp
using SQLite;

namespace Gymers.Models;

public class TrainerAssignment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int TrainerId { get; set; }

    [Indexed]
    public int MemberId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Notes { get; set; }
}
```

- [ ] **Step 5: `Payment.cs`**

```csharp
using SQLite;

namespace Gymers.Models;

public class Payment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int MemberId { get; set; }

    public decimal Amount { get; set; }

    [StoreAsText]
    public PaymentType PaymentType { get; set; }

    [StoreAsText]
    public PaymentMethod PaymentMethod { get; set; }

    [Indexed(Unique = true)]
    public string ReceiptNumber { get; set; } = string.Empty;

    public string? ReceiptPdfPath { get; set; }
    public int ProcessedByUserId { get; set; }

    [Indexed]
    public DateTime ProcessedAt { get; set; }

    public string? Notes { get; set; }
}
```

- [ ] **Step 6: `Attendance.cs`**

```csharp
using SQLite;

namespace Gymers.Models;

public class Attendance
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int MemberId { get; set; }

    [Indexed]
    public DateTime CheckInTime { get; set; }

    public DateTime? CheckOutTime { get; set; }
    public int ProcessedByUserId { get; set; }
}
```

- [ ] **Step 7: `Exercise.cs`**

```csharp
using SQLite;

namespace Gymers.Models;

public class Exercise
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Unique = true)]
    public string Name { get; set; } = string.Empty;

    public string? MuscleGroup { get; set; }
    public string? Description { get; set; }
}
```

- [ ] **Step 8: `WorkoutPlan.cs`**

```csharp
using SQLite;

namespace Gymers.Models;

public class WorkoutPlan
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    [Indexed]
    public bool IsTemplate { get; set; }

    [Indexed]
    public int? AssignedToMemberId { get; set; }

    public int? AssignedByTrainerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 9: `WorkoutPlanExercise.cs`**

```csharp
using SQLite;

namespace Gymers.Models;

public class WorkoutPlanExercise
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WorkoutPlanId { get; set; }

    public int ExerciseId { get; set; }
    public int Sets { get; set; }
    public int Reps { get; set; }
    public decimal? Weight { get; set; }
    public int Order { get; set; }
    public string? Notes { get; set; }
}
```

- [ ] **Step 10: `Equipment.cs`**

```csharp
using SQLite;

namespace Gymers.Models;

public class Equipment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }

    [StoreAsText]
    public EquipmentCategory Category { get; set; }

    [StoreAsText, Indexed]
    public EquipmentStatus Status { get; set; }

    [StoreAsText]
    public EquipmentCondition Condition { get; set; }

    public DateTime? PurchaseDate { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }

    [Indexed]
    public DateTime? NextMaintenanceDate { get; set; }

    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}
```

- [ ] **Step 11: Build**

```bash
dotnet build
```

Expected: `Build succeeded`.

- [ ] **Step 12: Commit**

```bash
git add Gymers.Core/Models/
git commit -m "feat: add entity models with sqlite-net attributes"
```

---

## Phase 3 — Database Layer (TDD)

### Task 3.1 — Test infrastructure

**Files:**
- Create: `Gymers.Tests/TestHelpers/TempDatabaseFixture.cs`, `Gymers.Tests/TestHelpers/FakeClock.cs`

`TempDatabaseFixture` creates a SQLite file in `Path.GetTempPath()` with a unique name, runs `GymDatabase.InitAsync` against it, exposes the connection, and deletes the file in `Dispose`. `FakeClock` is an `IClock` whose `UtcNow` is settable.

- [ ] **Step 1: Write `FakeClock.cs`**

```csharp
using Gymers.Services;

namespace Gymers.Tests.TestHelpers;

public class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
    public DateTime LocalNow => UtcNow.ToLocalTime();
    public DateOnly Today => DateOnly.FromDateTime(UtcNow);
}
```

- [ ] **Step 2: Write `TempDatabaseFixture.cs`**

```csharp
using Gymers.Data;
using SQLite;

namespace Gymers.Tests.TestHelpers;

public sealed class TempDatabaseFixture : IDisposable
{
    public string DbPath { get; }
    public GymDatabase Database { get; }
    public SQLiteAsyncConnection Connection => Database.Connection;

    public TempDatabaseFixture()
    {
        DbPath = Path.Combine(Path.GetTempPath(), $"gymers-test-{Guid.NewGuid():N}.db3");
        Database = new GymDatabase(DbPath);
        Database.InitAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        Connection.CloseAsync().GetAwaiter().GetResult();
        if (File.Exists(DbPath)) File.Delete(DbPath);
    }
}
```

`IClock` and `GymDatabase` don't exist yet — that's fine, tests will fail to compile until those types are written in Tasks 3.2 and 3.3 respectively. The helpers compile cleanly only after Task 3.3.

- [ ] **Step 3: Don't build yet (will fail until 3.3). Commit the helpers anyway.**

```bash
git add Gymers.Tests/TestHelpers/
git commit -m "test: add TempDatabaseFixture + FakeClock helpers"
```

### Task 3.2 — `IClock` and `SystemClock`

**Files:** Create: `Gymers.Core/Services/IClock.cs`, `Gymers.Core/Services/SystemClock.cs`

- [ ] **Step 1: `IClock.cs`**

```csharp
namespace Gymers.Services;

public interface IClock
{
    DateTime UtcNow { get; }
    DateTime LocalNow { get; }
    DateOnly Today { get; }
}
```

- [ ] **Step 2: `SystemClock.cs`**

```csharp
namespace Gymers.Services;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime LocalNow => DateTime.Now;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}
```

- [ ] **Step 3: Build**

```bash
dotnet build
```

Expected: success (the test project still won't compile because `GymDatabase` is missing — that's fine).

- [ ] **Step 4: Commit**

```bash
git add Gymers.Core/Services/IClock.cs Gymers.Core/Services/SystemClock.cs
git commit -m "feat: add IClock + SystemClock"
```

### Task 3.3 — `GymDatabase` init and table creation (TDD)

**Files:**
- Create: `Gymers.Core/Data/GymDatabase.cs`
- Create: `Gymers.Tests/Data/GymDatabaseTests.cs`

`GymDatabase` wraps `SQLiteAsyncConnection`. `InitAsync` opens the connection, runs `PRAGMA foreign_keys = ON`, creates all tables, **creates the composite indexes that `[Indexed]` attributes can't express**, and seeds the default admin user if the Users table is empty.

`sqlite-net-pcl`'s `[Indexed]` attribute creates only single-column indexes. The spec defines composite indexes (e.g., `(Status, IsActive)`, `(MemberId, ProcessedAt DESC)`) that we must create with raw `CREATE INDEX IF NOT EXISTS` statements after `CreateTablesAsync`.

- [ ] **Step 1: Write the failing test `GymDatabaseTests.cs`**

```csharp
using Gymers.Models;
using Gymers.Tests.TestHelpers;

namespace Gymers.Tests.Data;

public class GymDatabaseTests
{
    [Fact]
    public async Task InitAsync_CreatesAllTables_AndSeedsDefaultAdmin()
    {
        using var fixture = new TempDatabaseFixture();

        var users = await fixture.Connection.Table<User>().ToListAsync();

        Assert.Single(users);
        Assert.Equal("admin", users[0].Username);
        Assert.Equal(Role.Admin, users[0].Role);
        Assert.True(users[0].MustChangePassword);
    }

    [Fact]
    public async Task InitAsync_EnablesForeignKeys()
    {
        using var fixture = new TempDatabaseFixture();
        var pragma = await fixture.Connection.ExecuteScalarAsync<int>("PRAGMA foreign_keys;");
        Assert.Equal(1, pragma);
    }

    [Fact]
    public async Task InitAsync_IsIdempotent()
    {
        using var fixture = new TempDatabaseFixture();
        await fixture.Database.InitAsync(); // second call

        var userCount = await fixture.Connection.Table<User>().CountAsync();
        Assert.Equal(1, userCount); // still just the seeded admin
    }

    [Fact]
    public async Task InitAsync_CreatesAllCompositeIndexes()
    {
        using var fixture = new TempDatabaseFixture();

        // sqlite_master holds all CREATE INDEX statements; we expect every named composite index to be present.
        var expected = new[]
        {
            "idx_members_status",
            "idx_assignments_member",
            "idx_payments_member",
            "idx_attendance_member",
            "idx_planex_plan",
            "idx_equipment_status"
        };

        foreach (var name in expected)
        {
            var found = await fixture.Connection.ExecuteScalarAsync<string?>(
                "SELECT name FROM sqlite_master WHERE type='index' AND name=?", name);
            Assert.Equal(name, found);
        }
    }
}
```

- [ ] **Step 2: Run test, verify compile failure (`GymDatabase` missing)**

```bash
dotnet test
```

Expected: compilation error: `The type or namespace name 'GymDatabase' could not be found`.

- [ ] **Step 3: Write `GymDatabase.cs`**

```csharp
using BCrypt.Net;
using Gymers.Models;
using SQLite;

namespace Gymers.Data;

public class GymDatabase
{
    public SQLiteAsyncConnection Connection { get; }

    public GymDatabase(string dbPath)
    {
        Connection = new SQLiteAsyncConnection(dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
    }

    public async Task InitAsync()
    {
        await Connection.ExecuteAsync("PRAGMA foreign_keys = ON;");

        await Connection.CreateTablesAsync(CreateFlags.None,
            typeof(User), typeof(Member), typeof(Trainer), typeof(TrainerAssignment),
            typeof(Payment), typeof(Attendance), typeof(Exercise),
            typeof(WorkoutPlan), typeof(WorkoutPlanExercise), typeof(Equipment));

        await CreateCompositeIndexesAsync();
        await SeedDefaultAdminAsync();
    }

    private async Task CreateCompositeIndexesAsync()
    {
        // sqlite-net-pcl's [Indexed] attribute only creates single-column indexes.
        // Composite indexes from the spec must be issued as raw SQL.
        // Table names default to the class name (singular) — match the [Table] convention used by sqlite-net-pcl.
        var statements = new[]
        {
            "CREATE INDEX IF NOT EXISTS idx_members_status ON Member (Status, IsActive);",
            "CREATE INDEX IF NOT EXISTS idx_assignments_member ON TrainerAssignment (MemberId, StartDate DESC);",
            "CREATE INDEX IF NOT EXISTS idx_payments_member ON Payment (MemberId, ProcessedAt DESC);",
            "CREATE INDEX IF NOT EXISTS idx_attendance_member ON Attendance (MemberId, CheckInTime DESC);",
            "CREATE INDEX IF NOT EXISTS idx_planex_plan ON WorkoutPlanExercise (WorkoutPlanId, \"Order\");",
            "CREATE INDEX IF NOT EXISTS idx_equipment_status ON Equipment (Status, IsActive);"
        };

        foreach (var sql in statements)
            await Connection.ExecuteAsync(sql);
    }

    private async Task SeedDefaultAdminAsync()
    {
        var hasAny = await Connection.Table<User>().CountAsync() > 0;
        if (hasAny) return;

        var admin = new User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = Role.Admin,
            FullName = "Default Admin",
            MustChangePassword = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await Connection.InsertAsync(admin);
    }
}
```

- [ ] **Step 4: Run tests, verify pass**

```bash
dotnet test
```

Expected: 4 tests passed (the three originals plus `InitAsync_CreatesAllCompositeIndexes`).

- [ ] **Step 5: Commit**

```bash
git add Gymers.Core/Data/GymDatabase.cs Gymers.Tests/Data/GymDatabaseTests.cs
git commit -m "feat: add GymDatabase with tables + composite indexes + admin seed (TDD)"
```

### Task 3.4 — `UserRepository` (TDD)

**Files:**
- Create: `Gymers.Core/Data/Repositories/IUserRepository.cs`, `UserRepository.cs`
- Create: `Gymers.Tests/Data/UserRepositoryTests.cs`

- [ ] **Step 1: Write failing tests `UserRepositoryTests.cs`**

```csharp
using BCrypt.Net;
using Gymers.Data.Repositories;
using Gymers.Models;
using Gymers.Tests.TestHelpers;

namespace Gymers.Tests.Data;

public class UserRepositoryTests
{
    [Fact]
    public async Task GetByUsernameAsync_ExistingUser_ReturnsUser()
    {
        using var fx = new TempDatabaseFixture();
        var repo = new UserRepository(fx.Database);

        var user = await repo.GetByUsernameAsync("admin");

        Assert.NotNull(user);
        Assert.Equal("admin", user!.Username);
    }

    [Fact]
    public async Task GetByUsernameAsync_Missing_ReturnsNull()
    {
        using var fx = new TempDatabaseFixture();
        var repo = new UserRepository(fx.Database);

        var user = await repo.GetByUsernameAsync("nobody");

        Assert.Null(user);
    }

    [Fact]
    public async Task InsertAsync_NewUser_AssignsId()
    {
        using var fx = new TempDatabaseFixture();
        var repo = new UserRepository(fx.Database);

        var u = new User
        {
            Username = "alice",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("pw"),
            Role = Role.Staff,
            FullName = "Alice"
        };
        await repo.InsertAsync(u);

        Assert.True(u.Id > 0);
    }

    [Fact]
    public async Task InsertAsync_DuplicateUsername_Throws()
    {
        using var fx = new TempDatabaseFixture();
        var repo = new UserRepository(fx.Database);

        var u = new User { Username = "admin", PasswordHash = "x", Role = Role.Admin, FullName = "x" };

        await Assert.ThrowsAsync<SQLite.SQLiteException>(() => repo.InsertAsync(u));
    }

    [Fact]
    public async Task ListActiveAsync_ReturnsOnlyActive()
    {
        using var fx = new TempDatabaseFixture();
        var repo = new UserRepository(fx.Database);

        await repo.InsertAsync(new User { Username = "a", PasswordHash = "x", Role = Role.Staff, FullName = "A", IsActive = true });
        await repo.InsertAsync(new User { Username = "b", PasswordHash = "x", Role = Role.Staff, FullName = "B", IsActive = false });

        var active = await repo.ListActiveAsync();

        Assert.Equal(2, active.Count); // admin + a
        Assert.DoesNotContain(active, x => x.Username == "b");
    }
}
```

- [ ] **Step 2: Run, verify compile failures**

```bash
dotnet test --filter UserRepositoryTests
```

Expected: compile errors (`UserRepository` missing).

- [ ] **Step 3: Write `IUserRepository.cs`**

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByIdAsync(int id);
    Task InsertAsync(User user);
    Task UpdateAsync(User user);
    Task<List<User>> ListActiveAsync();
    Task<List<User>> ListByRoleAsync(Role role, bool includeInactive = false);
    Task DeactivateAsync(int id);
}
```

- [ ] **Step 4: Write `UserRepository.cs`**

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly GymDatabase _db;
    public UserRepository(GymDatabase db) => _db = db;

    public Task<User?> GetByUsernameAsync(string username) =>
        _db.Connection.Table<User>().Where(u => u.Username == username).FirstOrDefaultAsync()!;

    public Task<User?> GetByIdAsync(int id) =>
        _db.Connection.Table<User>().Where(u => u.Id == id).FirstOrDefaultAsync()!;

    public async Task InsertAsync(User user)
    {
        user.CreatedAt = DateTime.UtcNow;
        await _db.Connection.InsertAsync(user);
    }

    public Task UpdateAsync(User user) => _db.Connection.UpdateAsync(user);

    public async Task<List<User>> ListActiveAsync() =>
        await _db.Connection.Table<User>().Where(u => u.IsActive).OrderBy(u => u.Username).ToListAsync();

    public async Task<List<User>> ListByRoleAsync(Role role, bool includeInactive = false)
    {
        var q = _db.Connection.Table<User>().Where(u => u.Role == role);
        if (!includeInactive) q = q.Where(u => u.IsActive);
        return await q.OrderBy(u => u.Username).ToListAsync();
    }

    public async Task DeactivateAsync(int id)
    {
        var u = await GetByIdAsync(id);
        if (u == null) return;
        u.IsActive = false;
        await UpdateAsync(u);
    }
}
```

- [ ] **Step 5: Run tests, verify pass**

```bash
dotnet test --filter UserRepositoryTests
```

Expected: 5 tests passed.

- [ ] **Step 6: Commit**

```bash
git add Gymers.Core/Data/Repositories/IUserRepository.cs Gymers.Core/Data/Repositories/UserRepository.cs Gymers.Tests/Data/UserRepositoryTests.cs
git commit -m "feat: add UserRepository (TDD)"
```

### Task 3.5 — `MemberRepository` (TDD)

**Files:**
- Create: `Gymers.Core/Data/Repositories/IMemberRepository.cs`, `MemberRepository.cs`
- Create: `Gymers.Tests/Data/MemberRepositoryTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Gymers.Data.Repositories;
using Gymers.Models;
using Gymers.Tests.TestHelpers;

namespace Gymers.Tests.Data;

public class MemberRepositoryTests
{
    private static Member NewMember(string name = "Juan dela Cruz") => new()
    {
        FullName = name,
        ContactNumber = "09123456789",
        MembershipType = MembershipType.Monthly,
        MembershipStartDate = new DateTime(2026, 5, 1),
        MembershipEndDate = new DateTime(2026, 6, 1),
        Status = MemberStatus.Active,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task InsertAsync_AssignsId_AndCanBeFetched()
    {
        using var fx = new TempDatabaseFixture();
        var repo = new MemberRepository(fx.Database);

        var m = NewMember();
        await repo.InsertAsync(m);

        Assert.True(m.Id > 0);
        var fetched = await repo.GetByIdAsync(m.Id);
        Assert.Equal("Juan dela Cruz", fetched!.FullName);
    }

    [Fact]
    public async Task SearchActiveAsync_FiltersByNameOrContact()
    {
        using var fx = new TempDatabaseFixture();
        var repo = new MemberRepository(fx.Database);
        await repo.InsertAsync(NewMember("Maria Santos"));
        await repo.InsertAsync(NewMember("Juan Reyes"));

        var byName = await repo.SearchActiveAsync("santos");
        Assert.Single(byName);
        Assert.Equal("Maria Santos", byName[0].FullName);
    }

    [Fact]
    public async Task ListExpiringWithinAsync_ReturnsMembersExpiringSoon()
    {
        using var fx = new TempDatabaseFixture();
        var repo = new MemberRepository(fx.Database);
        var soon = NewMember("Soon Expire");
        soon.MembershipEndDate = new DateTime(2026, 5, 5);
        var far = NewMember("Far Expire");
        far.MembershipEndDate = new DateTime(2026, 12, 31);
        await repo.InsertAsync(soon);
        await repo.InsertAsync(far);

        var expiring = await repo.ListExpiringWithinAsync(new DateTime(2026, 5, 1), new DateTime(2026, 5, 7));

        Assert.Single(expiring);
        Assert.Equal("Soon Expire", expiring[0].FullName);
    }

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveFalse_AndExcludesFromActiveList()
    {
        using var fx = new TempDatabaseFixture();
        var repo = new MemberRepository(fx.Database);
        var m = NewMember();
        await repo.InsertAsync(m);

        await repo.DeactivateAsync(m.Id);

        var all = await repo.ListActiveAsync();
        Assert.Empty(all);
    }
}
```

- [ ] **Step 2: Run, verify compile failure**

```bash
dotnet test --filter MemberRepositoryTests
```

- [ ] **Step 3: Write `IMemberRepository.cs`**

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public interface IMemberRepository
{
    Task<Member?> GetByIdAsync(int id);
    Task InsertAsync(Member member);
    Task UpdateAsync(Member member);
    Task DeactivateAsync(int id);
    Task<List<Member>> ListActiveAsync();
    Task<List<Member>> ListByStatusAsync(MemberStatus status);
    Task<List<Member>> SearchActiveAsync(string query);
    Task<List<Member>> ListExpiringWithinAsync(DateTime fromInclusive, DateTime toInclusive);
    Task<int> CountActiveAsync();
}
```

- [ ] **Step 4: Write `MemberRepository.cs`**

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public class MemberRepository : IMemberRepository
{
    private readonly GymDatabase _db;
    public MemberRepository(GymDatabase db) => _db = db;

    public Task<Member?> GetByIdAsync(int id) =>
        _db.Connection.Table<Member>().Where(m => m.Id == id).FirstOrDefaultAsync()!;

    public async Task InsertAsync(Member m)
    {
        m.CreatedAt = DateTime.UtcNow;
        m.UpdatedAt = DateTime.UtcNow;
        await _db.Connection.InsertAsync(m);
    }

    public async Task UpdateAsync(Member m)
    {
        m.UpdatedAt = DateTime.UtcNow;
        await _db.Connection.UpdateAsync(m);
    }

    public async Task DeactivateAsync(int id)
    {
        var m = await GetByIdAsync(id);
        if (m == null) return;
        m.IsActive = false;
        await UpdateAsync(m);
    }

    public async Task<List<Member>> ListActiveAsync() =>
        await _db.Connection.Table<Member>()
            .Where(m => m.IsActive)
            .OrderBy(m => m.FullName)
            .ToListAsync();

    public async Task<List<Member>> ListByStatusAsync(MemberStatus status) =>
        await _db.Connection.Table<Member>()
            .Where(m => m.IsActive && m.Status == status)
            .OrderBy(m => m.FullName)
            .ToListAsync();

    public async Task<List<Member>> SearchActiveAsync(string query)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length == 0) return await ListActiveAsync();
        var like = $"%{q}%";
        return await _db.Connection.QueryAsync<Member>(
            "SELECT * FROM Member WHERE IsActive = 1 AND (FullName LIKE ? OR ContactNumber LIKE ?) ORDER BY FullName",
            like, like);
    }

    public async Task<List<Member>> ListExpiringWithinAsync(DateTime fromInclusive, DateTime toInclusive) =>
        await _db.Connection.Table<Member>()
            .Where(m => m.IsActive && m.MembershipEndDate >= fromInclusive && m.MembershipEndDate <= toInclusive)
            .OrderBy(m => m.MembershipEndDate)
            .ToListAsync();

    public Task<int> CountActiveAsync() =>
        _db.Connection.Table<Member>().Where(m => m.IsActive && m.Status == MemberStatus.Active).CountAsync();
}
```

- [ ] **Step 5: Run, verify pass**

```bash
dotnet test --filter MemberRepositoryTests
```

Expected: 4 tests passed.

- [ ] **Step 6: Commit**

```bash
git add Gymers.Core/Data/Repositories/IMemberRepository.cs Gymers.Core/Data/Repositories/MemberRepository.cs Gymers.Tests/Data/MemberRepositoryTests.cs
git commit -m "feat: add MemberRepository with search + expiry queries (TDD)"
```

### Task 3.6 — Remaining repositories (TDD, condensed)

For each repository below, follow the same rhythm: **(a) write the failing test class with at least the listed assertions, (b) run `dotnet test --filter <Name>RepositoryTests` and confirm compile failure, (c) write the interface and implementation shown, (d) run tests and confirm pass, (e) commit.**

#### `TrainerRepository`

`ITrainerRepository.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public interface ITrainerRepository
{
    Task<Trainer?> GetByIdAsync(int id);
    Task InsertAsync(Trainer trainer);
    Task UpdateAsync(Trainer trainer);
    Task DeactivateAsync(int id);
    Task<List<Trainer>> ListActiveAsync();
}
```

`TrainerRepository.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public class TrainerRepository : ITrainerRepository
{
    private readonly GymDatabase _db;
    public TrainerRepository(GymDatabase db) => _db = db;

    public Task<Trainer?> GetByIdAsync(int id) =>
        _db.Connection.Table<Trainer>().Where(t => t.Id == id).FirstOrDefaultAsync()!;

    public async Task InsertAsync(Trainer t)
    {
        t.CreatedAt = DateTime.UtcNow;
        await _db.Connection.InsertAsync(t);
    }

    public Task UpdateAsync(Trainer t) => _db.Connection.UpdateAsync(t);

    public async Task DeactivateAsync(int id)
    {
        var t = await GetByIdAsync(id);
        if (t == null) return;
        t.IsActive = false;
        await UpdateAsync(t);
    }

    public async Task<List<Trainer>> ListActiveAsync() =>
        await _db.Connection.Table<Trainer>().Where(t => t.IsActive).OrderBy(t => t.FullName).ToListAsync();
}
```

`TrainerRepositoryTests.cs` — must cover at minimum: insert assigns id; deactivate excludes from `ListActiveAsync`; update persists changes.

Commit message: `feat: add TrainerRepository (TDD)`.

#### `TrainerAssignmentRepository`

`ITrainerAssignmentRepository.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public interface ITrainerAssignmentRepository
{
    Task InsertAsync(TrainerAssignment assignment);
    Task UpdateAsync(TrainerAssignment assignment);
    Task<TrainerAssignment?> GetCurrentForMemberAsync(int memberId);
    Task<List<TrainerAssignment>> ListForMemberAsync(int memberId);
    Task<List<TrainerAssignment>> ListForTrainerAsync(int trainerId);
    Task EndAssignmentAsync(int id, DateTime endDate);
}
```

`TrainerAssignmentRepository.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public class TrainerAssignmentRepository : ITrainerAssignmentRepository
{
    private readonly GymDatabase _db;
    public TrainerAssignmentRepository(GymDatabase db) => _db = db;

    public Task InsertAsync(TrainerAssignment a) => _db.Connection.InsertAsync(a);
    public Task UpdateAsync(TrainerAssignment a) => _db.Connection.UpdateAsync(a);

    public Task<TrainerAssignment?> GetCurrentForMemberAsync(int memberId) =>
        _db.Connection.Table<TrainerAssignment>()
            .Where(a => a.MemberId == memberId && a.EndDate == null)
            .OrderByDescending(a => a.StartDate)
            .FirstOrDefaultAsync()!;

    public async Task<List<TrainerAssignment>> ListForMemberAsync(int memberId) =>
        await _db.Connection.Table<TrainerAssignment>()
            .Where(a => a.MemberId == memberId)
            .OrderByDescending(a => a.StartDate)
            .ToListAsync();

    public async Task<List<TrainerAssignment>> ListForTrainerAsync(int trainerId) =>
        await _db.Connection.Table<TrainerAssignment>()
            .Where(a => a.TrainerId == trainerId)
            .OrderByDescending(a => a.StartDate)
            .ToListAsync();

    public async Task EndAssignmentAsync(int id, DateTime endDate)
    {
        var a = await _db.Connection.Table<TrainerAssignment>().Where(x => x.Id == id).FirstOrDefaultAsync();
        if (a == null) return;
        a.EndDate = endDate;
        await UpdateAsync(a);
    }
}
```

Tests must cover: insert + retrieve; `GetCurrentForMemberAsync` returns the row with `EndDate IS NULL`; `EndAssignmentAsync` sets EndDate so the assignment no longer appears as current.

Commit: `feat: add TrainerAssignmentRepository (TDD)`.

#### `PaymentRepository`

`IPaymentRepository.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(int id);
    Task InsertAsync(Payment payment);
    Task UpdateAsync(Payment payment);
    Task<List<Payment>> ListForMemberAsync(int memberId);
    Task<List<Payment>> ListBetweenAsync(DateTime fromInclusive, DateTime toExclusive);
    Task<int> GetMaxReceiptCounterForYearAsync(int year);
    Task<decimal> SumAmountBetweenAsync(DateTime fromInclusive, DateTime toExclusive);
}
```

`PaymentRepository.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly GymDatabase _db;
    public PaymentRepository(GymDatabase db) => _db = db;

    public Task<Payment?> GetByIdAsync(int id) =>
        _db.Connection.Table<Payment>().Where(p => p.Id == id).FirstOrDefaultAsync()!;

    public Task InsertAsync(Payment p) => _db.Connection.InsertAsync(p);
    public Task UpdateAsync(Payment p) => _db.Connection.UpdateAsync(p);

    public async Task<List<Payment>> ListForMemberAsync(int memberId) =>
        await _db.Connection.Table<Payment>()
            .Where(p => p.MemberId == memberId)
            .OrderByDescending(p => p.ProcessedAt)
            .ToListAsync();

    public async Task<List<Payment>> ListBetweenAsync(DateTime fromInclusive, DateTime toExclusive) =>
        await _db.Connection.Table<Payment>()
            .Where(p => p.ProcessedAt >= fromInclusive && p.ProcessedAt < toExclusive)
            .OrderByDescending(p => p.ProcessedAt)
            .ToListAsync();

    public async Task<int> GetMaxReceiptCounterForYearAsync(int year)
    {
        var prefix = $"RCPT-{year}-";
        var max = await _db.Connection.ExecuteScalarAsync<string?>(
            "SELECT MAX(ReceiptNumber) FROM Payment WHERE ReceiptNumber LIKE ?", prefix + "%");
        if (string.IsNullOrEmpty(max)) return 0;
        var counterPart = max.Substring(prefix.Length);
        return int.TryParse(counterPart, out var n) ? n : 0;
    }

    public async Task<decimal> SumAmountBetweenAsync(DateTime fromInclusive, DateTime toExclusive)
    {
        var sum = await _db.Connection.ExecuteScalarAsync<decimal?>(
            "SELECT SUM(Amount) FROM Payment WHERE ProcessedAt >= ? AND ProcessedAt < ?",
            fromInclusive, toExclusive);
        return sum ?? 0m;
    }
}
```

Tests must cover: insert + retrieve; `ListForMemberAsync` ordered by date desc; `GetMaxReceiptCounterForYearAsync` returns 0 when none exist and the right number when receipts already exist for that year (insert two payments with `RCPT-2026-0001`, `RCPT-2026-0002`, expect 2); `SumAmountBetweenAsync` returns 0 when range empty and the correct sum when it contains payments.

Commit: `feat: add PaymentRepository with receipt-counter query (TDD)`.

#### `AttendanceRepository`

`IAttendanceRepository.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public interface IAttendanceRepository
{
    Task InsertAsync(Attendance attendance);
    Task UpdateAsync(Attendance attendance);
    Task<List<Attendance>> ListForMemberAsync(int memberId, int limit = 50);
    Task<List<Attendance>> ListBetweenAsync(DateTime fromInclusive, DateTime toExclusive);
    Task<int> CountBetweenAsync(DateTime fromInclusive, DateTime toExclusive);
    Task<DateTime?> GetLastCheckInForMemberAsync(int memberId);
}
```

`AttendanceRepository.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public class AttendanceRepository : IAttendanceRepository
{
    private readonly GymDatabase _db;
    public AttendanceRepository(GymDatabase db) => _db = db;

    public Task InsertAsync(Attendance a) => _db.Connection.InsertAsync(a);
    public Task UpdateAsync(Attendance a) => _db.Connection.UpdateAsync(a);

    public async Task<List<Attendance>> ListForMemberAsync(int memberId, int limit = 50) =>
        await _db.Connection.Table<Attendance>()
            .Where(a => a.MemberId == memberId)
            .OrderByDescending(a => a.CheckInTime)
            .Take(limit)
            .ToListAsync();

    public async Task<List<Attendance>> ListBetweenAsync(DateTime fromInclusive, DateTime toExclusive) =>
        await _db.Connection.Table<Attendance>()
            .Where(a => a.CheckInTime >= fromInclusive && a.CheckInTime < toExclusive)
            .OrderByDescending(a => a.CheckInTime)
            .ToListAsync();

    public Task<int> CountBetweenAsync(DateTime fromInclusive, DateTime toExclusive) =>
        _db.Connection.Table<Attendance>()
            .Where(a => a.CheckInTime >= fromInclusive && a.CheckInTime < toExclusive)
            .CountAsync();

    public async Task<DateTime?> GetLastCheckInForMemberAsync(int memberId)
    {
        var row = await _db.Connection.Table<Attendance>()
            .Where(a => a.MemberId == memberId)
            .OrderByDescending(a => a.CheckInTime)
            .FirstOrDefaultAsync();
        return row?.CheckInTime;
    }
}
```

Tests: insert + list for member; `CountBetweenAsync` correctly bounded by the range; `GetLastCheckInForMemberAsync` returns the most recent check-in.

Commit: `feat: add AttendanceRepository (TDD)`.

#### `ExerciseRepository`, `WorkoutPlanRepository`, `EquipmentRepository`

Each follows the same pattern. Interfaces and implementations:

`IExerciseRepository.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public interface IExerciseRepository
{
    Task<List<Exercise>> ListAllAsync();
    Task<Exercise?> GetByIdAsync(int id);
    Task InsertAsync(Exercise exercise);
    Task UpdateAsync(Exercise exercise);
    Task DeleteAsync(int id);
}
```

`ExerciseRepository.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public class ExerciseRepository : IExerciseRepository
{
    private readonly GymDatabase _db;
    public ExerciseRepository(GymDatabase db) => _db = db;

    public async Task<List<Exercise>> ListAllAsync() =>
        await _db.Connection.Table<Exercise>().OrderBy(e => e.Name).ToListAsync();

    public Task<Exercise?> GetByIdAsync(int id) =>
        _db.Connection.Table<Exercise>().Where(e => e.Id == id).FirstOrDefaultAsync()!;

    public Task InsertAsync(Exercise e) => _db.Connection.InsertAsync(e);
    public Task UpdateAsync(Exercise e) => _db.Connection.UpdateAsync(e);

    public async Task DeleteAsync(int id)
    {
        var e = await GetByIdAsync(id);
        if (e != null) await _db.Connection.DeleteAsync(e);
    }
}
```

`IWorkoutPlanRepository.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public interface IWorkoutPlanRepository
{
    Task<WorkoutPlan?> GetByIdAsync(int id);
    Task<int> InsertAsync(WorkoutPlan plan);
    Task UpdateAsync(WorkoutPlan plan);
    Task DeleteAsync(int id);
    Task<List<WorkoutPlan>> ListTemplatesAsync();
    Task<WorkoutPlan?> GetCurrentForMemberAsync(int memberId);

    Task InsertExerciseAsync(WorkoutPlanExercise wpe);
    Task<List<WorkoutPlanExercise>> ListExercisesForPlanAsync(int planId);
    Task DeleteExercisesForPlanAsync(int planId);
}
```

`WorkoutPlanRepository.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public class WorkoutPlanRepository : IWorkoutPlanRepository
{
    private readonly GymDatabase _db;
    public WorkoutPlanRepository(GymDatabase db) => _db = db;

    public Task<WorkoutPlan?> GetByIdAsync(int id) =>
        _db.Connection.Table<WorkoutPlan>().Where(p => p.Id == id).FirstOrDefaultAsync()!;

    public async Task<int> InsertAsync(WorkoutPlan p)
    {
        p.CreatedAt = DateTime.UtcNow;
        p.UpdatedAt = DateTime.UtcNow;
        await _db.Connection.InsertAsync(p);
        return p.Id;
    }

    public async Task UpdateAsync(WorkoutPlan p)
    {
        p.UpdatedAt = DateTime.UtcNow;
        await _db.Connection.UpdateAsync(p);
    }

    public async Task DeleteAsync(int id)
    {
        await DeleteExercisesForPlanAsync(id);
        await _db.Connection.ExecuteAsync("DELETE FROM WorkoutPlan WHERE Id = ?", id);
    }

    public async Task<List<WorkoutPlan>> ListTemplatesAsync() =>
        await _db.Connection.Table<WorkoutPlan>()
            .Where(p => p.IsTemplate)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();

    public Task<WorkoutPlan?> GetCurrentForMemberAsync(int memberId) =>
        _db.Connection.Table<WorkoutPlan>()
            .Where(p => !p.IsTemplate && p.AssignedToMemberId == memberId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync()!;

    public Task InsertExerciseAsync(WorkoutPlanExercise wpe) =>
        _db.Connection.InsertAsync(wpe);

    public async Task<List<WorkoutPlanExercise>> ListExercisesForPlanAsync(int planId) =>
        await _db.Connection.Table<WorkoutPlanExercise>()
            .Where(x => x.WorkoutPlanId == planId)
            .OrderBy(x => x.Order)
            .ToListAsync();

    public Task DeleteExercisesForPlanAsync(int planId) =>
        _db.Connection.ExecuteAsync("DELETE FROM WorkoutPlanExercise WHERE WorkoutPlanId = ?", planId);
}
```

`IEquipmentRepository.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public interface IEquipmentRepository
{
    Task<Equipment?> GetByIdAsync(int id);
    Task InsertAsync(Equipment equipment);
    Task UpdateAsync(Equipment equipment);
    Task DeactivateAsync(int id);
    Task<List<Equipment>> ListActiveAsync();
    Task<List<Equipment>> ListDueForMaintenanceAsync(DateTime byDateInclusive);
}
```

`EquipmentRepository.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Data.Repositories;

public class EquipmentRepository : IEquipmentRepository
{
    private readonly GymDatabase _db;
    public EquipmentRepository(GymDatabase db) => _db = db;

    public Task<Equipment?> GetByIdAsync(int id) =>
        _db.Connection.Table<Equipment>().Where(e => e.Id == id).FirstOrDefaultAsync()!;

    public Task InsertAsync(Equipment e) => _db.Connection.InsertAsync(e);
    public Task UpdateAsync(Equipment e) => _db.Connection.UpdateAsync(e);

    public async Task DeactivateAsync(int id)
    {
        var e = await GetByIdAsync(id);
        if (e == null) return;
        e.IsActive = false;
        await UpdateAsync(e);
    }

    public async Task<List<Equipment>> ListActiveAsync() =>
        await _db.Connection.Table<Equipment>()
            .Where(e => e.IsActive)
            .OrderBy(e => e.Name)
            .ToListAsync();

    public async Task<List<Equipment>> ListDueForMaintenanceAsync(DateTime byDateInclusive) =>
        await _db.Connection.Table<Equipment>()
            .Where(e => e.IsActive && e.NextMaintenanceDate != null && e.NextMaintenanceDate <= byDateInclusive)
            .OrderBy(e => e.NextMaintenanceDate)
            .ToListAsync();
}
```

For each: write a Tests class with at least one happy-path test for each public method. Run, watch fail, implement, run, watch pass, commit individually:
- `feat: add ExerciseRepository (TDD)`
- `feat: add WorkoutPlanRepository with template + assignment lookups (TDD)`
- `feat: add EquipmentRepository with maintenance-due query (TDD)`

---

## Phase 4 — Services (TDD)

### Task 4.1 — `UserSession`

**Files:**
- Create: `Gymers.Core/Services/IUserSession.cs`, `UserSession.cs`
- Create: `Gymers.Tests/Services/UserSessionTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Gymers.Models;
using Gymers.Services;

namespace Gymers.Tests.Services;

public class UserSessionTests
{
    [Fact]
    public void Default_HasNoUser()
    {
        var s = new UserSession();
        Assert.Null(s.CurrentUser);
        Assert.False(s.IsAdmin);
        Assert.False(s.IsStaff);
    }

    [Fact]
    public void SetUser_RaisesChanged_AndUpdatesRoleFlags()
    {
        var s = new UserSession();
        var changed = 0;
        s.Changed += (_, _) => changed++;

        s.SetUser(new User { Username = "admin", Role = Role.Admin });

        Assert.Equal(1, changed);
        Assert.True(s.IsAdmin);
        Assert.False(s.IsStaff);
    }

    [Fact]
    public void Clear_ResetsUserAndRaisesChanged()
    {
        var s = new UserSession();
        s.SetUser(new User { Username = "admin", Role = Role.Admin });
        var changedAfterSet = 0;
        s.Changed += (_, _) => changedAfterSet++;

        s.Clear();

        Assert.Null(s.CurrentUser);
        Assert.False(s.IsAdmin);
        Assert.Equal(1, changedAfterSet);
    }
}
```

- [ ] **Step 2: Run, verify compile failure**

```bash
dotnet test --filter UserSessionTests
```

- [ ] **Step 3: Write `IUserSession.cs`**

```csharp
using Gymers.Models;

namespace Gymers.Services;

public interface IUserSession
{
    User? CurrentUser { get; }
    bool IsAdmin { get; }
    bool IsStaff { get; }
    event EventHandler? Changed;
    void SetUser(User user);
    void Clear();
}
```

- [ ] **Step 4: Write `UserSession.cs`**

```csharp
using Gymers.Models;

namespace Gymers.Services;

public class UserSession : IUserSession
{
    public User? CurrentUser { get; private set; }
    public bool IsAdmin => CurrentUser?.Role == Role.Admin;
    public bool IsStaff => CurrentUser?.Role == Role.Staff;
    public event EventHandler? Changed;

    public void SetUser(User user)
    {
        CurrentUser = user;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        CurrentUser = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] **Step 5: Run, verify pass**

```bash
dotnet test --filter UserSessionTests
```

Expected: 3 tests passed.

- [ ] **Step 6: Commit**

```bash
git add Gymers.Core/Services/IUserSession.cs Gymers.Core/Services/UserSession.cs Gymers.Tests/Services/UserSessionTests.cs
git commit -m "feat: add UserSession with role flags (TDD)"
```

### Task 4.2 — `AuthService` (login)

**Files:**
- Create: `Gymers.Core/Services/IAuthService.cs`, `AuthService.cs`, `Gymers.Core/Services/AuthExceptions.cs`
- Create: `Gymers.Tests/Services/AuthServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using BCrypt.Net;
using Gymers.Data.Repositories;
using Gymers.Models;
using Gymers.Services;
using NSubstitute;

namespace Gymers.Tests.Services;

public class AuthServiceTests
{
    private static User MakeUser(string username = "admin", string password = "admin123",
        Role role = Role.Admin, bool isActive = true, bool mustChange = false)
        => new()
        {
            Id = 1,
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role,
            IsActive = isActive,
            MustChangePassword = mustChange,
            FullName = "Test"
        };

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsUser()
    {
        var repo = Substitute.For<IUserRepository>();
        var session = new UserSession();
        var u = MakeUser();
        repo.GetByUsernameAsync("admin").Returns(u);

        var sut = new AuthService(repo, session);
        var result = await sut.LoginAsync("admin", "admin123");

        Assert.Same(u, result);
        Assert.Same(u, session.CurrentUser);
    }

    [Fact]
    public async Task LoginAsync_UnknownUsername_ThrowsInvalidCredentials()
    {
        var repo = Substitute.For<IUserRepository>();
        repo.GetByUsernameAsync("nope").Returns((User?)null);
        var sut = new AuthService(repo, new UserSession());

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => sut.LoginAsync("nope", "x"));
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsInvalidCredentials()
    {
        var repo = Substitute.For<IUserRepository>();
        repo.GetByUsernameAsync("admin").Returns(MakeUser());
        var sut = new AuthService(repo, new UserSession());

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => sut.LoginAsync("admin", "wrong"));
    }

    [Fact]
    public async Task LoginAsync_DisabledUser_ThrowsAccountDisabled()
    {
        var repo = Substitute.For<IUserRepository>();
        repo.GetByUsernameAsync("admin").Returns(MakeUser(isActive: false));
        var sut = new AuthService(repo, new UserSession());

        await Assert.ThrowsAsync<AccountDisabledException>(() => sut.LoginAsync("admin", "admin123"));
    }

    [Fact]
    public async Task ChangePasswordAsync_ValidOld_UpdatesHashAndClearsFlag()
    {
        var repo = Substitute.For<IUserRepository>();
        var u = MakeUser(mustChange: true);
        repo.GetByIdAsync(u.Id).Returns(u);

        var sut = new AuthService(repo, new UserSession());
        await sut.ChangePasswordAsync(u.Id, "admin123", "newpass1");

        Assert.False(u.MustChangePassword);
        Assert.True(BCrypt.Net.BCrypt.Verify("newpass1", u.PasswordHash));
        await repo.Received(1).UpdateAsync(u);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongOldPassword_Throws()
    {
        var repo = Substitute.For<IUserRepository>();
        var u = MakeUser();
        repo.GetByIdAsync(u.Id).Returns(u);
        var sut = new AuthService(repo, new UserSession());

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => sut.ChangePasswordAsync(u.Id, "wrong", "x"));
    }
}
```

- [ ] **Step 2: Run, verify compile failure**

```bash
dotnet test --filter AuthServiceTests
```

- [ ] **Step 3: Write `AuthExceptions.cs`**

```csharp
namespace Gymers.Services;

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Invalid username or password.") { }
}

public class AccountDisabledException : Exception
{
    public AccountDisabledException() : base("This account has been disabled. Contact your administrator.") { }
}

public class DuplicateUsernameException : Exception
{
    public DuplicateUsernameException(string username) : base($"Username '{username}' is already in use.") { }
}
```

- [ ] **Step 4: Write `IAuthService.cs`**

```csharp
using Gymers.Models;

namespace Gymers.Services;

public interface IAuthService
{
    Task<User> LoginAsync(string username, string password);
    Task ChangePasswordAsync(int userId, string oldPassword, string newPassword);
    Task LogoutAsync();
}
```

- [ ] **Step 5: Write `AuthService.cs`**

```csharp
using BCrypt.Net;
using Gymers.Data.Repositories;
using Gymers.Models;

namespace Gymers.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IUserSession _session;

    public AuthService(IUserRepository users, IUserSession session)
    {
        _users = users;
        _session = session;
    }

    public async Task<User> LoginAsync(string username, string password)
    {
        var user = await _users.GetByUsernameAsync(username);
        if (user == null) throw new InvalidCredentialsException();
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new InvalidCredentialsException();
        if (!user.IsActive) throw new AccountDisabledException();

        _session.SetUser(user);
        return user;
    }

    public async Task ChangePasswordAsync(int userId, string oldPassword, string newPassword)
    {
        var user = await _users.GetByIdAsync(userId)
            ?? throw new InvalidCredentialsException();
        if (!BCrypt.Net.BCrypt.Verify(oldPassword, user.PasswordHash))
            throw new InvalidCredentialsException();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.MustChangePassword = false;
        await _users.UpdateAsync(user);
    }

    public Task LogoutAsync()
    {
        _session.Clear();
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 6: Run, verify pass**

```bash
dotnet test --filter AuthServiceTests
```

Expected: 6 tests passed.

- [ ] **Step 7: Commit**

```bash
git add Gymers.Core/Services/IAuthService.cs Gymers.Core/Services/AuthService.cs Gymers.Core/Services/AuthExceptions.cs Gymers.Tests/Services/AuthServiceTests.cs
git commit -m "feat: add AuthService with login + change password (TDD)"
```

### Task 4.3 — `MembershipService`

**Files:**
- Create: `Gymers.Core/Services/IMembershipService.cs`, `MembershipService.cs`
- Create: `Gymers.Tests/Services/MembershipServiceTests.cs`

Responsibilities: compute the period for a `MembershipType`, recompute `Member.Status` per the precedence rules, extend membership end-date on renewal.

- [ ] **Step 1: Write failing tests**

```csharp
using Gymers.Data.Repositories;
using Gymers.Models;
using Gymers.Services;
using Gymers.Tests.TestHelpers;
using NSubstitute;

namespace Gymers.Tests.Services;

public class MembershipServiceTests
{
    private static Member NewMember(MemberStatus s = MemberStatus.Active, MembershipType t = MembershipType.Monthly,
        DateTime? endDate = null) => new()
    {
        Id = 1,
        FullName = "X",
        ContactNumber = "1",
        MembershipType = t,
        MembershipStartDate = new DateTime(2026, 5, 1),
        MembershipEndDate = endDate ?? new DateTime(2026, 6, 1),
        Status = s,
        IsActive = true
    };

    [Theory]
    [InlineData(MembershipType.Monthly, "2026-05-01", "2026-06-01")]
    [InlineData(MembershipType.Quarterly, "2026-05-01", "2026-08-01")]
    [InlineData(MembershipType.Annual, "2026-05-01", "2027-05-01")]
    public void ComputeEndDate_ReturnsExpected(MembershipType type, string startStr, string expectedEndStr)
    {
        var sut = new MembershipService(
            Substitute.For<IMemberRepository>(),
            Substitute.For<IAttendanceRepository>(),
            new FakeClock());
        var start = DateTime.Parse(startStr);
        var expected = DateTime.Parse(expectedEndStr);

        var result = sut.ComputeEndDate(start, type);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task RecomputeStatusAsync_EndDateInPast_SetsExpired()
    {
        var members = Substitute.For<IMemberRepository>();
        var attendance = Substitute.For<IAttendanceRepository>();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc) };
        var m = NewMember(endDate: new DateTime(2026, 6, 1));
        members.GetByIdAsync(m.Id).Returns(m);

        var sut = new MembershipService(members, attendance, clock);
        await sut.RecomputeStatusAsync(m.Id);

        Assert.Equal(MemberStatus.Expired, m.Status);
        await members.Received(1).UpdateAsync(m);
    }

    [Fact]
    public async Task RecomputeStatusAsync_NoAttendanceIn30Days_SetsInactive()
    {
        var members = Substitute.For<IMemberRepository>();
        var attendance = Substitute.For<IAttendanceRepository>();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc) };
        var m = NewMember(endDate: new DateTime(2026, 6, 1));
        members.GetByIdAsync(m.Id).Returns(m);
        attendance.GetLastCheckInForMemberAsync(m.Id).Returns(new DateTime(2026, 4, 1));

        var sut = new MembershipService(members, attendance, clock);
        await sut.RecomputeStatusAsync(m.Id);

        Assert.Equal(MemberStatus.Inactive, m.Status);
    }

    [Fact]
    public async Task RecomputeStatusAsync_RecentAttendance_SetsActive()
    {
        var members = Substitute.For<IMemberRepository>();
        var attendance = Substitute.For<IAttendanceRepository>();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc) };
        var m = NewMember(endDate: new DateTime(2026, 6, 1));
        members.GetByIdAsync(m.Id).Returns(m);
        attendance.GetLastCheckInForMemberAsync(m.Id).Returns(new DateTime(2026, 5, 10));

        var sut = new MembershipService(members, attendance, clock);
        await sut.RecomputeStatusAsync(m.Id);

        Assert.Equal(MemberStatus.Active, m.Status);
    }

    [Fact]
    public async Task ExtendMembershipAsync_AddsTypePeriod()
    {
        var members = Substitute.For<IMemberRepository>();
        var attendance = Substitute.For<IAttendanceRepository>();
        var m = NewMember(t: MembershipType.Quarterly, endDate: new DateTime(2026, 6, 1));
        members.GetByIdAsync(m.Id).Returns(m);
        var clock = new FakeClock { UtcNow = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc) };

        var sut = new MembershipService(members, attendance, clock);
        await sut.ExtendMembershipAsync(m.Id);

        Assert.Equal(new DateTime(2026, 9, 1), m.MembershipEndDate);
        await members.Received(1).UpdateAsync(m);
    }
}
```

- [ ] **Step 2: Run, verify compile failures**

```bash
dotnet test --filter MembershipServiceTests
```

- [ ] **Step 3: Write `IMembershipService.cs`**

```csharp
using Gymers.Models;

namespace Gymers.Services;

public interface IMembershipService
{
    DateTime ComputeEndDate(DateTime start, MembershipType type);
    Task RecomputeStatusAsync(int memberId);
    Task RecomputeAllStatusesAsync();
    Task ExtendMembershipAsync(int memberId);
}
```

- [ ] **Step 4: Write `MembershipService.cs`**

```csharp
using Gymers.Data.Repositories;
using Gymers.Models;

namespace Gymers.Services;

public class MembershipService : IMembershipService
{
    private readonly IMemberRepository _members;
    private readonly IAttendanceRepository _attendance;
    private readonly IClock _clock;

    public MembershipService(IMemberRepository members, IAttendanceRepository attendance, IClock clock)
    {
        _members = members;
        _attendance = attendance;
        _clock = clock;
    }

    public DateTime ComputeEndDate(DateTime start, MembershipType type) => type switch
    {
        MembershipType.Monthly => start.AddMonths(1),
        MembershipType.Quarterly => start.AddMonths(3),
        MembershipType.Annual => start.AddYears(1),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public async Task RecomputeStatusAsync(int memberId)
    {
        var m = await _members.GetByIdAsync(memberId);
        if (m == null) return;

        var today = _clock.UtcNow.Date;
        MemberStatus newStatus;
        if (m.MembershipEndDate.Date < today)
        {
            newStatus = MemberStatus.Expired;
        }
        else
        {
            var lastCheckIn = await _attendance.GetLastCheckInForMemberAsync(m.Id);
            if (lastCheckIn == null || (today - lastCheckIn.Value.Date).TotalDays > 30)
                newStatus = MemberStatus.Inactive;
            else
                newStatus = MemberStatus.Active;
        }

        if (m.Status != newStatus)
        {
            m.Status = newStatus;
            await _members.UpdateAsync(m);
        }
    }

    public async Task RecomputeAllStatusesAsync()
    {
        var all = await _members.ListActiveAsync();
        foreach (var m in all) await RecomputeStatusAsync(m.Id);
    }

    public async Task ExtendMembershipAsync(int memberId)
    {
        var m = await _members.GetByIdAsync(memberId)
            ?? throw new InvalidOperationException("Member not found");
        m.MembershipEndDate = ComputeEndDate(m.MembershipEndDate, m.MembershipType);
        await _members.UpdateAsync(m);
    }
}
```

- [ ] **Step 5: Run, verify pass**

```bash
dotnet test --filter MembershipServiceTests
```

Expected: 7 tests passed.

- [ ] **Step 6: Commit**

```bash
git add Gymers.Core/Services/IMembershipService.cs Gymers.Core/Services/MembershipService.cs Gymers.Tests/Services/MembershipServiceTests.cs
git commit -m "feat: add MembershipService with status calc + renewal extension (TDD)"
```

### Task 4.4 — `PaymentService` + `IReceiptPdfService`

**Files:**
- Create: `Gymers.Core/Services/IReceiptPdfService.cs`, `ReceiptPdfService.cs`
- Create: `Gymers.Core/Services/IPaymentService.cs`, `PaymentService.cs`
- Create: `Gymers.Tests/Services/PaymentServiceTests.cs`

`PaymentService` records payments, generates a unique receipt number, calls `IReceiptPdfService` to write a PDF to disk, and (when type is `Renewal`) calls `IMembershipService.ExtendMembershipAsync`. The PDF service is *not* unit-tested for content (we'd need to read PDF bytes); we test that it was called.

- [ ] **Step 1: Write `IReceiptPdfService.cs`**

```csharp
using Gymers.Models;

namespace Gymers.Services;

public interface IReceiptPdfService
{
    /// <summary>Writes a PDF to disk and returns the absolute file path.</summary>
    Task<string> WriteReceiptAsync(Payment payment, Member member);
}
```

- [ ] **Step 2: Write failing `PaymentServiceTests.cs`**

```csharp
using Gymers.Data.Repositories;
using Gymers.Models;
using Gymers.Services;
using Gymers.Tests.TestHelpers;
using NSubstitute;

namespace Gymers.Tests.Services;

public class PaymentServiceTests
{
    private static Member ExistingMember(int id = 1) => new()
    {
        Id = id,
        FullName = "Juan",
        ContactNumber = "1",
        MembershipType = MembershipType.Monthly,
        MembershipStartDate = new DateTime(2026, 5, 1),
        MembershipEndDate = new DateTime(2026, 6, 1),
        Status = MemberStatus.Active,
        IsActive = true
    };

    [Fact]
    public async Task RecordAsync_GeneratesSequentialReceiptNumbers()
    {
        var payments = Substitute.For<IPaymentRepository>();
        var members = Substitute.For<IMemberRepository>();
        var pdf = Substitute.For<IReceiptPdfService>();
        var membership = Substitute.For<IMembershipService>();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc) };

        members.GetByIdAsync(1).Returns(ExistingMember());
        payments.GetMaxReceiptCounterForYearAsync(2026).Returns(7);
        pdf.WriteReceiptAsync(Arg.Any<Payment>(), Arg.Any<Member>()).Returns("/tmp/r.pdf");

        var sut = new PaymentService(payments, members, pdf, membership, clock);

        var p = await sut.RecordAsync(memberId: 1, amount: 1500m,
            type: PaymentType.WalkIn, method: PaymentMethod.Cash,
            processedByUserId: 1, notes: null);

        Assert.Equal("RCPT-2026-0008", p.ReceiptNumber);
        Assert.Equal("/tmp/r.pdf", p.ReceiptPdfPath);
    }

    [Fact]
    public async Task RecordAsync_RenewalType_ExtendsMembership()
    {
        var payments = Substitute.For<IPaymentRepository>();
        var members = Substitute.For<IMemberRepository>();
        var pdf = Substitute.For<IReceiptPdfService>();
        var membership = Substitute.For<IMembershipService>();
        var clock = new FakeClock();
        members.GetByIdAsync(1).Returns(ExistingMember());
        payments.GetMaxReceiptCounterForYearAsync(Arg.Any<int>()).Returns(0);
        pdf.WriteReceiptAsync(Arg.Any<Payment>(), Arg.Any<Member>()).Returns("/tmp/r.pdf");

        var sut = new PaymentService(payments, members, pdf, membership, clock);

        await sut.RecordAsync(1, 1500m, PaymentType.Renewal, PaymentMethod.Cash, 1, null);

        await membership.Received(1).ExtendMembershipAsync(1);
    }

    [Fact]
    public async Task RecordAsync_NonRenewalType_DoesNotExtendMembership()
    {
        var payments = Substitute.For<IPaymentRepository>();
        var members = Substitute.For<IMemberRepository>();
        var pdf = Substitute.For<IReceiptPdfService>();
        var membership = Substitute.For<IMembershipService>();
        var clock = new FakeClock();
        members.GetByIdAsync(1).Returns(ExistingMember());
        payments.GetMaxReceiptCounterForYearAsync(Arg.Any<int>()).Returns(0);
        pdf.WriteReceiptAsync(Arg.Any<Payment>(), Arg.Any<Member>()).Returns("/tmp/r.pdf");

        var sut = new PaymentService(payments, members, pdf, membership, clock);

        await sut.RecordAsync(1, 100m, PaymentType.WalkIn, PaymentMethod.Cash, 1, null);

        await membership.DidNotReceive().ExtendMembershipAsync(Arg.Any<int>());
    }

    [Fact]
    public async Task RecordAsync_MissingMember_Throws()
    {
        var payments = Substitute.For<IPaymentRepository>();
        var members = Substitute.For<IMemberRepository>();
        members.GetByIdAsync(99).Returns((Member?)null);

        var sut = new PaymentService(payments, members,
            Substitute.For<IReceiptPdfService>(), Substitute.For<IMembershipService>(), new FakeClock());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RecordAsync(99, 100m, PaymentType.WalkIn, PaymentMethod.Cash, 1, null));
    }
}
```

- [ ] **Step 3: Run, verify compile failure**

```bash
dotnet test --filter PaymentServiceTests
```

- [ ] **Step 4: Write `IPaymentService.cs`**

```csharp
using Gymers.Models;

namespace Gymers.Services;

public interface IPaymentService
{
    Task<Payment> RecordAsync(int memberId, decimal amount, PaymentType type,
        PaymentMethod method, int processedByUserId, string? notes);
}
```

- [ ] **Step 5: Write `PaymentService.cs`**

```csharp
using Gymers.Data.Repositories;
using Gymers.Models;

namespace Gymers.Services;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _payments;
    private readonly IMemberRepository _members;
    private readonly IReceiptPdfService _pdf;
    private readonly IMembershipService _membership;
    private readonly IClock _clock;

    public PaymentService(IPaymentRepository payments, IMemberRepository members,
        IReceiptPdfService pdf, IMembershipService membership, IClock clock)
    {
        _payments = payments;
        _members = members;
        _pdf = pdf;
        _membership = membership;
        _clock = clock;
    }

    public async Task<Payment> RecordAsync(int memberId, decimal amount, PaymentType type,
        PaymentMethod method, int processedByUserId, string? notes)
    {
        var member = await _members.GetByIdAsync(memberId)
            ?? throw new InvalidOperationException($"Member {memberId} not found.");

        var now = _clock.UtcNow;
        var year = now.Year;
        var nextCounter = await _payments.GetMaxReceiptCounterForYearAsync(year) + 1;
        var receiptNumber = $"RCPT-{year}-{nextCounter:D4}";

        var payment = new Payment
        {
            MemberId = memberId,
            Amount = amount,
            PaymentType = type,
            PaymentMethod = method,
            ReceiptNumber = receiptNumber,
            ProcessedByUserId = processedByUserId,
            ProcessedAt = now,
            Notes = notes
        };

        await _payments.InsertAsync(payment);

        if (type == PaymentType.Renewal)
            await _membership.ExtendMembershipAsync(memberId);

        var refreshed = await _members.GetByIdAsync(memberId) ?? member;
        var pdfPath = await _pdf.WriteReceiptAsync(payment, refreshed);
        payment.ReceiptPdfPath = pdfPath;
        await _payments.UpdateAsync(payment);

        return payment;
    }
}
```

- [ ] **Step 6: Write `ReceiptPdfService.cs`** (no unit test — content rendering is integration)

The constructor takes the output directory **explicitly** so this class lives in `Gymers.Core` (no MAUI types). The MAUI app supplies `FileSystem.AppDataDirectory + "/Receipts"` at DI registration time (see Task 5.2).

```csharp
using Gymers.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Gymers.Services;

public class ReceiptPdfService : IReceiptPdfService
{
    private readonly string _outputDir;

    public ReceiptPdfService(string outputDir)
    {
        _outputDir = outputDir;
        Directory.CreateDirectory(_outputDir);
    }

    public Task<string> WriteReceiptAsync(Payment payment, Member member)
    {
        var path = Path.Combine(_outputDir, $"{payment.ReceiptNumber}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A5);
                page.DefaultTextStyle(s => s.FontSize(12));

                page.Header().Column(c =>
                {
                    c.Item().Text("GYMERS").FontSize(24).Bold();
                    c.Item().Text("Official Receipt").FontSize(14);
                    c.Item().Text(payment.ReceiptNumber).FontSize(11).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingVertical(16).Column(c =>
                {
                    c.Item().Text($"Date: {payment.ProcessedAt:yyyy-MM-dd HH:mm}");
                    c.Item().Text($"Member: {member.FullName}");
                    c.Item().Text($"Type: {payment.PaymentType}");
                    c.Item().Text($"Method: {payment.PaymentMethod}");
                    if (!string.IsNullOrWhiteSpace(payment.Notes))
                        c.Item().Text($"Notes: {payment.Notes}");

                    c.Item().PaddingTop(20).Text($"Amount: ₱ {payment.Amount:N2}").FontSize(18).Bold();
                });

                page.Footer().AlignRight().Text("Thank you!").FontSize(10).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf(path);

        return Task.FromResult(path);
    }
}
```

- [ ] **Step 7: Run tests, verify pass**

```bash
dotnet test --filter PaymentServiceTests
```

Expected: 4 tests passed.

- [ ] **Step 8: Commit**

```bash
git add Gymers.Core/Services/IPaymentService.cs Gymers.Core/Services/PaymentService.cs Gymers.Core/Services/IReceiptPdfService.cs Gymers.Core/Services/ReceiptPdfService.cs Gymers.Tests/Services/PaymentServiceTests.cs
git commit -m "feat: add PaymentService + ReceiptPdfService with sequential receipt numbers (TDD)"
```

### Task 4.5 — `AttendanceService`

**Files:** Create `Gymers.Core/Services/IAttendanceService.cs`, `Gymers.Core/Services/AttendanceService.cs`, `Gymers.Tests/Services/AttendanceServiceTests.cs`

`IAttendanceService.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Services;

public interface IAttendanceService
{
    Task<Attendance> CheckInAsync(int memberId, int processedByUserId);
    Task<int> CountTodayAsync();
    Task<List<Attendance>> ListTodayAsync();
}
```

`AttendanceService.cs`:

```csharp
using Gymers.Data.Repositories;
using Gymers.Models;

namespace Gymers.Services;

public class AttendanceService : IAttendanceService
{
    private readonly IAttendanceRepository _attendance;
    private readonly IMemberRepository _members;
    private readonly IMembershipService _membership;
    private readonly IClock _clock;

    public AttendanceService(IAttendanceRepository attendance, IMemberRepository members,
        IMembershipService membership, IClock clock)
    {
        _attendance = attendance;
        _members = members;
        _membership = membership;
        _clock = clock;
    }

    public async Task<Attendance> CheckInAsync(int memberId, int processedByUserId)
    {
        var m = await _members.GetByIdAsync(memberId)
            ?? throw new InvalidOperationException("Member not found");
        if (!m.IsActive)
            throw new InvalidOperationException("Member has been removed.");

        var a = new Attendance
        {
            MemberId = memberId,
            CheckInTime = _clock.UtcNow,
            ProcessedByUserId = processedByUserId
        };
        await _attendance.InsertAsync(a);

        await _membership.RecomputeStatusAsync(memberId);

        return a;
    }

    public Task<int> CountTodayAsync()
    {
        var (start, end) = TodayRangeUtc();
        return _attendance.CountBetweenAsync(start, end);
    }

    public Task<List<Attendance>> ListTodayAsync()
    {
        var (start, end) = TodayRangeUtc();
        return _attendance.ListBetweenAsync(start, end);
    }

    private (DateTime start, DateTime end) TodayRangeUtc()
    {
        var today = _clock.LocalNow.Date;
        var startLocal = today;
        var endLocal = today.AddDays(1);
        return (startLocal.ToUniversalTime(), endLocal.ToUniversalTime());
    }
}
```

Tests must cover: check-in inserts an Attendance row with the expected `CheckInTime`; check-in for a soft-deleted member throws; `CountTodayAsync` and `ListTodayAsync` return only today's records.

Commit: `feat: add AttendanceService (TDD)`.

### Task 4.6 — `PhotoStorageService`

**Files:** Create `Gymers.Core/Services/IPhotoStorageService.cs`, `Gymers.Core/Services/PhotoStorageService.cs`, `Gymers.Tests/Services/PhotoStorageServiceTests.cs`

Same pattern as `ReceiptPdfService`: the constructor takes the photo root directory **explicitly**, so the class lives in `Gymers.Core`.

`IPhotoStorageService.cs`:

```csharp
namespace Gymers.Services;

public interface IPhotoStorageService
{
    /// <summary>Copies a source photo into the app's photo directory and returns the relative stored path.</summary>
    Task<string> SavePhotoAsync(string sourceFilePath, string memberPrefix);

    /// <summary>Resolves a stored relative path back to an absolute file path.</summary>
    string ResolveAbsolute(string relativePath);

    /// <summary>Deletes the photo file (no-op if missing).</summary>
    void Delete(string relativePath);
}
```

`PhotoStorageService.cs`:

```csharp
namespace Gymers.Services;

public class PhotoStorageService : IPhotoStorageService
{
    private readonly string _root;
    public PhotoStorageService(string root)
    {
        _root = root;
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SavePhotoAsync(string sourceFilePath, string memberPrefix)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Source photo not found.", sourceFilePath);
        var ext = Path.GetExtension(sourceFilePath);
        var fileName = $"{Sanitize(memberPrefix)}-{Guid.NewGuid():N}{ext}";
        var destination = Path.Combine(_root, fileName);
        using (var src = File.OpenRead(sourceFilePath))
        using (var dst = File.Create(destination))
            await src.CopyToAsync(dst);
        return Path.GetRelativePath(_root, destination);
    }

    public string ResolveAbsolute(string relativePath) => Path.Combine(_root, relativePath);

    public void Delete(string relativePath)
    {
        var abs = ResolveAbsolute(relativePath);
        if (File.Exists(abs)) File.Delete(abs);
    }

    private static string Sanitize(string s) =>
        new string(s.Where(c => char.IsLetterOrDigit(c)).ToArray()).ToLowerInvariant();
}
```

Tests use a temp root directory: `SavePhotoAsync` copies, returns a relative path that resolves to an existing file; `Delete` removes the file; `SavePhotoAsync` with a missing source throws `FileNotFoundException`.

Commit: `feat: add PhotoStorageService (TDD)`.

### Task 4.7 — Remaining services (full implementations)

Write each interface, implementation, and test class fully. Keep the same TDD rhythm: failing test → implement → green → commit.

#### `TrainerService`

**Files:** Create `Gymers.Core/Services/ITrainerService.cs`, `Gymers.Core/Services/TrainerService.cs`, `Gymers.Tests/Services/TrainerServiceTests.cs`

`ITrainerService.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Services;

public interface ITrainerService
{
    Task<int> CreateAsync(Trainer t);
    Task UpdateAsync(Trainer t);
    Task DeactivateAsync(int id);
    Task<List<Trainer>> ListActiveAsync();
    Task AssignToMemberAsync(int trainerId, int memberId, DateTime startDate, string? notes);
    Task EndCurrentAssignmentAsync(int memberId, DateTime endDate);
}
```

`TrainerService.cs`:

```csharp
using Gymers.Data.Repositories;
using Gymers.Models;

namespace Gymers.Services;

public class TrainerService : ITrainerService
{
    private readonly ITrainerRepository _trainers;
    private readonly ITrainerAssignmentRepository _assignments;

    public TrainerService(ITrainerRepository trainers, ITrainerAssignmentRepository assignments)
    {
        _trainers = trainers;
        _assignments = assignments;
    }

    public async Task<int> CreateAsync(Trainer t)
    {
        await _trainers.InsertAsync(t);
        return t.Id;
    }

    public Task UpdateAsync(Trainer t) => _trainers.UpdateAsync(t);
    public Task DeactivateAsync(int id) => _trainers.DeactivateAsync(id);
    public Task<List<Trainer>> ListActiveAsync() => _trainers.ListActiveAsync();

    public async Task AssignToMemberAsync(int trainerId, int memberId, DateTime startDate, string? notes)
    {
        // End any existing current assignment for this member first.
        var current = await _assignments.GetCurrentForMemberAsync(memberId);
        if (current != null)
            await _assignments.EndAssignmentAsync(current.Id, startDate);

        await _assignments.InsertAsync(new TrainerAssignment
        {
            TrainerId = trainerId,
            MemberId = memberId,
            StartDate = startDate,
            Notes = notes
        });
    }

    public async Task EndCurrentAssignmentAsync(int memberId, DateTime endDate)
    {
        var current = await _assignments.GetCurrentForMemberAsync(memberId);
        if (current != null)
            await _assignments.EndAssignmentAsync(current.Id, endDate);
    }
}
```

Tests must cover: `CreateAsync` returns the new Id; `AssignToMemberAsync` with no current assignment inserts one row; `AssignToMemberAsync` with an existing current assignment ends it before inserting (verify `EndAssignmentAsync` was called once). Use NSubstitute for both repositories.

Commit: `feat: add TrainerService (TDD)`.

#### `WorkoutPlanService`

**Files:** Create `Gymers.Core/Services/IWorkoutPlanService.cs`, `Gymers.Core/Services/WorkoutPlanService.cs`, `Gymers.Tests/Services/WorkoutPlanServiceTests.cs`

`IWorkoutPlanService.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Services;

public class WorkoutPlanWithExercises
{
    public WorkoutPlan Plan { get; set; } = new();
    public List<WorkoutPlanExercise> Exercises { get; set; } = new();
}

public interface IWorkoutPlanService
{
    Task<int> CreateTemplateAsync(WorkoutPlan plan, IEnumerable<WorkoutPlanExercise> exercises);
    Task<int> AssignTemplateToMemberAsync(int templateId, int memberId, int? trainerId);
    Task<List<WorkoutPlan>> ListTemplatesAsync();
    Task<WorkoutPlanWithExercises?> GetCurrentForMemberAsync(int memberId);
    Task ReplaceExercisesAsync(int planId, IEnumerable<WorkoutPlanExercise> exercises);
}
```

`WorkoutPlanService.cs`:

```csharp
using Gymers.Data.Repositories;
using Gymers.Models;

namespace Gymers.Services;

public class WorkoutPlanService : IWorkoutPlanService
{
    private readonly IWorkoutPlanRepository _plans;

    public WorkoutPlanService(IWorkoutPlanRepository plans) => _plans = plans;

    public async Task<int> CreateTemplateAsync(WorkoutPlan plan, IEnumerable<WorkoutPlanExercise> exercises)
    {
        plan.IsTemplate = true;
        plan.AssignedToMemberId = null;
        var planId = await _plans.InsertAsync(plan);
        foreach (var ex in exercises)
        {
            ex.WorkoutPlanId = planId;
            await _plans.InsertExerciseAsync(ex);
        }
        return planId;
    }

    public async Task<int> AssignTemplateToMemberAsync(int templateId, int memberId, int? trainerId)
    {
        var template = await _plans.GetByIdAsync(templateId)
            ?? throw new InvalidOperationException("Template not found");
        if (!template.IsTemplate)
            throw new InvalidOperationException("Plan is not a template");

        var sourceExercises = await _plans.ListExercisesForPlanAsync(templateId);

        var assigned = new WorkoutPlan
        {
            Name = template.Name,
            Description = template.Description,
            IsTemplate = false,
            AssignedToMemberId = memberId,
            AssignedByTrainerId = trainerId
        };
        var newId = await _plans.InsertAsync(assigned);

        foreach (var src in sourceExercises)
        {
            await _plans.InsertExerciseAsync(new WorkoutPlanExercise
            {
                WorkoutPlanId = newId,
                ExerciseId = src.ExerciseId,
                Sets = src.Sets,
                Reps = src.Reps,
                Weight = src.Weight,
                Order = src.Order,
                Notes = src.Notes
            });
        }

        return newId;
    }

    public Task<List<WorkoutPlan>> ListTemplatesAsync() => _plans.ListTemplatesAsync();

    public async Task<WorkoutPlanWithExercises?> GetCurrentForMemberAsync(int memberId)
    {
        var plan = await _plans.GetCurrentForMemberAsync(memberId);
        if (plan == null) return null;
        var exercises = await _plans.ListExercisesForPlanAsync(plan.Id);
        return new WorkoutPlanWithExercises { Plan = plan, Exercises = exercises };
    }

    public async Task ReplaceExercisesAsync(int planId, IEnumerable<WorkoutPlanExercise> exercises)
    {
        await _plans.DeleteExercisesForPlanAsync(planId);
        foreach (var ex in exercises)
        {
            ex.WorkoutPlanId = planId;
            await _plans.InsertExerciseAsync(ex);
        }
    }
}
```

Tests must cover: `CreateTemplateAsync` sets `IsTemplate=true` and `AssignedToMemberId=null` regardless of input; `AssignTemplateToMemberAsync` produces a new plan with `IsTemplate=false`, `AssignedToMemberId=memberId`, copies all exercises; the original template is **not** modified (use a substitute that captures the calls and verify the source row's `IsTemplate` remains true).

Commit: `feat: add WorkoutPlanService with template assignment (TDD)`.

#### `EquipmentService`

**Files:** Create `Gymers.Core/Services/IEquipmentService.cs`, `Gymers.Core/Services/EquipmentService.cs`, `Gymers.Tests/Services/EquipmentServiceTests.cs`

`IEquipmentService.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Services;

public interface IEquipmentService
{
    Task<int> CreateAsync(Equipment e);
    Task UpdateAsync(Equipment e);
    Task DeactivateAsync(int id);
    Task<List<Equipment>> ListActiveAsync();
    Task<List<Equipment>> ListDueForMaintenanceAsync(DateTime byDateInclusive);
}
```

`EquipmentService.cs`:

```csharp
using Gymers.Data.Repositories;
using Gymers.Models;

namespace Gymers.Services;

public class EquipmentService : IEquipmentService
{
    private readonly IEquipmentRepository _equipment;

    public EquipmentService(IEquipmentRepository equipment) => _equipment = equipment;

    public async Task<int> CreateAsync(Equipment e)
    {
        await _equipment.InsertAsync(e);
        return e.Id;
    }

    public Task UpdateAsync(Equipment e) => _equipment.UpdateAsync(e);
    public Task DeactivateAsync(int id) => _equipment.DeactivateAsync(id);
    public Task<List<Equipment>> ListActiveAsync() => _equipment.ListActiveAsync();
    public Task<List<Equipment>> ListDueForMaintenanceAsync(DateTime byDateInclusive)
        => _equipment.ListDueForMaintenanceAsync(byDateInclusive);
}
```

Tests: one happy-path test per public method. `CreateAsync` writes through and returns the assigned Id; `ListDueForMaintenanceAsync` forwards the date argument; etc.

Commit: `feat: add EquipmentService (TDD)`.

#### `ReportService`

**Files:** Create `Gymers.Core/Services/IReportService.cs`, `Gymers.Core/Services/ReportService.cs`, `Gymers.Tests/Services/ReportServiceTests.cs`

`IReportService.cs`:

```csharp
using Gymers.Models;

namespace Gymers.Services;

public class DashboardKpis
{
    public int TotalActiveMembers { get; set; }
    public int TodayCheckIns { get; set; }
    public int ExpiringWithinSevenDays { get; set; }
    public decimal MonthlyRevenue { get; set; }
}

public class RevenueRow
{
    public DateTime Date { get; set; }
    public decimal Total { get; set; }
}

public interface IReportService
{
    Task<DashboardKpis> GetDashboardAsync();
    Task<List<Payment>> ListPaymentsBetweenAsync(DateTime fromInclusive, DateTime toExclusive);
    Task<List<Attendance>> ListAttendanceBetweenAsync(DateTime fromInclusive, DateTime toExclusive);
    Task<(int active, int inactive, int expired)> GetMembershipSummaryAsync();
}
```

`ReportService.cs`:

```csharp
using Gymers.Data.Repositories;
using Gymers.Models;

namespace Gymers.Services;

public class ReportService : IReportService
{
    private readonly IMemberRepository _members;
    private readonly IPaymentRepository _payments;
    private readonly IAttendanceRepository _attendance;
    private readonly IClock _clock;

    public ReportService(IMemberRepository members, IPaymentRepository payments,
        IAttendanceRepository attendance, IClock clock)
    {
        _members = members;
        _payments = payments;
        _attendance = attendance;
        _clock = clock;
    }

    public async Task<DashboardKpis> GetDashboardAsync()
    {
        var todayLocal = _clock.LocalNow.Date;
        var todayStartUtc = todayLocal.ToUniversalTime();
        var tomorrowStartUtc = todayLocal.AddDays(1).ToUniversalTime();

        var firstOfMonthLocal = new DateTime(todayLocal.Year, todayLocal.Month, 1);
        var firstOfNextMonthLocal = firstOfMonthLocal.AddMonths(1);
        var monthStartUtc = firstOfMonthLocal.ToUniversalTime();
        var monthEndUtc = firstOfNextMonthLocal.ToUniversalTime();

        return new DashboardKpis
        {
            TotalActiveMembers = await _members.CountActiveAsync(),
            TodayCheckIns = await _attendance.CountBetweenAsync(todayStartUtc, tomorrowStartUtc),
            ExpiringWithinSevenDays = (await _members.ListExpiringWithinAsync(todayLocal, todayLocal.AddDays(7))).Count,
            MonthlyRevenue = await _payments.SumAmountBetweenAsync(monthStartUtc, monthEndUtc)
        };
    }

    public Task<List<Payment>> ListPaymentsBetweenAsync(DateTime fromInclusive, DateTime toExclusive)
        => _payments.ListBetweenAsync(fromInclusive, toExclusive);

    public Task<List<Attendance>> ListAttendanceBetweenAsync(DateTime fromInclusive, DateTime toExclusive)
        => _attendance.ListBetweenAsync(fromInclusive, toExclusive);

    public async Task<(int active, int inactive, int expired)> GetMembershipSummaryAsync()
    {
        var active = (await _members.ListByStatusAsync(MemberStatus.Active)).Count;
        var inactive = (await _members.ListByStatusAsync(MemberStatus.Inactive)).Count;
        var expired = (await _members.ListByStatusAsync(MemberStatus.Expired)).Count;
        return (active, inactive, expired);
    }
}
```

Tests: substitute every repo + a `FakeClock` set to `2026-05-15 12:00 UTC`. Verify `GetDashboardAsync` reads the correct date ranges (assert calls received with expected args). Verify `GetMembershipSummaryAsync` returns the correct tuple from three substitute returns.

Commit: `feat: add ReportService with KPI aggregation (TDD)`.

#### `StartupService`

**Files:** Create `Gymers.Core/Services/StartupService.cs` (no interface needed — invoked directly)

```csharp
namespace Gymers.Services;

public class StartupService
{
    private readonly IMembershipService _membership;
    public StartupService(IMembershipService membership) => _membership = membership;

    public Task RunOnLaunchAsync() => _membership.RecomputeAllStatusesAsync();
}
```

No tests — it's a thin orchestrator. Commit: `feat: add StartupService for launch-time status recompute`.

---

## Phase 5 — Navigation, DI Wiring, Auth UI

### Task 5.1 — `INavigationService` + `NavigationService`

**Files:**
- Create: `Gymers.Core/Services/INavigationService.cs` (interface — pure abstractions, no MAUI types)
- Create: `Gymers/Services/NavigationService.cs` (impl — uses `Shell.Current` from MAUI)

`INavigationService.cs` (in Gymers.Core):

```csharp
namespace Gymers.Services;

public interface INavigationService
{
    Task GoToAsync(string route);
    Task GoToAsync(string route, IDictionary<string, object> parameters);
    Task GoBackAsync();
}
```

`NavigationService.cs` (in Gymers — MAUI assembly, uses `Shell.Current`):

```csharp
namespace Gymers.Services;

public class NavigationService : INavigationService
{
    public Task GoToAsync(string route) =>
        Microsoft.Maui.Controls.Shell.Current.GoToAsync(route);

    public Task GoToAsync(string route, IDictionary<string, object> parameters) =>
        Microsoft.Maui.Controls.Shell.Current.GoToAsync(route, parameters);

    public Task GoBackAsync() =>
        Microsoft.Maui.Controls.Shell.Current.GoToAsync("..");
}
```

Both files declare `namespace Gymers.Services;` — two assemblies contributing to the same namespace is allowed in .NET. The interface lives in Core so VMs (in Gymers) and services (in Gymers.Core) can share it without dragging MAUI into the test project.

No tests — it's a thin Shell wrapper.

```bash
git add Gymers.Core/Services/INavigationService.cs Gymers/Services/NavigationService.cs
git commit -m "feat: add NavigationService Shell wrapper"
```

### Task 5.2 — DI registration in `MauiProgram.cs` (services + repos only)

**Files:** Modify `Gymers/MauiProgram.cs`

This task registers every service and repository that exists at this point (Phases 3 + 4). ViewModel and View registrations are added inside the tasks that *create* those VMs/Views (Task 5.4 adds Login, Task 5.5 adds ChangePassword, Task 6.2 adds Dashboard, etc.). That keeps the build green at every commit — no commented-out lines.

- [ ] **Step 1: Replace the `MauiProgram` class body**

```csharp
using CommunityToolkit.Maui;
using Gymers.Data;
using Gymers.Data.Repositories;
using Gymers.Services;
using Microsoft.Extensions.Logging;
using QuestPDF.Infrastructure;
using Serilog;

namespace Gymers;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var logDir = Path.Combine(FileSystem.AppDataDirectory, "Logs");
        Directory.CreateDirectory(logDir);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(logDir, "gymers-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Logging.AddSerilog(dispose: true);

        // Database (singleton)
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "gymers.db3");
        builder.Services.AddSingleton(new GymDatabase(dbPath));

        // Repositories
        builder.Services.AddSingleton<IUserRepository, UserRepository>();
        builder.Services.AddSingleton<IMemberRepository, MemberRepository>();
        builder.Services.AddSingleton<ITrainerRepository, TrainerRepository>();
        builder.Services.AddSingleton<ITrainerAssignmentRepository, TrainerAssignmentRepository>();
        builder.Services.AddSingleton<IPaymentRepository, PaymentRepository>();
        builder.Services.AddSingleton<IAttendanceRepository, AttendanceRepository>();
        builder.Services.AddSingleton<IExerciseRepository, ExerciseRepository>();
        builder.Services.AddSingleton<IWorkoutPlanRepository, WorkoutPlanRepository>();
        builder.Services.AddSingleton<IEquipmentRepository, EquipmentRepository>();

        // Services
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<IUserSession, UserSession>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IMembershipService, MembershipService>();
        builder.Services.AddSingleton<IPaymentService, PaymentService>();
        builder.Services.AddSingleton<IReceiptPdfService>(_ => new ReceiptPdfService(
            Path.Combine(FileSystem.AppDataDirectory, "Receipts")));
        builder.Services.AddSingleton<IAttendanceService, AttendanceService>();
        builder.Services.AddSingleton<IPhotoStorageService>(_ => new PhotoStorageService(
            Path.Combine(FileSystem.AppDataDirectory, "MemberPhotos")));
        builder.Services.AddSingleton<ITrainerService, TrainerService>();
        builder.Services.AddSingleton<IWorkoutPlanService, WorkoutPlanService>();
        builder.Services.AddSingleton<IEquipmentService, EquipmentService>();
        builder.Services.AddSingleton<IReportService, ReportService>();
        builder.Services.AddSingleton<StartupService>();

        // ViewModels and Views are registered in their own tasks — see Tasks 5.4, 5.5, 6.2, 6.3, 6.4, 6.5, 7.x, etc.

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // Initialize DB on startup (synchronously block so the first page sees a ready DB)
        var db = app.Services.GetRequiredService<GymDatabase>();
        db.InitAsync().GetAwaiter().GetResult();

        // Recompute statuses
        var startup = app.Services.GetRequiredService<StartupService>();
        startup.RunOnLaunchAsync().GetAwaiter().GetResult();

        return app;
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build
```

Expected: `Build succeeded`. (No types are referenced before they exist — Login/ChangePassword VMs and Pages are added in Tasks 5.4 and 5.5.)

- [ ] **Step 3: Commit**

```bash
git add Gymers/MauiProgram.cs
git commit -m "feat: wire up DI for repos, services, and core scaffolding"
```

> **Convention for the rest of the plan:** every task that creates a new ViewModel + Page also adds two registration lines in `MauiProgram.cs` immediately, e.g.:
> ```csharp
> builder.Services.AddTransient<LoginViewModel>();
> builder.Services.AddTransient<LoginPage>();
> ```
> Add them in the same commit that introduces the VM/Page so the project never has a "registration without class" or "class without registration" intermediate state.

### Task 5.3 — `BaseViewModel`

**Files:** Create `Gymers.Core/ViewModels/BaseViewModel.cs`

`BaseViewModel` lives in Core because every VM derives from it, including the auth VMs that we want to unit-test.

- [ ] **Step 1: Create the folder**

```bash
mkdir -p Gymers.Core/ViewModels
```

- [ ] **Step 2: Write `BaseViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Gymers.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;
}
```

- [ ] **Step 3: Build**

```bash
dotnet build
```

- [ ] **Step 4: Commit**

```bash
git add Gymers.Core/ViewModels/BaseViewModel.cs
git commit -m "feat: add BaseViewModel with IsBusy + ErrorMessage"
```

### Task 5.4 — `LoginPage` + `LoginViewModel`

**Files:**
- Create: `Gymers.Core/ViewModels/Auth/LoginViewModel.cs` (no MAUI deps — unit-tested)
- Create: `Gymers/Views/Auth/LoginPage.xaml`, `LoginPage.xaml.cs`
- Create: `Gymers.Tests/ViewModels/LoginViewModelTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Gymers.Models;
using Gymers.Services;
using Gymers.ViewModels.Auth;
using NSubstitute;

namespace Gymers.Tests.ViewModels;

public class LoginViewModelTests
{
    [Fact]
    public async Task LoginCommand_EmptyFields_ShowsValidationError()
    {
        var auth = Substitute.For<IAuthService>();
        var nav = Substitute.For<INavigationService>();

        var vm = new LoginViewModel(auth, nav);
        await vm.LoginCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
        await auth.DidNotReceive().LoginAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LoginCommand_ValidAdmin_NavigatesToAdminDashboard()
    {
        var auth = Substitute.For<IAuthService>();
        var nav = Substitute.For<INavigationService>();
        var user = new User { Id = 1, Username = "admin", Role = Role.Admin, MustChangePassword = false };
        auth.LoginAsync("admin", "pw").Returns(user);

        var vm = new LoginViewModel(auth, nav)
        {
            Username = "admin",
            Password = "pw"
        };
        await vm.LoginCommand.ExecuteAsync(null);

        await nav.Received(1).GoToAsync("//admin/dashboard");
    }

    [Fact]
    public async Task LoginCommand_MustChangePassword_NavigatesToChangePassword()
    {
        var auth = Substitute.For<IAuthService>();
        var nav = Substitute.For<INavigationService>();
        var user = new User { Id = 1, Username = "admin", Role = Role.Admin, MustChangePassword = true };
        auth.LoginAsync("admin", "pw").Returns(user);

        var vm = new LoginViewModel(auth, nav) { Username = "admin", Password = "pw" };
        await vm.LoginCommand.ExecuteAsync(null);

        await nav.Received(1).GoToAsync("//ChangePassword");
    }

    [Fact]
    public async Task LoginCommand_InvalidCredentials_ShowsError()
    {
        var auth = Substitute.For<IAuthService>();
        var nav = Substitute.For<INavigationService>();
        auth.LoginAsync("admin", "wrong").Returns<User>(_ => throw new InvalidCredentialsException());

        var vm = new LoginViewModel(auth, nav) { Username = "admin", Password = "wrong" };
        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Contains("Invalid", vm.ErrorMessage);
    }
}
```

- [ ] **Step 2: Run, verify compile failure**

```bash
dotnet test --filter LoginViewModelTests
```

- [ ] **Step 3: Write `LoginViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gymers.Models;
using Gymers.Services;

namespace Gymers.ViewModels.Auth;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _auth;
    private readonly INavigationService _nav;

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;

    public LoginViewModel(IAuthService auth, INavigationService nav)
    {
        _auth = auth;
        _nav = nav;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter both username and password.";
            return;
        }

        IsBusy = true;
        try
        {
            var user = await _auth.LoginAsync(Username.Trim(), Password);
            if (user.MustChangePassword)
            {
                await _nav.GoToAsync("//ChangePassword");
                return;
            }
            await _nav.GoToAsync(user.Role == Role.Admin ? "//admin/dashboard" : "//staff/dashboard");
        }
        catch (InvalidCredentialsException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (AccountDisabledException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            ErrorMessage = "Could not log in. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

- [ ] **Step 4: Run tests, verify pass**

```bash
dotnet test --filter LoginViewModelTests
```

Expected: 4 tests passed.

- [ ] **Step 5: Write `LoginPage.xaml`**

The `xmlns:vm` reference includes `assembly=Gymers.Core` because `LoginViewModel` lives in the Core library, not in this MAUI assembly.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:Gymers.ViewModels.Auth;assembly=Gymers.Core"
             x:Class="Gymers.Views.Auth.LoginPage"
             x:DataType="vm:LoginViewModel"
             Title="Sign in"
             Shell.NavBarIsVisible="False">

    <Grid Padding="40" RowDefinitions="*,Auto,*">
        <VerticalStackLayout Grid.Row="1" Spacing="16" MaximumWidthRequest="380" HorizontalOptions="Center">

            <Label Text="GYMERS" Style="{StaticResource H1}" HorizontalOptions="Center" />
            <Label Text="Sign in to manage your gym" Style="{StaticResource Muted}"
                   HorizontalOptions="Center" />

            <Entry Placeholder="Username" Text="{Binding Username}" />
            <Entry Placeholder="Password" Text="{Binding Password}" IsPassword="True" />

            <Label Text="{Binding ErrorMessage}" TextColor="{StaticResource Danger}"
                   IsVisible="{Binding ErrorMessage, Converter={StaticResource StringNotEmptyToBoolConverter}}" />

            <Button Text="Sign In" Command="{Binding LoginCommand}"
                    Style="{StaticResource PrimaryButton}"
                    IsEnabled="{Binding IsBusy, Converter={StaticResource InverseBoolConverter}}" />

            <ActivityIndicator IsRunning="{Binding IsBusy}" IsVisible="{Binding IsBusy}" />

        </VerticalStackLayout>
    </Grid>
</ContentPage>
```

- [ ] **Step 6: Write `LoginPage.xaml.cs`**

```csharp
using Gymers.ViewModels.Auth;

namespace Gymers.Views.Auth;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
```

- [ ] **Step 7: Add the converters** referenced in XAML — create `Gymers/Converters/StringNotEmptyToBoolConverter.cs` and `InverseBoolConverter.cs`

```csharp
using System.Globalization;

namespace Gymers.Converters;

public class StringNotEmptyToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrWhiteSpace(value as string);
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;
}
```

Register them in `App.xaml`:

```xml
<Application.Resources>
    <ResourceDictionary>
        <converters:StringNotEmptyToBoolConverter x:Key="StringNotEmptyToBoolConverter" />
        <converters:InverseBoolConverter x:Key="InverseBoolConverter" />
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="Resources/Styles/Colors.xaml" />
            <ResourceDictionary Source="Resources/Styles/Styles.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

Add `xmlns:converters="clr-namespace:Gymers.Converters"` at the `<Application>` element.

- [ ] **Step 8: Build**

```bash
dotnet build
```

- [ ] **Step 9: Register VM + Page in `MauiProgram.cs`**

Add these lines inside `CreateMauiApp()`, just below the existing `// Services` block (find the line that says `builder.Services.AddSingleton<StartupService>();` and add the two registrations after it):

```csharp
// Auth UI
builder.Services.AddTransient<Gymers.ViewModels.Auth.LoginViewModel>();
builder.Services.AddTransient<Gymers.Views.Auth.LoginPage>();
```

- [ ] **Step 10: Build**

```bash
dotnet build
```

Expected: `Build succeeded`.

- [ ] **Step 11: Commit**

```bash
git add Gymers.Core/ViewModels/Auth/LoginViewModel.cs Gymers/Views/Auth/ Gymers/Converters/ Gymers/App.xaml Gymers/MauiProgram.cs Gymers.Tests/ViewModels/LoginViewModelTests.cs
git commit -m "feat: add LoginPage + LoginViewModel with role-based routing (TDD)"
```

### Task 5.5 — `ChangePasswordPage` + `ChangePasswordViewModel`

**Files:**
- Create: `Gymers.Core/ViewModels/Auth/ChangePasswordViewModel.cs` (no MAUI deps — unit-tested)
- Create: `Gymers/Views/Auth/ChangePasswordPage.xaml`, `ChangePasswordPage.xaml.cs`
- Create: `Gymers.Tests/ViewModels/ChangePasswordViewModelTests.cs`

`ChangePasswordViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gymers.Models;
using Gymers.Services;

namespace Gymers.ViewModels.Auth;

public partial class ChangePasswordViewModel : BaseViewModel
{
    private readonly IAuthService _auth;
    private readonly IUserSession _session;
    private readonly INavigationService _nav;

    [ObservableProperty] private string _oldPassword = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;

    public ChangePasswordViewModel(IAuthService auth, IUserSession session, INavigationService nav)
    {
        _auth = auth;
        _session = session;
        _nav = nav;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(OldPassword) || string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "All fields are required.";
            return;
        }
        if (NewPassword.Length < 6)
        {
            ErrorMessage = "New password must be at least 6 characters.";
            return;
        }
        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return;
        }

        var user = _session.CurrentUser;
        if (user == null) { await _nav.GoToAsync("//Login"); return; }

        IsBusy = true;
        try
        {
            await _auth.ChangePasswordAsync(user.Id, OldPassword, NewPassword);
            await _nav.GoToAsync(user.Role == Role.Admin ? "//admin/dashboard" : "//staff/dashboard");
        }
        catch (InvalidCredentialsException)
        {
            ErrorMessage = "Old password is incorrect.";
        }
        catch (Exception)
        {
            ErrorMessage = "Could not update password.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

- [ ] **Step 1: Write failing tests `Gymers.Tests/ViewModels/ChangePasswordViewModelTests.cs`**

```csharp
using Gymers.Models;
using Gymers.Services;
using Gymers.ViewModels.Auth;
using NSubstitute;

namespace Gymers.Tests.ViewModels;

public class ChangePasswordViewModelTests
{
    private static (ChangePasswordViewModel vm, IAuthService auth, IUserSession session, INavigationService nav) CreateSut(User? sessionUser = null)
    {
        var auth = Substitute.For<IAuthService>();
        var session = new UserSession();
        if (sessionUser != null) session.SetUser(sessionUser);
        var nav = Substitute.For<INavigationService>();
        return (new ChangePasswordViewModel(auth, session, nav), auth, session, nav);
    }

    [Fact]
    public async Task SaveCommand_EmptyFields_ShowsError()
    {
        var (vm, auth, _, _) = CreateSut(new User { Id = 1, Role = Role.Admin });
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("All fields are required.", vm.ErrorMessage);
        await auth.DidNotReceive().ChangePasswordAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SaveCommand_TooShortNewPassword_ShowsError()
    {
        var (vm, _, _, _) = CreateSut(new User { Id = 1, Role = Role.Admin });
        vm.OldPassword = "old";
        vm.NewPassword = "abc";
        vm.ConfirmPassword = "abc";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Contains("at least 6", vm.ErrorMessage);
    }

    [Fact]
    public async Task SaveCommand_MismatchedConfirm_ShowsError()
    {
        var (vm, _, _, _) = CreateSut(new User { Id = 1, Role = Role.Admin });
        vm.OldPassword = "old";
        vm.NewPassword = "abcdef";
        vm.ConfirmPassword = "abcxyz";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Contains("do not match", vm.ErrorMessage);
    }

    [Fact]
    public async Task SaveCommand_ValidAdmin_CallsChangePasswordAndNavigatesAdmin()
    {
        var user = new User { Id = 7, Role = Role.Admin };
        var (vm, auth, _, nav) = CreateSut(user);
        vm.OldPassword = "oldpw";
        vm.NewPassword = "newpw1";
        vm.ConfirmPassword = "newpw1";

        await vm.SaveCommand.ExecuteAsync(null);

        await auth.Received(1).ChangePasswordAsync(7, "oldpw", "newpw1");
        await nav.Received(1).GoToAsync("//admin/dashboard");
    }

    [Fact]
    public async Task SaveCommand_WrongOldPassword_ShowsError()
    {
        var user = new User { Id = 7, Role = Role.Admin };
        var (vm, auth, _, _) = CreateSut(user);
        auth.ChangePasswordAsync(7, "oldpw", "newpw1").Returns<Task>(_ => throw new InvalidCredentialsException());

        vm.OldPassword = "oldpw";
        vm.NewPassword = "newpw1";
        vm.ConfirmPassword = "newpw1";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Old password is incorrect.", vm.ErrorMessage);
    }
}
```

- [ ] **Step 2: Run tests, verify compile failure** (`ChangePasswordViewModel` missing)

```bash
dotnet test --filter ChangePasswordViewModelTests
```

- [ ] **Step 3: Write `ChangePasswordViewModel.cs`** (the body shown above) at `Gymers.Core/ViewModels/Auth/ChangePasswordViewModel.cs`.

- [ ] **Step 4: Run tests, verify pass**

```bash
dotnet test --filter ChangePasswordViewModelTests
```

Expected: 5 tests passed.

- [ ] **Step 5: Write `ChangePasswordPage.xaml`** at `Gymers/Views/Auth/ChangePasswordPage.xaml` — same overall shape as Task 5.4's `LoginPage.xaml`: a centered `VerticalStackLayout` with three `Entry` boxes (old / new / confirm passwords, all `IsPassword="True"`), an error `Label`, a `Save` button bound to `SaveCommand`, and an `ActivityIndicator` bound to `IsBusy`. Use the same Core-assembly XAML reference:

```xml
xmlns:vm="clr-namespace:Gymers.ViewModels.Auth;assembly=Gymers.Core"
x:Class="Gymers.Views.Auth.ChangePasswordPage"
x:DataType="vm:ChangePasswordViewModel"
Title="Change Password"
Shell.NavBarIsVisible="False"
```

- [ ] **Step 6: Write `ChangePasswordPage.xaml.cs`**

```csharp
using Gymers.ViewModels.Auth;

namespace Gymers.Views.Auth;

public partial class ChangePasswordPage : ContentPage
{
    public ChangePasswordPage(ChangePasswordViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
```

- [ ] **Step 7: Register VM + Page in `MauiProgram.cs`**

Add below the LoginViewModel/LoginPage registrations from Task 5.4:

```csharp
builder.Services.AddTransient<Gymers.ViewModels.Auth.ChangePasswordViewModel>();
builder.Services.AddTransient<Gymers.Views.Auth.ChangePasswordPage>();
```

- [ ] **Step 8: Build**

```bash
dotnet build
```

- [ ] **Step 9: Commit**

```bash
git add Gymers.Core/ViewModels/Auth/ChangePasswordViewModel.cs Gymers/Views/Auth/ChangePasswordPage.xaml Gymers/Views/Auth/ChangePasswordPage.xaml.cs Gymers/MauiProgram.cs Gymers.Tests/ViewModels/ChangePasswordViewModelTests.cs
git commit -m "feat: add ChangePasswordPage + ViewModel (TDD)"
```

### Task 5.6 — `AppShell` with role-based shell items

**Files:** Modify `Gymers/AppShell.xaml` and `AppShell.xaml.cs`

`AppShell.xaml`:

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Shell xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
       xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
       xmlns:auth="clr-namespace:Gymers.Views.Auth"
       x:Class="Gymers.AppShell"
       FlyoutBehavior="Disabled">

    <ShellContent Title="Login" Route="Login" ContentTemplate="{DataTemplate auth:LoginPage}" />
    <ShellContent Title="Change Password" Route="ChangePassword" ContentTemplate="{DataTemplate auth:ChangePasswordPage}" />

    <!-- Admin and Staff shell items get added in their respective phases (6 and 14) -->

</Shell>
```

`AppShell.xaml.cs`:

```csharp
namespace Gymers;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        FlyoutBehavior = FlyoutBehavior.Disabled;
    }
}
```

Update `App.xaml.cs`:

```csharp
namespace Gymers;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();
    }
}
```

- [ ] **Build, run, verify the LoginPage shows up**

```bash
dotnet build -t:Run -f net8.0-maccatalyst
```

Expected: app opens to a login screen. Enter `admin` / `admin123` → see ChangePasswordPage prompt → enter new password → confirm → app navigates somewhere (will be a blank Shell since admin/staff shell items aren't added yet — that's fine).

- [ ] **Commit**

```bash
git add Gymers/AppShell.xaml Gymers/AppShell.xaml.cs Gymers/App.xaml.cs
git commit -m "feat: AppShell starts at LoginPage; password-change route registered"
```

---

## Phase 6 — Admin Shell + Dashboard + Member Management

For each page from this phase onward, the rhythm is:

1. Create the ViewModel with the listed properties and commands.
2. Create the XAML page binding to the ViewModel.
3. Register the page and ViewModel in `MauiProgram.cs` (in the **same commit** that introduces them — see Task 5.2's convention note).
4. Add the page to the relevant `<FlyoutItem>` (admin) or `<TabBar>` (staff) in `AppShell.xaml` (in the same commit, or the next one — never reference a page from Shell *before* the page exists).
5. Build and run; smoke-test the page manually.
6. Commit.

> **VM unit tests in Phase 6 onward are deferred to the manual smoke checklist (Task 16.1).** Most VMs in this phase use MAUI APIs (`FilePicker`, `MediaPicker`, `Launcher`, `DisplayAlert`, `[QueryProperty]`) and therefore live in `Gymers/ViewModels/` rather than `Gymers.Core`. `Gymers.Tests` cannot reference the MAUI assembly, so those VMs cannot be unit-tested without an architectural detour. This matches the spec's `"ViewModels: tests after the page works in dev"` guidance.

Don't skip the smoke test — it's the only check that XAML bindings, navigation routes, and DI wiring are correct.

### Task 6.1 — AppShell role-aware flyout (no admin pages yet)

**Files:** Modify `Gymers/AppShell.xaml.cs`, `Gymers/App.xaml.cs`, `Gymers/MauiProgram.cs`

This task only wires up the **behavior** of the shell so it can react to login. Adding the actual admin `FlyoutItem` is deferred to Task 6.2 — by then `AdminDashboardPage` exists and the XAML can reference it without a missing-type build failure.

- [ ] **Step 1: Update `AppShell.xaml.cs` to subscribe to `IUserSession.Changed`**

```csharp
using Gymers.Services;

namespace Gymers;

public partial class AppShell : Shell
{
    public AppShell(IUserSession session)
    {
        InitializeComponent();
        session.Changed += (_, _) =>
        {
            FlyoutBehavior = session.IsAdmin ? FlyoutBehavior.Flyout : FlyoutBehavior.Disabled;
        };
        FlyoutBehavior = FlyoutBehavior.Disabled;
    }
}
```

- [ ] **Step 2: Register `AppShell` as transient in `MauiProgram.cs`**

Add this line near the other DI registrations (anywhere inside `CreateMauiApp()` before `var app = builder.Build();`):

```csharp
builder.Services.AddTransient<AppShell>();
```

- [ ] **Step 3: Update `Gymers/App.xaml.cs` to resolve `AppShell` from DI**

```csharp
namespace Gymers;

public partial class App : Application
{
    public App(IServiceProvider services)
    {
        InitializeComponent();
        MainPage = services.GetRequiredService<AppShell>();
    }
}
```

- [ ] **Step 4: Build & smoke**

```bash
dotnet build -t:Run -f net8.0-maccatalyst
```

Expected: app opens at LoginPage. Logging in as Admin doesn't yet show a flyout (no `FlyoutItem` exists), but the app should not crash. Force-quit.

- [ ] **Step 5: Commit**

```bash
git add Gymers/AppShell.xaml.cs Gymers/App.xaml.cs Gymers/MauiProgram.cs
git commit -m "feat: register AppShell with role-aware flyout behavior"
```

### Task 6.2 — `AdminDashboardPage`

**Files:**
- Create: `Gymers/ViewModels/Admin/AdminDashboardViewModel.cs`
- Create: `Gymers/Views/Admin/AdminDashboardPage.xaml`(.cs)

`AdminDashboardViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gymers.Services;

namespace Gymers.ViewModels.Admin;

public partial class AdminDashboardViewModel : BaseViewModel
{
    private readonly IReportService _reports;

    [ObservableProperty] private int _totalActiveMembers;
    [ObservableProperty] private int _todayCheckIns;
    [ObservableProperty] private int _expiringSoon;
    [ObservableProperty] private decimal _monthlyRevenue;

    public AdminDashboardViewModel(IReportService reports) => _reports = reports;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var k = await _reports.GetDashboardAsync();
            TotalActiveMembers = k.TotalActiveMembers;
            TodayCheckIns = k.TodayCheckIns;
            ExpiringSoon = k.ExpiringWithinSevenDays;
            MonthlyRevenue = k.MonthlyRevenue;
        }
        finally { IsBusy = false; }
    }
}
```

`AdminDashboardPage.xaml` — four Card-styled `Border`s in a 2x2 `Grid`, each showing a label + a big number; the page calls `LoadCommand` in `OnAppearing`.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:Gymers.ViewModels.Admin"
             x:Class="Gymers.Views.Admin.AdminDashboardPage"
             x:DataType="vm:AdminDashboardViewModel"
             Title="Dashboard">

    <ScrollView>
        <VerticalStackLayout Padding="24" Spacing="20">
            <Label Text="Today" Style="{StaticResource H1}" />

            <Grid ColumnDefinitions="*,*" RowDefinitions="*,*" ColumnSpacing="16" RowSpacing="16">

                <Border Grid.Row="0" Grid.Column="0" Style="{StaticResource Card}">
                    <VerticalStackLayout Spacing="6">
                        <Label Text="Active Members" Style="{StaticResource Muted}" />
                        <Label Text="{Binding TotalActiveMembers}" FontSize="32" FontAttributes="Bold" />
                    </VerticalStackLayout>
                </Border>

                <Border Grid.Row="0" Grid.Column="1" Style="{StaticResource Card}">
                    <VerticalStackLayout Spacing="6">
                        <Label Text="Today's Check-ins" Style="{StaticResource Muted}" />
                        <Label Text="{Binding TodayCheckIns}" FontSize="32" FontAttributes="Bold" />
                    </VerticalStackLayout>
                </Border>

                <Border Grid.Row="1" Grid.Column="0" Style="{StaticResource Card}">
                    <VerticalStackLayout Spacing="6">
                        <Label Text="Expiring This Week" Style="{StaticResource Muted}" />
                        <Label Text="{Binding ExpiringSoon}" FontSize="32" FontAttributes="Bold" />
                    </VerticalStackLayout>
                </Border>

                <Border Grid.Row="1" Grid.Column="1" Style="{StaticResource Card}">
                    <VerticalStackLayout Spacing="6">
                        <Label Text="Monthly Revenue" Style="{StaticResource Muted}" />
                        <Label Text="{Binding MonthlyRevenue, StringFormat='₱ {0:N2}'}"
                               FontSize="32" FontAttributes="Bold" />
                    </VerticalStackLayout>
                </Border>
            </Grid>
        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

`AdminDashboardPage.xaml.cs`:

```csharp
using Gymers.ViewModels.Admin;

namespace Gymers.Views.Admin;

public partial class AdminDashboardPage : ContentPage
{
    private readonly AdminDashboardViewModel _vm;

    public AdminDashboardPage(AdminDashboardViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
    }
}
```

VM unit test deferred (see Phase 6 intro). Validate via the Phase 16 smoke test ("Admin Dashboard: 4 KPI cards show numbers").

- [ ] **Step: Register `AdminDashboardPage` and `AdminDashboardViewModel` in `MauiProgram.cs`**

```csharp
builder.Services.AddTransient<Gymers.ViewModels.Admin.AdminDashboardViewModel>();
builder.Services.AddTransient<Gymers.Views.Admin.AdminDashboardPage>();
```

- [ ] **Step: Add the admin `FlyoutItem` to `AppShell.xaml`** (now safe — `AdminDashboardPage` exists)

Add these to the `<Shell>` root element:
```xml
xmlns:admin="clr-namespace:Gymers.Views.Admin"
```

Inside the `<Shell>` body, after the existing `<ShellContent>` for Login/ChangePassword:
```xml
<FlyoutItem Title="Admin" Route="admin" FlyoutDisplayOptions="AsMultipleItems">
    <ShellContent Title="Dashboard" Route="dashboard"
                  ContentTemplate="{DataTemplate admin:AdminDashboardPage}" />
    <!-- additional admin shell-content entries are added in subsequent tasks -->
</FlyoutItem>
```

- [ ] **Step: Build, log in as admin, see the dashboard.** Commit: `feat: add AdminDashboard with KPI cards`.

### Task 6.3 — `MembersListPage`

**Files:**
- Create: `Gymers/ViewModels/Admin/MembersListViewModel.cs`
- Create: `Gymers/Views/Admin/MembersListPage.xaml`(.cs)

ViewModel responsibilities:
- `Members: ObservableCollection<Member>` — bound to a `CollectionView`.
- `SearchText` — when changed, debounce 250ms, then call `IMemberRepository.SearchActiveAsync` (or `ListByStatusAsync`).
- `SelectedStatus` — bound to a `Picker` (`All | Active | Inactive | Expired`).
- `LoadCommand`, `OpenMemberCommand(int memberId)`, `NewMemberCommand`.

`MembersListViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gymers.Data.Repositories;
using Gymers.Models;
using Gymers.Services;

namespace Gymers.ViewModels.Admin;

public partial class MembersListViewModel : BaseViewModel
{
    private readonly IMemberRepository _members;
    private readonly INavigationService _nav;

    public ObservableCollection<Member> Members { get; } = new();

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedStatus = "All";

    public string[] StatusOptions { get; } = new[] { "All", "Active", "Inactive", "Expired" };

    public MembersListViewModel(IMemberRepository members, INavigationService nav)
    {
        _members = members;
        _nav = nav;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            List<Member> list;
            if (!string.IsNullOrWhiteSpace(SearchText))
                list = await _members.SearchActiveAsync(SearchText);
            else if (SelectedStatus == "All")
                list = await _members.ListActiveAsync();
            else
                list = await _members.ListByStatusAsync(Enum.Parse<MemberStatus>(SelectedStatus));

            Members.Clear();
            foreach (var m in list) Members.Add(m);
        }
        finally { IsBusy = false; }
    }

    partial void OnSearchTextChanged(string value) => _ = LoadAsync();
    partial void OnSelectedStatusChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private Task OpenMemberAsync(int memberId) =>
        _nav.GoToAsync($"//admin/memberDetail?id={memberId}");

    [RelayCommand]
    private Task NewMemberAsync() => _nav.GoToAsync("//admin/memberForm");
}
```

XAML: a `Grid` with `RowDefinitions="Auto,Auto,*"`. Row 0: `SearchBar` bound to `SearchText`. Row 1: `Picker` bound to `StatusOptions` and `SelectedStatus`. Row 2: `CollectionView` bound to `Members`, each item template is a `Border` (Card style) showing FullName, ContactNumber, Status badge.

```xml
<CollectionView ItemsSource="{Binding Members}" SelectionMode="None">
    <CollectionView.ItemTemplate>
        <DataTemplate x:DataType="models:Member">
            <Border Style="{StaticResource Card}" Margin="0,4">
                <Border.GestureRecognizers>
                    <TapGestureRecognizer Command="{Binding Source={RelativeSource AncestorType={x:Type vm:MembersListViewModel}}, Path=OpenMemberCommand}"
                                          CommandParameter="{Binding Id}" />
                </Border.GestureRecognizers>
                <Grid ColumnDefinitions="*,Auto">
                    <VerticalStackLayout Spacing="2">
                        <Label Text="{Binding FullName}" Style="{StaticResource H2}" />
                        <Label Text="{Binding ContactNumber}" Style="{StaticResource Muted}" />
                    </VerticalStackLayout>
                    <Label Grid.Column="1" Text="{Binding Status}" VerticalOptions="Center" />
                </Grid>
            </Border>
        </DataTemplate>
    </CollectionView.ItemTemplate>
</CollectionView>
```

(Add `xmlns:models="clr-namespace:Gymers.Models"`, `xmlns:vm="clr-namespace:Gymers.ViewModels.Admin"`.)

Tests: `LoadAsync_NoSearch_All_ReturnsAllActive`; `LoadAsync_WithSearch_FiltersByQuery`; `OnSearchTextChanged_TriggersLoad`.

Register page + VM, register the route via `Routing.RegisterRoute("admin/membersList", typeof(MembersListPage))` and add it as a `ShellContent`.

Commit: `feat: add MembersListPage with status filter + search (TDD)`.

### Task 6.4 — `MemberFormPage` (add/edit + photo capture)

**Files:**
- Create: `Gymers/ViewModels/Admin/MemberFormViewModel.cs`
- Create: `Gymers/Views/Admin/MemberFormPage.xaml`(.cs)

Key aspects:
- Accepts an optional `MemberId` query parameter; null = new member.
- `MembershipType` `Picker` + `MembershipStartDate` `DatePicker`. The end date is computed via `IMembershipService.ComputeEndDate` and shown read-only.
- Photo: two buttons — "Choose File" and "Take Photo". Both update `PhotoSourcePath` (a local file path). On Save, `IPhotoStorageService.SavePhotoAsync` copies to AppData and the returned relative path is stored as `Member.PhotoPath`.
- Validation: FullName required, ContactNumber required, MembershipStartDate required.
- Save flow: insert or update via `IMemberRepository`, then call `IMembershipService.RecomputeStatusAsync`, then navigate back.

`MemberFormViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gymers.Data.Repositories;
using Gymers.Models;
using Gymers.Services;

namespace Gymers.ViewModels.Admin;

[QueryProperty(nameof(MemberId), "id")]
public partial class MemberFormViewModel : BaseViewModel
{
    private readonly IMemberRepository _members;
    private readonly IMembershipService _membership;
    private readonly IPhotoStorageService _photos;
    private readonly INavigationService _nav;

    [ObservableProperty] private int? _memberId;
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _contactNumber = string.Empty;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private DateTime? _dateOfBirth;
    [ObservableProperty] private string? _address;
    [ObservableProperty] private string? _photoSourcePath;
    [ObservableProperty] private string? _photoStoredPath;
    [ObservableProperty] private MembershipType _membershipType = MembershipType.Monthly;
    [ObservableProperty] private DateTime _membershipStartDate = DateTime.Today;
    [ObservableProperty] private DateTime _membershipEndDate = DateTime.Today.AddMonths(1);
    [ObservableProperty] private string? _notes;

    public MembershipType[] MembershipTypes { get; } = Enum.GetValues<MembershipType>();

    public MemberFormViewModel(IMemberRepository members, IMembershipService membership,
        IPhotoStorageService photos, INavigationService nav)
    {
        _members = members;
        _membership = membership;
        _photos = photos;
        _nav = nav;
    }

    partial void OnMembershipTypeChanged(MembershipType v) => RecomputeEndDate();
    partial void OnMembershipStartDateChanged(DateTime v) => RecomputeEndDate();
    private void RecomputeEndDate() => MembershipEndDate = _membership.ComputeEndDate(MembershipStartDate, MembershipType);

    partial void OnMemberIdChanged(int? value)
    {
        if (value.HasValue) _ = LoadAsync(value.Value);
    }

    private async Task LoadAsync(int id)
    {
        IsBusy = true;
        try
        {
            var m = await _members.GetByIdAsync(id);
            if (m == null) return;
            FullName = m.FullName;
            ContactNumber = m.ContactNumber;
            Email = m.Email;
            DateOfBirth = m.DateOfBirth;
            Address = m.Address;
            PhotoStoredPath = m.PhotoPath;
            MembershipType = m.MembershipType;
            MembershipStartDate = m.MembershipStartDate;
            MembershipEndDate = m.MembershipEndDate;
            Notes = m.Notes;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PickPhotoAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            FileTypes = FilePickerFileType.Images,
            PickerTitle = "Choose member photo"
        });
        if (file != null) PhotoSourcePath = file.FullPath;
    }

    [RelayCommand]
    private async Task CapturePhotoAsync()
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            ErrorMessage = "Webcam is not available on this machine.";
            return;
        }
        var file = await MediaPicker.Default.CapturePhotoAsync();
        if (file != null) PhotoSourcePath = file.FullPath;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(ContactNumber))
        {
            ErrorMessage = "Full name and contact number are required.";
            return;
        }

        IsBusy = true;
        try
        {
            // Save photo file if a new one was picked/captured.
            if (!string.IsNullOrEmpty(PhotoSourcePath))
            {
                PhotoStoredPath = await _photos.SavePhotoAsync(PhotoSourcePath, FullName);
                PhotoSourcePath = null;
            }

            Member m;
            if (MemberId.HasValue)
            {
                m = await _members.GetByIdAsync(MemberId.Value)
                    ?? throw new InvalidOperationException("Member missing");
            }
            else
            {
                m = new Member { Status = MemberStatus.Active };
            }

            m.FullName = FullName.Trim();
            m.ContactNumber = ContactNumber.Trim();
            m.Email = Email;
            m.DateOfBirth = DateOfBirth;
            m.Address = Address;
            m.PhotoPath = PhotoStoredPath;
            m.MembershipType = MembershipType;
            m.MembershipStartDate = MembershipStartDate;
            m.MembershipEndDate = MembershipEndDate;
            m.Notes = Notes;

            if (MemberId.HasValue) await _members.UpdateAsync(m);
            else { await _members.InsertAsync(m); MemberId = m.Id; }

            await _membership.RecomputeStatusAsync(m.Id);
            await _nav.GoBackAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Could not save: " + ex.Message;
        }
        finally { IsBusy = false; }
    }
}
```

XAML: `ScrollView` containing a `VerticalStackLayout` with all the entries, the photo preview (`Image` + two buttons), the membership controls, and a `Save` button.

Add Mac Catalyst camera permission entry to `Platforms/MacCatalyst/Info.plist`:

```xml
<key>NSCameraUsageDescription</key>
<string>Capture member ID photos.</string>
```

Tests cover: empty FullName rejected; valid input calls `_members.InsertAsync` for new member and `UpdateAsync` for existing; photo picking populates `PhotoSourcePath`.

Register page + VM. Wire route `admin/memberForm` and `admin/memberDetail`.

Commit: `feat: add MemberFormPage with photo capture (TDD)`.

### Task 6.5 — `MemberDetailPage`

**Files:**
- Create: `Gymers/ViewModels/Admin/MemberDetailViewModel.cs`
- Create: `Gymers/Views/Admin/MemberDetailPage.xaml`(.cs)

Properties:
- `Member` (the loaded entity)
- `Payments`, `Attendances`, `CurrentWorkoutPlan`, `CurrentTrainerAssignment`
- Commands: `EditCommand`, `RenewCommand`, `MarkAsRemovedCommand`, `LoadCommand`

`RenewCommand` navigates to `admin/paymentForm?memberId={id}&type=Renewal`. `MarkAsRemovedCommand` shows a confirmation dialog (`DisplayAlert`), then calls `_members.DeactivateAsync` and navigates back.

XAML: a header with the photo + name + status badge, then a `TabView` (or just sections in a `ScrollView`) for Payments, Attendance, Workout Plan, Trainer.

Tests: `LoadAsync` populates all properties via mocked repos; `MarkAsRemovedAsync` calls `_members.DeactivateAsync(id)`.

Commit: `feat: add MemberDetailPage with tabs and remove flow (TDD)`.

### Task 6.6 — Wire member routes into AppShell

**Files:** Modify `Gymers/AppShell.xaml`, `MauiProgram.cs`

Add to AppShell admin `<FlyoutItem>`:

```xml
<ShellContent Title="Members" Route="members" ContentTemplate="{DataTemplate admin:MembersListPage}" />
```

In `MauiProgram.cs` after `CreateBuilder`, register hidden routes (pages reachable via navigation but not flyout items):

```csharp
Routing.RegisterRoute("admin/memberForm", typeof(Views.Admin.MemberFormPage));
Routing.RegisterRoute("admin/memberDetail", typeof(Views.Admin.MemberDetailPage));
```

Build, run, sign in as admin → flyout shows Dashboard + Members → tap a member → opens detail → "Edit" opens form. Smoke test the full add/edit/list cycle.

Commit: `feat: wire member routes into AppShell`.

---

## Phase 7 — Payments

> **Phase 7 onward — task density.** Tasks from this point are described in condensed form: ViewModel responsibilities are listed by property/command name, XAML is described in prose, and `Tests:` lines list the test names a future "VM-testability refactor" would write. The patterns shown in Tasks 5.4 (`LoginViewModel`), 5.5 (`ChangePasswordViewModel`), 6.2 (`AdminDashboardViewModel`), and 6.4 (`MemberFormViewModel`) are the templates — when in doubt, copy their shape (constructor injection of services, `[ObservableProperty]` for state, `[RelayCommand]` for actions, `try/finally` around `IsBusy`, `_nav.GoToAsync` for navigation). VM unit tests in this phase remain deferred to Phase 16 smoke testing per the Phase 6 intro.

### Task 7.1 — `PaymentsListPage`

ViewModel: `Payments: ObservableCollection<Payment>`, `FromDate`, `ToDate`, `LoadCommand`. `OpenReceiptCommand(int paymentId)` opens `Payment.ReceiptPdfPath` via `Launcher.OpenAsync(new OpenFileRequest(...))`.

XAML: two `DatePicker`s for date range; a `CollectionView` of payments showing date, member name (looked up via `_members.GetByIdAsync`), amount formatted as `₱ {0:N2}`, type, receipt #.

Tests: `LoadAsync_FiltersByRange`; `OpenReceiptCommand_OpensFile`.

Commit: `feat: add PaymentsListPage (TDD)`.

### Task 7.2 — `PaymentFormPage`

`PaymentFormViewModel` accepts `[QueryProperty]` for `MemberId` and `Type`. Properties: `Member`, `Amount`, `PaymentType`, `PaymentMethod`, `Notes`. `SaveCommand` calls `IPaymentService.RecordAsync`, then opens the resulting PDF via `Launcher.OpenAsync` and navigates back.

```csharp
[RelayCommand]
private async Task SaveAsync()
{
    if (Member == null || Amount <= 0)
    {
        ErrorMessage = "Member and amount are required.";
        return;
    }

    IsBusy = true;
    try
    {
        var p = await _paymentService.RecordAsync(
            Member.Id, Amount, PaymentType, PaymentMethod,
            _session.CurrentUser!.Id, Notes);
        if (!string.IsNullOrEmpty(p.ReceiptPdfPath))
            await Launcher.OpenAsync(new OpenFileRequest("Receipt", new ReadOnlyFile(p.ReceiptPdfPath)));
        await _nav.GoBackAsync();
    }
    catch (Exception ex) { ErrorMessage = "Could not save: " + ex.Message; }
    finally { IsBusy = false; }
}
```

`Member` is selected via a typeahead (call `IMemberRepository.SearchActiveAsync`).

Tests: `SaveAsync_NoMember_ShowsError`; `SaveAsync_Valid_CallsPaymentService`.

Add to AppShell: `<ShellContent Title="Payments" Route="payments" .../>`. Register route `admin/paymentForm`.

Commit: `feat: add PaymentFormPage with PDF generation + auto-open (TDD)`.

---

## Phase 8 — Attendance (Admin view)

### Task 8.1 — `AttendanceListPage`

VM: `Attendances`, `FromDate`, `ToDate`, optional `MemberFilter`. `LoadCommand` calls `IAttendanceRepository.ListBetweenAsync` (or for-member if filter set).

XAML: `DatePicker`s + an optional member typeahead + `CollectionView` showing time + member name.

Test: `LoadAsync_PopulatesAttendances`.

Add to AppShell: `<ShellContent Title="Attendance" Route="attendance" .../>`.

Commit: `feat: add AttendanceListPage (TDD)`.

---

## Phase 9 — Trainers

### Task 9.1 — `TrainersListPage` and `TrainerFormPage`

`TrainersListViewModel`: `Trainers` collection, `LoadCommand`, `NewCommand`, `OpenCommand(int id)`.

`TrainerFormViewModel`: `[QueryProperty(nameof(TrainerId), "id")]`, fields for FullName, ContactNumber, Email, Specialization, AvailabilityNotes (multi-line), HourlyRate, HireDate. `SaveCommand` inserts or updates via `ITrainerService`.

Tests: list loads via `_trainers.ListActiveAsync`; form save calls `_trainerService.CreateAsync` for new, `UpdateAsync` for existing.

Add to AppShell: `<ShellContent Title="Trainers" Route="trainers" .../>`. Register routes `admin/trainerForm`, `admin/trainerDetail`.

Commit: `feat: add Trainers list + form pages (TDD)`.

### Task 9.2 — `TrainerDetailPage` and `TrainerAssignmentPage`

`TrainerDetailViewModel`: loads trainer + assigned members (via `ITrainerAssignmentRepository.ListForTrainerAsync` joined to `IMemberRepository`).

`TrainerAssignmentViewModel`: select Member from typeahead, select Trainer from typeahead, set StartDate, optional Notes. `SaveCommand` calls `ITrainerService.AssignToMemberAsync`.

Test: `AssignAsync_CallsTrainerService_WithExpectedArgs`.

Commit: `feat: add Trainer detail + member assignment (TDD)`.

---

## Phase 10 — Workout Plans + Exercises

### Task 10.1 — `ExercisesListPage` and `ExerciseFormPage`

Trivial CRUD for the master library. Same shape as Trainers list/form. Each exercise: Name, MuscleGroup (free text or simple picker), Description.

Add to AppShell: `<ShellContent Title="Exercises" Route="exercises" .../>`.

Commit: `feat: add Exercises CRUD (TDD)`.

### Task 10.2 — `WorkoutTemplatesListPage` and `WorkoutPlanFormPage`

`WorkoutPlanFormViewModel` — most complex form so far. Properties:
- `PlanId` (query param)
- `PlanName`, `PlanDescription`
- `Exercises: ObservableCollection<WorkoutPlanExerciseEditor>` — local edit-state class wrapping each exercise:

```csharp
public partial class WorkoutPlanExerciseEditor : ObservableObject
{
    [ObservableProperty] private int _exerciseId;
    [ObservableProperty] private string _exerciseName = string.Empty;
    [ObservableProperty] private int _sets;
    [ObservableProperty] private int _reps;
    [ObservableProperty] private decimal? _weight;
    [ObservableProperty] private int _order;
    [ObservableProperty] private string? _notes;
}
```

Commands: `AddExerciseCommand` (opens a picker dialog over `IExerciseRepository.ListAllAsync`), `RemoveExerciseCommand(WorkoutPlanExerciseEditor)`, `MoveUpCommand`/`MoveDownCommand`, `SaveCommand`.

`SaveCommand` builds a `WorkoutPlan` entity, calls `IWorkoutPlanService.CreateTemplateAsync`, or for edit calls `ReplaceExercisesAsync` after updating the plan.

Tests: `AddExercise_AppendsToCollection`; `Save_NoExercises_ShowsError`; `Save_Valid_CallsService`.

XAML: name + description fields, then a `CollectionView` of exercises with editable `Stepper`/`Entry` for sets/reps/weight per row, plus add/remove buttons.

Commit: `feat: add WorkoutPlan templates + form (TDD)`.

### Task 10.3 — `AssignWorkoutPlanPage`

VM: `Templates: ObservableCollection<WorkoutPlan>` (loaded from `ListTemplatesAsync`), `SelectedTemplateId`, `SelectedMemberId`, `SelectedTrainerId`. `AssignCommand` calls `IWorkoutPlanService.AssignTemplateToMemberAsync`.

Reachable from `MemberDetailPage` ("Assign Plan" button). Optional: also as standalone page.

Tests: `AssignAsync_CallsService_WithExpectedArgs`.

Commit: `feat: add AssignWorkoutPlanPage (TDD)`.

---

## Phase 11 — Equipment

### Task 11.1 — `EquipmentListPage` + `EquipmentFormPage`

VM: `Equipment` collection, plus `DueForMaintenance` collection (computed via `IEquipmentService.ListDueForMaintenanceAsync(today.AddDays(7))`).

XAML: list with two sections (CollectionView with `IsGrouped="True"` or two CollectionViews). Items due for maintenance get a yellow accent border. Tap → form.

Form: Name, SerialNumber, Category picker, Status picker, Condition picker, PurchaseDate, LastMaintenanceDate, NextMaintenanceDate.

Tests: `LoadAsync_PopulatesBothCollections`; `Save_New_CallsCreate`.

Add to AppShell: `<ShellContent Title="Equipment" Route="equipment" .../>`.

Commit: `feat: add Equipment list + form with maintenance highlight (TDD)`.

---

## Phase 12 — Reports

### Task 12.1 — `ReportsPage`

VM: three tabs' worth of data — `MembershipSummary` (active/inactive/expired counts), `Revenue` (table of payments in range, total at top), `Attendance` (table of check-ins in range, count at top).

XAML: top-level `Picker` for tab choice (or a `TabbedPage` if you prefer). `DatePicker`s for the range. Cards for KPIs, `CollectionView`s for tables.

Tests: each tab's load command populates the right collection from the right service method.

Add to AppShell: `<ShellContent Title="Reports" Route="reports" .../>`.

Commit: `feat: add ReportsPage with three tabs (TDD)`.

---

## Phase 13 — Settings

### Task 13.1 — `SettingsPage`

VM: `StaffUsers: ObservableCollection<User>` (filtered Role=Staff), commands: `CreateStaffCommand`, `DeactivateStaffCommand(int id)`, `ResetPasswordCommand(int id)`. "About" section showing the DB file path (`FileSystem.AppDataDirectory + "/gymers.db3"`).

`CreateStaffCommand` shows a modal entry for username + full name. The new staff is created with `MustChangePassword=true` and a temporary password (display it once after creation: `"Temporary password: staff{timestamp}"`).

`ResetPasswordCommand` similar — generates a temp password, sets `MustChangePassword=true`, shows it once.

Tests: `CreateStaffAsync_CallsRepoInsert_WithRoleStaff`; `DeactivateStaffAsync_CallsRepoDeactivate`.

Add to AppShell: `<ShellContent Title="Settings" Route="settings" .../>`. Add a "Logout" button in this page that calls `IAuthService.LogoutAsync` then `_nav.GoToAsync("//Login")`.

Commit: `feat: add SettingsPage with staff management + logout (TDD)`.

---

## Phase 14 — Staff Shell

### Task 14.1 — Add Staff `TabBar` to AppShell

```xml
<TabBar Route="staff">
    <ShellContent Title="Dashboard" Route="dashboard"
                  ContentTemplate="{DataTemplate staff:StaffDashboardPage}" />
    <ShellContent Title="Check-In" Route="checkin"
                  ContentTemplate="{DataTemplate staff:CheckInPage}" />
    <ShellContent Title="Members" Route="members"
                  ContentTemplate="{DataTemplate staff:MemberSearchPage}" />
    <ShellContent Title="Payments" Route="payments"
                  ContentTemplate="{DataTemplate admin:PaymentFormPage}" />
    <ShellContent Title="Trainers" Route="trainers"
                  ContentTemplate="{DataTemplate staff:TrainerScheduleViewPage}" />
</TabBar>
```

Update `AppShell.xaml.cs` so `FlyoutBehavior = FlyoutBehavior.Disabled` for staff (TabBar is auto-shown).

Commit: `feat: add Staff TabBar with five tabs`.

### Task 14.2 — `StaffDashboardPage`

VM: same KPI cards as admin but limited (Today's Check-ins, Active Members). Plus a `RecentCheckIns: ObservableCollection<Attendance>` from `_attendance.ListTodayAsync()`.

Buttons: "New Check-In" → navigates to `//staff/checkin`. "Process Payment" → `//staff/payments`.

Tests: `LoadAsync_PopulatesData`.

Commit: `feat: add StaffDashboard (TDD)`.

### Task 14.3 — `CheckInPage`

VM:
- `SearchText` — typeahead (debounced) over `IMemberRepository.SearchActiveAsync`.
- `Candidates: ObservableCollection<Member>`.
- `TodayCheckIns: ObservableCollection<Attendance>` — refreshed after each check-in.
- `CheckInCommand(int memberId)` — calls `IAttendanceService.CheckInAsync`, shows a `Toast.Make($"Checked in {member.FullName}")`, refreshes `TodayCheckIns`.

XAML: top half a `SearchBar` + `CollectionView` of candidates, bottom half a `CollectionView` of today's check-ins.

Tests: `CheckInAsync_CallsAttendanceService`; `OnSearchTextChanged_TriggersSearch`.

Commit: `feat: add CheckInPage with typeahead (TDD)`.

### Task 14.4 — `MemberSearchPage` (read-only)

Same shape as `MembersListPage` but with edit/new buttons hidden. Tap a member → opens a simplified `MemberSummaryPage` that shows profile + current membership status (no edit button).

Commit: `feat: add staff MemberSearchPage (TDD)`.

### Task 14.5 — `TrainerScheduleViewPage`

VM: `Trainers: ObservableCollection<Trainer>` from `ITrainerRepository.ListActiveAsync`. Read-only display.

XAML: `CollectionView` of cards showing trainer name + specialization + AvailabilityNotes.

Test: `LoadAsync_LoadsTrainers`.

Commit: `feat: add staff TrainerScheduleViewPage (TDD)`.

### Task 14.6 — Verify staff full flow end-to-end

Manual smoke test:

```bash
dotnet build -t:Run -f net8.0-maccatalyst
```

- Log in as a Staff user (created earlier from Admin Settings).
- Confirm Tab bar shows 5 tabs and admin pages are not visible.
- Search for a member, check them in, see the count update.
- Process a walk-in payment, see the PDF open.
- View the Trainer Schedule tab.

If all pass: `git commit --allow-empty -m "test: verified staff smoke flow"`.

---

## Phase 15 — Visual Styling Pass (against Figma exports)

Pre-requisite: the team drops PNG exports of all designed screens into `designs/` (e.g., `designs/01-login.png`, `designs/02-admin-dashboard.png`, …). If those aren't present, this phase blocks until they are.

### Task 15.1 — Extract palette + spacing from exports

- [ ] **Step 1: Pick the cleanest 3-4 export PNGs** that contain the brand colors and primary surfaces.

- [ ] **Step 2: Use a color picker** (macOS Digital Color Meter, or `sips -g pixel <x> <y>`) to read hex values for: brand primary, accent, text-primary, text-muted, background, surface (cards), danger, success, border.

- [ ] **Step 3: Update `Resources/Styles/Colors.xaml`** with the actual hex values, replacing the placeholders.

- [ ] **Step 4: Read font + sizing decisions.** Note H1, H2, body, muted sizes. Update `Styles.xaml` with measured values.

- [ ] **Step 5: Commit the palette update**

```bash
git add Gymers/Resources/Styles/
git commit -m "style: extract palette + typography from Figma exports"
```

### Task 15.2 — Refine pages screen-by-screen

For each export PNG:

- [ ] **Step 1**: Open the page in MAUI dev (Hot Reload helps).
- [ ] **Step 2**: Compare to the export side-by-side. List divergences (spacing, color, alignment, missing elements).
- [ ] **Step 3**: Apply XAML changes one element at a time. Use `Padding`, `Margin`, `RowSpacing`, named styles already in `Styles.xaml`.
- [ ] **Step 4**: When the page matches, commit with `style: refine LoginPage to match Figma export` (replace page name).

Repeat for every page that has an export.

### Task 15.3 — Add custom fonts (if Figma uses non-system fonts)

If the export uses a custom typeface (e.g. Inter):

- [ ] Drop `.ttf` files into `Gymers/Resources/Fonts/`.
- [ ] Mark each as `MauiFont` in the `.csproj` (already auto-included with the default `<MauiFont Include="Resources\Fonts\*" />`).
- [ ] In `MauiProgram.CreateMauiApp()` `ConfigureFonts`, add: `fonts.AddFont("Inter-Regular.ttf", "Inter");`
- [ ] In `Styles.xaml`, set `<Setter Property="FontFamily" Value="Inter" />` on the base label/button styles.

Commit: `style: add Inter custom font`.

---

## Phase 16 — Smoke Test + Final Polish

### Task 16.1 — Manual smoke test checklist

Execute every item, document the outcome in `docs/superpowers/smoke-test-2026-05-02.md` (✓/✗ + notes for failures).

```
[ ] First-launch: app opens to Login page.
[ ] Login: admin/admin123 → ChangePassword forced; new pwd accepted; routes to Admin Dashboard.
[ ] Admin Dashboard: 4 KPI cards show numbers (zeros are fine on empty DB).
[ ] Members: + New → fill required fields → photo via Choose File → Save → appears in list.
[ ] Members: + New → photo via Take Photo → photo preview updates.
[ ] Members: edit existing member → field updates persist after navigating away and back.
[ ] Members: open a member → Renew button opens payment form prefilled.
[ ] Payment: create a Renewal payment → PDF opens → member's MembershipEndDate extended by the type period.
[ ] Payment: create a WalkIn payment → PDF opens → no membership change.
[ ] Receipt #: two payments in the same year produce sequential RCPT-{year}-{counter} numbers.
[ ] Attendance: Staff check-in for an active member → today's count increments.
[ ] Attendance: Staff search returns soft-deleted members? (must NOT appear).
[ ] Trainers: CRUD; assign trainer to member; ending current assignment when reassigning.
[ ] Workout: create exercise; create workout template with 3 exercises; assign template to a member; member's detail shows the assigned plan with the same exercises (modifying the assigned plan does NOT modify the template).
[ ] Equipment: CRUD; items due in 7 days highlighted on the list.
[ ] Reports: Membership Summary / Revenue / Attendance tabs each load with data.
[ ] Settings: create a Staff account (admin role does NOT). New staff temp password shown once. Login as staff → forced password change → routes to Staff TabBar (no admin pages visible).
[ ] Staff: tab bar has exactly 5 tabs; admin pages hidden.
[ ] Logout (Settings or Staff dashboard footer) returns to Login screen and clears UserSession.
[ ] Re-launch the app: previously created members/payments/etc. persist (DB written to disk).
[ ] Force-quit during a save: no DB corruption (relaunch and verify the last in-flight record either fully landed or didn't, never partial).
```

Fix any items marked ✗ in the checklist before committing.

Commit: `test: run manual smoke checklist; all items pass`.

### Task 16.2 — Code review pass

- [ ] **Step 1**: Run all tests and confirm green.

```bash
dotnet test
```

Expected: all tests pass.

- [ ] **Step 2**: Use the `superpowers:requesting-code-review` skill to get a code-review-quality pass over the whole codebase. Address any blocking findings; defer any non-blocking ones to a TODO list at the bottom of `README.md`.

- [ ] **Step 3**: Commit the fixes individually as you make them.

### Task 16.3 — Final tag

- [ ] **Step 1: Update `README.md`** with: how to build, how to run, default credentials, where the DB file lives.

- [ ] **Step 2: Tag the release**

```bash
git tag -a v1.0.0 -m "Gymers v1.0.0 — first complete demo build"
```

- [ ] **Step 3: Final commit**

```bash
git commit --allow-empty -m "chore: ready for demo"
```

---





