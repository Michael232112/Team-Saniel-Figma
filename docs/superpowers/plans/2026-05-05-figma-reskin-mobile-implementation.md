# Gymers Mobile (iOS) — Figma Reskin Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build five Admin/Staff iOS screens (Login, Dashboard, Members, Payments, Attendance) for the Gymers .NET MAUI app, matching the Figma design's tokens, components, and layout.

**Architecture:** Token-system-first. Phase 1 retargets the build and adds fonts. Phase 2 lays down design tokens (Colors, Typography, Spacing, Shadows, Styles). Phase 3 adds POCO models + hardcoded sample data. Phase 4 builds the icon font integration. Phase 5 builds the nine reusable XAML UserControls. Phase 6 wires AppShell + the five pages. Phase 7 verifies via build + iOS 26.2 simulator.

**Tech Stack:** .NET 10 MAUI, XAML, C#, iOS 26.2 simulator (Xcode), Manrope + Inter (Google Fonts), Lucide icon font.

**Spec:** `docs/superpowers/specs/2026-05-05-figma-reskin-mobile-design.md` (commit `017c02a`).

**Testing approach:** This project ships no unit tests in v1 (per spec §9). Each phase ends with `dotnet build Gymers/Gymers.csproj -f net10.0-ios` returning 0 errors. After Phase 7, the engineer runs the app in the iOS 26.2 simulator and visually compares each screen to its Figma frame.

**Plan deviations from spec (v1 simplifications):**
- Spec §3 lists `Resources/Images/admin_avatar.png` and `Resources/Images/coach_marcus.png`. The plan replaces these with generated initial-circle placeholders (`A` in `TopAppBar`, `MS` in the Coach Spotlight card). No image assets are downloaded.
- Spec §5 `TopAppBar` defines `AvatarSource` and `TrailingCommand` props. The plan drops both — avatar is a fixed initial circle, trailing icon is decorative (no tap handler) since v1 has no real actions wired up.

---

## File Structure

**New files:**
- `Gymers/Resources/Styles/Typography.xaml`
- `Gymers/Resources/Styles/Spacing.xaml`
- `Gymers/Resources/Styles/Shadows.xaml`
- `Gymers/Resources/Fonts/Manrope-Bold.ttf` (downloaded)
- `Gymers/Resources/Fonts/Manrope-ExtraBold.ttf` (downloaded)
- `Gymers/Resources/Fonts/Manrope-SemiBold.ttf` (downloaded)
- `Gymers/Resources/Fonts/Inter-Regular.ttf` (downloaded)
- `Gymers/Resources/Fonts/Inter-Medium.ttf` (downloaded)
- `Gymers/Resources/Fonts/Inter-SemiBold.ttf` (downloaded)
- `Gymers/Resources/Fonts/Lucide.ttf` (downloaded)
- `Gymers/Models/MembershipTier.cs`
- `Gymers/Models/Member.cs`
- `Gymers/Models/Payment.cs`
- `Gymers/Models/CheckIn.cs`
- `Gymers/Models/ClassSession.cs`
- `Gymers/Data/SampleData.cs`
- `Gymers/Controls/Icons.cs`
- `Gymers/Controls/DeltaChip.xaml` + `.xaml.cs`
- `Gymers/Controls/PrimaryButton.xaml` + `.xaml.cs`
- `Gymers/Controls/SecondaryButton.xaml` + `.xaml.cs`
- `Gymers/Controls/LabeledInput.xaml` + `.xaml.cs`
- `Gymers/Controls/SearchField.xaml` + `.xaml.cs`
- `Gymers/Controls/TopAppBar.xaml` + `.xaml.cs`
- `Gymers/Controls/BottomTabBar.xaml` + `.xaml.cs`
- `Gymers/Controls/KpiCard.xaml` + `.xaml.cs`
- `Gymers/Controls/ListRow.xaml` + `.xaml.cs`
- `Gymers/Pages/LoginPage.xaml` + `.xaml.cs`
- `Gymers/Pages/DashboardPage.xaml` + `.xaml.cs`
- `Gymers/Pages/MembersPage.xaml` + `.xaml.cs`
- `Gymers/Pages/PaymentsPage.xaml` + `.xaml.cs`
- `Gymers/Pages/AttendancePage.xaml` + `.xaml.cs`

**Modified files:**
- `Gymers/Gymers.csproj` (target framework, font items)
- `Gymers/App.xaml` (merged dictionaries)
- `Gymers/AppShell.xaml` + `.xaml.cs` (route to LoginPage + TabBar with 4 tabs)
- `Gymers/MauiProgram.cs` (font registration)
- `Gymers/Resources/Styles/Colors.xaml` (rewrite with Figma tokens)
- `Gymers/Resources/Styles/Styles.xaml` (rewrite — Card / CardMuted / Pill base styles)

**Deleted files:**
- `Gymers/MainPage.xaml`
- `Gymers/MainPage.xaml.cs`
- `Gymers/Resources/Fonts/OpenSans-Regular.ttf`
- `Gymers/Resources/Fonts/OpenSans-Semibold.ttf`

---

## Phase 1 — Project foundation

### Task 1: Retarget csproj to iOS, drop OpenSans, register custom font items

**Files:**
- Modify: `Gymers/Gymers.csproj`
- Delete: `Gymers/Resources/Fonts/OpenSans-Regular.ttf`
- Delete: `Gymers/Resources/Fonts/OpenSans-Semibold.ttf`

- [ ] **Step 1: Replace the entire content of `Gymers/Gymers.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<TargetFramework>net10.0-ios</TargetFramework>

		<OutputType>Exe</OutputType>
		<RootNamespace>Gymers</RootNamespace>
		<UseMaui>true</UseMaui>
		<SingleProject>true</SingleProject>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>

		<ApplicationTitle>Gymers</ApplicationTitle>
		<ApplicationId>com.companyname.gymers</ApplicationId>
		<ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
		<ApplicationVersion>1</ApplicationVersion>

		<SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">17.0</SupportedOSPlatformVersion>
	</PropertyGroup>

	<ItemGroup>
		<MauiIcon Include="Resources\AppIcon\appicon.svg" ForegroundFile="Resources\AppIcon\appiconfg.svg" Color="#002159" />
		<MauiSplashScreen Include="Resources\Splash\splash.svg" Color="#002159" BaseSize="128,128" />
		<MauiImage Include="Resources\Images\*" />
		<MauiFont Include="Resources\Fonts\*" />
		<MauiAsset Include="Resources\Raw\**" LogicalName="%(RecursiveDir)%(Filename)%(Extension)" />
	</ItemGroup>

	<ItemGroup>
		<PackageReference Include="Microsoft.Maui.Controls" Version="$(MauiVersion)" />
		<PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.0" />
	</ItemGroup>

</Project>
```

- [ ] **Step 2: Delete the OpenSans fonts**

```bash
rm Gymers/Resources/Fonts/OpenSans-Regular.ttf Gymers/Resources/Fonts/OpenSans-Semibold.ttf
```

- [ ] **Step 3: Verify the obj folder is cleaned for the framework switch**

```bash
rm -rf Gymers/obj Gymers/bin
```

- [ ] **Step 4: Restore packages**

Run: `dotnet restore Gymers/Gymers.csproj`
Expected: Restore completes with no errors. (May warn about workload — acceptable.)

- [ ] **Step 5: Commit**

```bash
git add Gymers/Gymers.csproj Gymers/Resources/Fonts
git commit -m "build: retarget Gymers to net10.0-ios; drop OpenSans fonts"
```

---

### Task 2: Download design + icon fonts

**Files:**
- Create: `Gymers/Resources/Fonts/Manrope-Bold.ttf`
- Create: `Gymers/Resources/Fonts/Manrope-ExtraBold.ttf`
- Create: `Gymers/Resources/Fonts/Manrope-SemiBold.ttf`
- Create: `Gymers/Resources/Fonts/Inter-Regular.ttf`
- Create: `Gymers/Resources/Fonts/Inter-Medium.ttf`
- Create: `Gymers/Resources/Fonts/Inter-SemiBold.ttf`
- Create: `Gymers/Resources/Fonts/Lucide.ttf`

- [ ] **Step 1: Download Manrope (3 weights) from Google Fonts API**

```bash
mkdir -p /tmp/gymers-fonts && cd /tmp/gymers-fonts
# Manrope variable font split into static weights — fetch the static distribution
curl -L -o manrope.zip "https://fonts.google.com/download?family=Manrope"
unzip -o manrope.zip -d manrope
cp manrope/static/Manrope-Bold.ttf      ${OLDPWD}/Gymers/Resources/Fonts/Manrope-Bold.ttf
cp manrope/static/Manrope-ExtraBold.ttf ${OLDPWD}/Gymers/Resources/Fonts/Manrope-ExtraBold.ttf
cp manrope/static/Manrope-SemiBold.ttf  ${OLDPWD}/Gymers/Resources/Fonts/Manrope-SemiBold.ttf
cd -
```

- [ ] **Step 2: Download Inter (3 weights)**

```bash
cd /tmp/gymers-fonts
curl -L -o inter.zip "https://fonts.google.com/download?family=Inter"
unzip -o inter.zip -d inter
cp inter/static/Inter_18pt-Regular.ttf  ${OLDPWD}/Gymers/Resources/Fonts/Inter-Regular.ttf
cp inter/static/Inter_18pt-Medium.ttf   ${OLDPWD}/Gymers/Resources/Fonts/Inter-Medium.ttf
cp inter/static/Inter_18pt-SemiBold.ttf ${OLDPWD}/Gymers/Resources/Fonts/Inter-SemiBold.ttf
cd -
```

(If Google's API path is unavailable, fall back to `https://github.com/rsms/inter/releases` for Inter and `https://github.com/sharanda/manrope/releases` for Manrope. The exact static-weight filenames must end up in `Resources/Fonts/` as listed above.)

- [ ] **Step 3: Download Lucide icon font**

```bash
curl -L -o Gymers/Resources/Fonts/Lucide.ttf https://unpkg.com/lucide-static@latest/font/lucide.ttf
```

- [ ] **Step 4: Save the Lucide info.json for codepoint lookup**

```bash
curl -L -o /tmp/gymers-fonts/lucide-info.json https://unpkg.com/lucide-static@latest/font/info.json
```

Keep this file around — Task 8 uses it.

- [ ] **Step 5: Verify all seven font files are non-zero size**

```bash
ls -la Gymers/Resources/Fonts/
```

Expected: seven `.ttf` files, each at least 30 KB.

- [ ] **Step 6: Commit**

```bash
git add Gymers/Resources/Fonts/
git commit -m "assets: add Manrope, Inter, and Lucide font files"
```

---

### Task 3: Register fonts in MauiProgram.cs

**Files:**
- Modify: `Gymers/MauiProgram.cs`

- [ ] **Step 1: Read the current MauiProgram.cs**

Run: `cat Gymers/MauiProgram.cs`

- [ ] **Step 2: Replace its content with the font-registering version**

```csharp
using Microsoft.Extensions.Logging;

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

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
```

- [ ] **Step 3: Build to confirm fonts compile in**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios`
Expected: Build succeeded. 0 errors. (Warnings about empty MainPage, missing routes, etc., are OK at this stage — those go away in later tasks.)

- [ ] **Step 4: Commit**

```bash
git add Gymers/MauiProgram.cs
git commit -m "build: register Manrope, Inter, and Lucide font aliases"
```

---

## Phase 2 — Design tokens

### Task 4: Rewrite Colors.xaml with Figma palette

**Files:**
- Modify: `Gymers/Resources/Styles/Colors.xaml`

- [ ] **Step 1: Replace the entire content of `Gymers/Resources/Styles/Colors.xaml`**

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">

    <!-- Surfaces -->
    <Color x:Key="BgApp">#F9F9F9</Color>
    <Color x:Key="Surface">#FFFFFF</Color>
    <Color x:Key="SurfaceMuted">#F4F3F3</Color>
    <Color x:Key="BorderSoft">#1AC4C6D2</Color> <!-- 10% alpha of #C4C6D2 -->

    <!-- Brand -->
    <Color x:Key="NavyDeep">#002159</Color>
    <Color x:Key="NavyMid">#10367D</Color>
    <Color x:Key="NavyHeading">#1E3A8A</Color>
    <Color x:Key="Periwinkle">#B1C5FF</Color>
    <Color x:Key="PeriwinkleLight">#DAE2FF</Color>
    <Color x:Key="PaleBlue">#DBEAFE</Color>

    <!-- Accent -->
    <Color x:Key="Lime">#C7F339</Color>
    <Color x:Key="LimeSoft">#4DC7F339</Color> <!-- 30% alpha of #C7F339 -->
    <Color x:Key="Olive">#516600</Color>
    <Color x:Key="OliveDark">#161E00</Color>

    <!-- Text -->
    <Color x:Key="TextPrimary">#1A1C1C</Color>
    <Color x:Key="TextSecondary">#444651</Color>
    <Color x:Key="TextMuted">#64748B</Color>

    <!-- Brushes for the few places that need them -->
    <SolidColorBrush x:Key="BgAppBrush" Color="{StaticResource BgApp}" />
    <SolidColorBrush x:Key="SurfaceBrush" Color="{StaticResource Surface}" />
    <SolidColorBrush x:Key="SurfaceMutedBrush" Color="{StaticResource SurfaceMuted}" />
    <SolidColorBrush x:Key="NavyDeepBrush" Color="{StaticResource NavyDeep}" />
    <SolidColorBrush x:Key="LimeBrush" Color="{StaticResource Lime}" />

    <!-- Gradients -->
    <LinearGradientBrush x:Key="LiveCapacityGradientBrush" StartPoint="0,0" EndPoint="1,0">
        <GradientStop Color="{StaticResource NavyMid}" Offset="0.0" />
        <GradientStop Color="{StaticResource Olive}"   Offset="1.0" />
    </LinearGradientBrush>

    <LinearGradientBrush x:Key="PrimaryButtonGradientBrush" StartPoint="0,0" EndPoint="0.13,1">
        <GradientStop Color="{StaticResource NavyDeep}" Offset="0.0" />
        <GradientStop Color="{StaticResource NavyMid}"  Offset="1.0" />
    </LinearGradientBrush>

</ResourceDictionary>
```

- [ ] **Step 2: Build to confirm syntax**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Gymers/Resources/Styles/Colors.xaml
git commit -m "style: replace template palette with Figma color tokens"
```

---

### Task 5: Add Typography.xaml

**Files:**
- Create: `Gymers/Resources/Styles/Typography.xaml`

- [ ] **Step 1: Create the file with all type styles**

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">

    <!-- Font family aliases (declared in MauiProgram.cs) -->
    <x:String x:Key="FontManropeBold">ManropeBold</x:String>
    <x:String x:Key="FontManropeExtraBold">ManropeExtraBold</x:String>
    <x:String x:Key="FontManropeSemiBold">ManropeSemiBold</x:String>
    <x:String x:Key="FontInterRegular">InterRegular</x:String>
    <x:String x:Key="FontInterMedium">InterMedium</x:String>
    <x:String x:Key="FontInterSemiBold">InterSemiBold</x:String>
    <x:String x:Key="FontLucide">LucideIcons</x:String>

    <!-- Headings (Manrope) -->
    <Style x:Key="DisplayKpi" TargetType="Label">
        <Setter Property="FontFamily" Value="ManropeExtraBold" />
        <Setter Property="FontSize" Value="48" />
        <Setter Property="LineHeight" Value="1.0" />
        <Setter Property="TextColor" Value="{StaticResource TextPrimary}" />
    </Style>

    <Style x:Key="H1Page" TargetType="Label">
        <Setter Property="FontFamily" Value="ManropeBold" />
        <Setter Property="FontSize" Value="30" />
        <Setter Property="LineHeight" Value="1.2" />
        <Setter Property="CharacterSpacing" Value="-0.75" />
        <Setter Property="TextColor" Value="{StaticResource NavyHeading}" />
    </Style>

    <Style x:Key="H2Section" TargetType="Label">
        <Setter Property="FontFamily" Value="ManropeBold" />
        <Setter Property="FontSize" Value="24" />
        <Setter Property="LineHeight" Value="1.33" />
        <Setter Property="TextColor" Value="{StaticResource TextPrimary}" />
    </Style>

    <Style x:Key="H3Card" TargetType="Label">
        <Setter Property="FontFamily" Value="ManropeBold" />
        <Setter Property="FontSize" Value="20" />
        <Setter Property="LineHeight" Value="1.4" />
        <Setter Property="TextColor" Value="{StaticResource TextPrimary}" />
    </Style>

    <Style x:Key="H4Item" TargetType="Label">
        <Setter Property="FontFamily" Value="ManropeBold" />
        <Setter Property="FontSize" Value="16" />
        <Setter Property="LineHeight" Value="1.5" />
        <Setter Property="TextColor" Value="{StaticResource TextPrimary}" />
    </Style>

    <Style x:Key="StatLg" TargetType="Label">
        <Setter Property="FontFamily" Value="ManropeBold" />
        <Setter Property="FontSize" Value="18" />
        <Setter Property="LineHeight" Value="1.55" />
        <Setter Property="TextColor" Value="{StaticResource TextPrimary}" />
    </Style>

    <!-- Body & UI (Inter) -->
    <Style x:Key="BodyMd" TargetType="Label">
        <Setter Property="FontFamily" Value="InterRegular" />
        <Setter Property="FontSize" Value="14" />
        <Setter Property="LineHeight" Value="1.42" />
        <Setter Property="TextColor" Value="{StaticResource TextSecondary}" />
    </Style>

    <Style x:Key="BodySm" TargetType="Label">
        <Setter Property="FontFamily" Value="InterRegular" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="LineHeight" Value="1.33" />
        <Setter Property="TextColor" Value="{StaticResource TextSecondary}" />
    </Style>

    <Style x:Key="LabelKpi" TargetType="Label">
        <Setter Property="FontFamily" Value="InterSemiBold" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="LineHeight" Value="1.33" />
        <Setter Property="CharacterSpacing" Value="0.6" />
        <Setter Property="TextTransform" Value="Uppercase" />
        <Setter Property="TextColor" Value="{StaticResource TextSecondary}" />
    </Style>

    <Style x:Key="LabelZone" TargetType="Label">
        <Setter Property="FontFamily" Value="InterSemiBold" />
        <Setter Property="FontSize" Value="10" />
        <Setter Property="LineHeight" Value="1.5" />
        <Setter Property="CharacterSpacing" Value="1.0" />
        <Setter Property="TextTransform" Value="Uppercase" />
        <Setter Property="TextColor" Value="{StaticResource TextSecondary}" />
    </Style>

    <Style x:Key="LabelTab" TargetType="Label">
        <Setter Property="FontFamily" Value="InterMedium" />
        <Setter Property="FontSize" Value="10" />
        <Setter Property="LineHeight" Value="1.5" />
        <Setter Property="CharacterSpacing" Value="0.5" />
        <Setter Property="TextTransform" Value="Uppercase" />
        <Setter Property="TextColor" Value="{StaticResource TextMuted}" />
    </Style>

    <Style x:Key="ButtonLg" TargetType="Label">
        <Setter Property="FontFamily" Value="InterSemiBold" />
        <Setter Property="FontSize" Value="14" />
        <Setter Property="LineHeight" Value="1.42" />
        <Setter Property="CharacterSpacing" Value="0.35" />
        <Setter Property="TextColor" Value="{StaticResource PeriwinkleLight}" />
        <Setter Property="HorizontalTextAlignment" Value="Center" />
    </Style>

    <Style x:Key="Caption" TargetType="Label">
        <Setter Property="FontFamily" Value="InterMedium" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="LineHeight" Value="1.33" />
        <Setter Property="TextColor" Value="{StaticResource TextSecondary}" />
    </Style>

</ResourceDictionary>
```

- [ ] **Step 2: Commit**

```bash
git add Gymers/Resources/Styles/Typography.xaml
git commit -m "style: add Typography.xaml with Figma type scale"
```

---

### Task 6: Add Spacing.xaml and Shadows.xaml

**Files:**
- Create: `Gymers/Resources/Styles/Spacing.xaml`
- Create: `Gymers/Resources/Styles/Shadows.xaml`

- [ ] **Step 1: Create `Gymers/Resources/Styles/Spacing.xaml`**

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">

    <!-- 8-pt spacing scale -->
    <x:Double x:Key="Sp1">4</x:Double>
    <x:Double x:Key="Sp2">8</x:Double>
    <x:Double x:Key="Sp3">12</x:Double>
    <x:Double x:Key="Sp4">16</x:Double>
    <x:Double x:Key="Sp6">24</x:Double>
    <x:Double x:Key="Sp8">32</x:Double>
    <x:Double x:Key="Sp12">48</x:Double>

    <!-- Corner radii -->
    <x:Double x:Key="RadiusChip">8</x:Double>
    <x:Double x:Key="RadiusInput">16</x:Double>
    <x:Double x:Key="RadiusCard">24</x:Double>
    <x:Double x:Key="RadiusPill">999</x:Double>

</ResourceDictionary>
```

- [ ] **Step 2: Create `Gymers/Resources/Styles/Shadows.xaml`**

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">

    <!-- Single elevation: 0 10 30 / #1A1C1C @ 6% -->
    <Shadow x:Key="CardShadow"
            Brush="{StaticResource TextPrimary}"
            Offset="0,10"
            Radius="30"
            Opacity="0.06" />

</ResourceDictionary>
```

- [ ] **Step 3: Commit**

```bash
git add Gymers/Resources/Styles/Spacing.xaml Gymers/Resources/Styles/Shadows.xaml
git commit -m "style: add Spacing and Shadows token dictionaries"
```

---

### Task 7: Rewrite Styles.xaml (Card / CardMuted / Pill / Entry base styles) and wire dictionaries in App.xaml

**Files:**
- Modify: `Gymers/Resources/Styles/Styles.xaml`
- Modify: `Gymers/App.xaml`

- [ ] **Step 1: Replace `Gymers/Resources/Styles/Styles.xaml`**

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">

    <!-- App-level Page background -->
    <Style TargetType="ContentPage" x:Key="AppPage">
        <Setter Property="BackgroundColor" Value="{StaticResource BgApp}" />
    </Style>

    <!-- Card: white surface, 24-radius, soft shadow, 32 padding -->
    <Style x:Key="Card" TargetType="Border">
        <Setter Property="BackgroundColor" Value="{StaticResource Surface}" />
        <Setter Property="StrokeThickness" Value="0" />
        <Setter Property="StrokeShape">
            <Setter.Value>
                <RoundRectangle CornerRadius="24" />
            </Setter.Value>
        </Setter>
        <Setter Property="Shadow" Value="{StaticResource CardShadow}" />
        <Setter Property="Padding" Value="32" />
    </Style>

    <!-- CardMuted: surface-muted bg, 24-radius, no shadow, 16 padding -->
    <Style x:Key="CardMuted" TargetType="Border">
        <Setter Property="BackgroundColor" Value="{StaticResource SurfaceMuted}" />
        <Setter Property="StrokeThickness" Value="0" />
        <Setter Property="StrokeShape">
            <Setter.Value>
                <RoundRectangle CornerRadius="24" />
            </Setter.Value>
        </Setter>
        <Setter Property="Padding" Value="16" />
    </Style>

    <!-- Pill: half-height radius, no shadow -->
    <Style x:Key="Pill" TargetType="Border">
        <Setter Property="StrokeThickness" Value="0" />
        <Setter Property="StrokeShape">
            <Setter.Value>
                <RoundRectangle CornerRadius="999" />
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Entry default: removes the platform underline -->
    <Style TargetType="Entry">
        <Setter Property="FontFamily" Value="InterRegular" />
        <Setter Property="FontSize" Value="14" />
        <Setter Property="TextColor" Value="{StaticResource TextPrimary}" />
        <Setter Property="PlaceholderColor" Value="{StaticResource TextMuted}" />
        <Setter Property="BackgroundColor" Value="Transparent" />
    </Style>

</ResourceDictionary>
```

- [ ] **Step 2: Update `Gymers/App.xaml` to load all five dictionaries**

```xml
<?xml version = "1.0" encoding = "UTF-8" ?>
<Application xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:local="clr-namespace:Gymers"
             x:Class="Gymers.App">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Resources/Styles/Colors.xaml" />
                <ResourceDictionary Source="Resources/Styles/Spacing.xaml" />
                <ResourceDictionary Source="Resources/Styles/Shadows.xaml" />
                <ResourceDictionary Source="Resources/Styles/Typography.xaml" />
                <ResourceDictionary Source="Resources/Styles/Styles.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

(Note: order matters — Colors must come before anything that references its keys.)

- [ ] **Step 3: Build**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Gymers/Resources/Styles/Styles.xaml Gymers/App.xaml
git commit -m "style: rewrite Styles.xaml with Card/CardMuted/Pill atoms; wire token dictionaries"
```

---

## Phase 3 — Models & sample data

### Task 8: POCO models

**Files:**
- Create: `Gymers/Models/MembershipTier.cs`
- Create: `Gymers/Models/Member.cs`
- Create: `Gymers/Models/Payment.cs`
- Create: `Gymers/Models/CheckIn.cs`
- Create: `Gymers/Models/ClassSession.cs`

- [ ] **Step 1: Create `Gymers/Models/MembershipTier.cs`**

```csharp
namespace Gymers.Models;

public enum MembershipTier
{
    Basic,
    Premium,
    Elite
}
```

- [ ] **Step 2: Create `Gymers/Models/Member.cs`**

```csharp
namespace Gymers.Models;

public record Member(
    string Id,
    string Name,
    MembershipTier Tier,
    string Status,
    DateOnly Expires);
```

- [ ] **Step 3: Create `Gymers/Models/Payment.cs`**

```csharp
namespace Gymers.Models;

public record Payment(
    int Id,
    string MemberId,
    decimal Amount,
    string Method,
    int ReceiptNumber,
    DateTime At);
```

- [ ] **Step 4: Create `Gymers/Models/CheckIn.cs`**

```csharp
namespace Gymers.Models;

public record CheckIn(
    int Id,
    string MemberId,
    DateTime At);
```

- [ ] **Step 5: Create `Gymers/Models/ClassSession.cs`**

```csharp
namespace Gymers.Models;

public record ClassSession(
    string Id,
    string Title,
    string Location,
    DateTime Start,
    DateTime End);
```

- [ ] **Step 6: Build**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios`
Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add Gymers/Models/
git commit -m "model: add Member, Payment, CheckIn, ClassSession POCOs"
```

---

### Task 9: SampleData seed

**Files:**
- Create: `Gymers/Data/SampleData.cs`

- [ ] **Step 1: Create the file with all hardcoded data from spec §7**

```csharp
using Gymers.Models;

namespace Gymers.Data;

public static class SampleData
{
    public static readonly IReadOnlyList<Member> Members = new[]
    {
        new Member("m1", "Marcus Sterling", MembershipTier.Premium, "Active",         new DateOnly(2026, 12, 15)),
        new Member("m2", "Lena Park",       MembershipTier.Elite,   "Active",         new DateOnly(2027,  3,  4)),
        new Member("m3", "Diego Alvarez",   MembershipTier.Basic,   "Active",         new DateOnly(2026,  6, 22)),
        new Member("m4", "Aisha Khan",      MembershipTier.Premium, "Active",         new DateOnly(2026, 11,  1)),
        new Member("m5", "Sam Chen",        MembershipTier.Basic,   "Expiring Soon",  new DateOnly(2026,  5, 30)),
        new Member("m6", "Priya Nair",      MembershipTier.Elite,   "Active",         new DateOnly(2027,  8, 14)),
    };

    public static readonly IReadOnlyList<Payment> Payments = new[]
    {
        new Payment(1042, "m1", 99.00m,  "Card", 1042, new DateTime(2026, 5, 5,  9, 41, 0)),
        new Payment(1041, "m2", 149.00m, "Card", 1041, new DateTime(2026, 5, 5,  9, 12, 0)),
        new Payment(1040, "m3", 49.00m,  "Cash", 1040, new DateTime(2026, 5, 5,  8, 55, 0)),
        new Payment(1039, "m4", 99.00m,  "Bank", 1039, new DateTime(2026, 5, 4, 18, 22, 0)),
        new Payment(1038, "m5", 49.00m,  "Cash", 1038, new DateTime(2026, 5, 4, 17, 03, 0)),
    };

    public static readonly IReadOnlyList<CheckIn> CheckIns = new[]
    {
        new CheckIn(1, "m1", new DateTime(2026, 5, 5, 9, 42, 0)),
        new CheckIn(2, "m2", new DateTime(2026, 5, 5, 9, 38, 0)),
        new CheckIn(3, "m3", new DateTime(2026, 5, 5, 9, 21, 0)),
        new CheckIn(4, "m4", new DateTime(2026, 5, 5, 9, 15, 0)),
        new CheckIn(5, "m6", new DateTime(2026, 5, 5, 9, 8,  0)),
        new CheckIn(6, "m5", new DateTime(2026, 5, 5, 8, 51, 0)),
    };

    public static readonly IReadOnlyList<ClassSession> TodaysClasses = new[]
    {
        new ClassSession("c1", "High-Intensity Power Blast", "Studio A",
            new DateTime(2026, 5, 5, 10, 30, 0), new DateTime(2026, 5, 5, 11, 30, 0)),
        new ClassSession("c2", "Zen Flow Vinyasa", "Yoga Loft",
            new DateTime(2026, 5, 5, 12, 00, 0), new DateTime(2026, 5, 5, 13, 15, 0)),
        new ClassSession("c3", "Advanced Squat Workshop", "Performance Zone",
            new DateTime(2026, 5, 5, 13, 30, 0), new DateTime(2026, 5, 5, 15, 00, 0)),
    };

    public static Member GetMember(string id) =>
        Members.First(m => m.Id == id);
}
```

- [ ] **Step 2: Build**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Gymers/Data/SampleData.cs
git commit -m "data: add SampleData seed for Members/Payments/CheckIns/Classes"
```

---

## Phase 4 — Icon font integration

### Task 10: Build Icons.cs from Lucide info.json

**Files:**
- Create: `Gymers/Controls/Icons.cs`

- [ ] **Step 1: Extract the nine codepoints from the saved info.json**

```bash
python3 - <<'PY'
import json
data = json.load(open('/tmp/gymers-fonts/lucide-info.json'))
needed = {
    "users":         "Users",
    "calendar":      "Calendar",
    "dollar-sign":   "DollarSign",
    "search":        "Search",
    "bell":          "Bell",
    "chevron-right": "ChevronRight",
    "arrow-up":      "ArrowUp",
    "log-in":        "LogIn",
    "plus":          "Plus",
}
print("// Auto-extracted from lucide-static info.json")
for slug, csname in needed.items():
    info = data.get(slug)
    if info is None:
        raise SystemExit(f"Missing icon: {slug}")
    cp = info["unicode"]
    print(f'    public const string {csname} = "\\u{cp.upper()}";')
PY
```

This prints the nine `public const string ... = "\uXXXX";` lines. Copy them into the next step.

- [ ] **Step 2: Create `Gymers/Controls/Icons.cs`**

Replace the nine `\u????` placeholders below with the values printed in Step 1:

```csharp
namespace Gymers.Controls;

public static class Icons
{
    // Codepoints sourced from lucide-static info.json (paste the printed
    // values from the Step 1 script here, replacing each \uXXXX).
    public const string Users        = "\uXXXX";
    public const string Calendar     = "\uXXXX";
    public const string DollarSign   = "\uXXXX";
    public const string Search       = "\uXXXX";
    public const string Bell         = "\uXXXX";
    public const string ChevronRight = "\uXXXX";
    public const string ArrowUp      = "\uXXXX";
    public const string LogIn        = "\uXXXX";
    public const string Plus         = "\uXXXX";
}
```

- [ ] **Step 3: Build**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Gymers/Controls/Icons.cs
git commit -m "feat: add Icons.cs Lucide glyph constants"
```

---

## Phase 5 — Components

> All components are XAML UserControls under `Gymers/Controls/`. Each lives in two files: `<Name>.xaml` and `<Name>.xaml.cs`. The code-behind declares the bindable properties; the XAML composes the visual.

### Task 11: DeltaChip control

**Files:**
- Create: `Gymers/Controls/DeltaChip.xaml`
- Create: `Gymers/Controls/DeltaChip.xaml.cs`

- [ ] **Step 1: Create `Gymers/Controls/DeltaChip.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:c="clr-namespace:Gymers.Controls"
             x:Class="Gymers.Controls.DeltaChip"
             x:Name="ThisChip">
    <Border x:Name="Pill" Style="{StaticResource Pill}" Padding="8,2"
            HorizontalOptions="Start">
        <HorizontalStackLayout Spacing="2" VerticalOptions="Center">
            <Label x:Name="GlyphLabel"
                   FontFamily="{StaticResource FontLucide}"
                   FontSize="11"
                   VerticalTextAlignment="Center" />
            <Label x:Name="TextLabel"
                   FontFamily="{StaticResource FontInterSemiBold}"
                   FontSize="14"
                   VerticalTextAlignment="Center"
                   BindingContext="{x:Reference ThisChip}"
                   Text="{Binding Text}" />
        </HorizontalStackLayout>
    </Border>
</ContentView>
```

- [ ] **Step 2: Create `Gymers/Controls/DeltaChip.xaml.cs`**

```csharp
using Microsoft.Maui.Graphics;

namespace Gymers.Controls;

public partial class DeltaChip : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(DeltaChip), string.Empty,
            propertyChanged: (b, _, _) => ((DeltaChip)b).ApplyVisual());

    public static readonly BindableProperty DirectionProperty =
        BindableProperty.Create(nameof(Direction), typeof(DeltaDirection), typeof(DeltaChip), DeltaDirection.Up,
            propertyChanged: (b, _, _) => ((DeltaChip)b).ApplyVisual());

    public static readonly BindableProperty OnDarkSurfaceProperty =
        BindableProperty.Create(nameof(OnDarkSurface), typeof(bool), typeof(DeltaChip), false,
            propertyChanged: (b, _, _) => ((DeltaChip)b).ApplyVisual());

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public DeltaDirection Direction
    {
        get => (DeltaDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public bool OnDarkSurface
    {
        get => (bool)GetValue(OnDarkSurfaceProperty);
        set => SetValue(OnDarkSurfaceProperty, value);
    }

    public DeltaChip()
    {
        InitializeComponent();
        ApplyVisual();
    }

    void ApplyVisual()
    {
        var lime      = (Color)Application.Current!.Resources["Lime"];
        var limeSoft  = (Color)Application.Current.Resources["LimeSoft"];
        var olive     = (Color)Application.Current.Resources["Olive"];
        var oliveDark = (Color)Application.Current.Resources["OliveDark"];

        Pill.BackgroundColor = OnDarkSurface ? lime : limeSoft;
        var textColor        = OnDarkSurface ? oliveDark : olive;
        TextLabel.TextColor  = textColor;
        GlyphLabel.TextColor = textColor;
        GlyphLabel.Text      = Direction == DeltaDirection.Up ? Icons.ArrowUp : Icons.ArrowUp; // up only in v1
    }
}

public enum DeltaDirection { Up, Down }
```

- [ ] **Step 3: Build**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Gymers/Controls/DeltaChip.xaml Gymers/Controls/DeltaChip.xaml.cs
git commit -m "feat(controls): add DeltaChip pill"
```

---

### Task 12: PrimaryButton control

**Files:**
- Create: `Gymers/Controls/PrimaryButton.xaml`
- Create: `Gymers/Controls/PrimaryButton.xaml.cs`

- [ ] **Step 1: Create `Gymers/Controls/PrimaryButton.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="Gymers.Controls.PrimaryButton"
             x:Name="ThisButton">
    <Border StrokeThickness="0" HeightRequest="44"
            Background="{StaticResource PrimaryButtonGradientBrush}">
        <Border.StrokeShape>
            <RoundRectangle CornerRadius="8" />
        </Border.StrokeShape>
        <Label Style="{StaticResource ButtonLg}"
               VerticalTextAlignment="Center"
               HorizontalTextAlignment="Center"
               BindingContext="{x:Reference ThisButton}"
               Text="{Binding Text}" />
        <Border.GestureRecognizers>
            <TapGestureRecognizer Tapped="OnTapped" />
        </Border.GestureRecognizers>
    </Border>
</ContentView>
```

- [ ] **Step 2: Create `Gymers/Controls/PrimaryButton.xaml.cs`**

```csharp
using System.Windows.Input;

namespace Gymers.Controls;

public partial class PrimaryButton : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(PrimaryButton), string.Empty);

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(PrimaryButton));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public event EventHandler? Clicked;

    public PrimaryButton() => InitializeComponent();

    void OnTapped(object? sender, TappedEventArgs e)
    {
        if (!IsEnabled) return;
        Clicked?.Invoke(this, EventArgs.Empty);
        if (Command?.CanExecute(null) == true)
            Command.Execute(null);
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Gymers/Controls/PrimaryButton.xaml Gymers/Controls/PrimaryButton.xaml.cs
git commit -m "feat(controls): add PrimaryButton (gradient navy)"
```

---

### Task 13: SecondaryButton control

**Files:**
- Create: `Gymers/Controls/SecondaryButton.xaml`
- Create: `Gymers/Controls/SecondaryButton.xaml.cs`

- [ ] **Step 1: Create `Gymers/Controls/SecondaryButton.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="Gymers.Controls.SecondaryButton"
             x:Name="ThisButton">
    <Label FontFamily="{StaticResource FontInterSemiBold}"
           FontSize="14"
           CharacterSpacing="0.35"
           TextColor="{StaticResource NavyDeep}"
           VerticalTextAlignment="Center"
           BindingContext="{x:Reference ThisButton}"
           Text="{Binding Text}">
        <Label.GestureRecognizers>
            <TapGestureRecognizer Tapped="OnTapped" />
        </Label.GestureRecognizers>
    </Label>
</ContentView>
```

- [ ] **Step 2: Create `Gymers/Controls/SecondaryButton.xaml.cs`**

```csharp
using System.Windows.Input;

namespace Gymers.Controls;

public partial class SecondaryButton : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(SecondaryButton), string.Empty);

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SecondaryButton));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public event EventHandler? Clicked;

    public SecondaryButton() => InitializeComponent();

    void OnTapped(object? sender, TappedEventArgs e)
    {
        Clicked?.Invoke(this, EventArgs.Empty);
        if (Command?.CanExecute(null) == true)
            Command.Execute(null);
    }
}
```

- [ ] **Step 3: Build & commit**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios`
Expected: 0 errors.

```bash
git add Gymers/Controls/SecondaryButton.xaml Gymers/Controls/SecondaryButton.xaml.cs
git commit -m "feat(controls): add SecondaryButton (ghost link)"
```

---

### Task 14: LabeledInput control

**Files:**
- Create: `Gymers/Controls/LabeledInput.xaml`
- Create: `Gymers/Controls/LabeledInput.xaml.cs`

- [ ] **Step 1: Create `Gymers/Controls/LabeledInput.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="Gymers.Controls.LabeledInput"
             x:Name="ThisInput">
    <VerticalStackLayout Spacing="8">
        <Label Style="{StaticResource BodyMd}"
               BindingContext="{x:Reference ThisInput}"
               Text="{Binding Label}" />
        <Border BackgroundColor="{StaticResource SurfaceMuted}"
                StrokeThickness="0"
                HeightRequest="56"
                Padding="16,0">
            <Border.StrokeShape>
                <RoundRectangle CornerRadius="16" />
            </Border.StrokeShape>
            <Entry x:Name="EntryField"
                   VerticalOptions="Center"
                   BindingContext="{x:Reference ThisInput}"
                   Text="{Binding Text, Mode=TwoWay}"
                   Placeholder="{Binding Placeholder}"
                   IsPassword="{Binding IsPassword}"
                   Keyboard="{Binding Keyboard}" />
        </Border>
    </VerticalStackLayout>
</ContentView>
```

- [ ] **Step 2: Create `Gymers/Controls/LabeledInput.xaml.cs`**

```csharp
namespace Gymers.Controls;

public partial class LabeledInput : ContentView
{
    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(LabeledInput), string.Empty);

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(LabeledInput), string.Empty);

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(LabeledInput), string.Empty,
            BindingMode.TwoWay);

    public static readonly BindableProperty IsPasswordProperty =
        BindableProperty.Create(nameof(IsPassword), typeof(bool), typeof(LabeledInput), false);

    public static readonly BindableProperty KeyboardProperty =
        BindableProperty.Create(nameof(Keyboard), typeof(Keyboard), typeof(LabeledInput), Keyboard.Default);

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsPassword
    {
        get => (bool)GetValue(IsPasswordProperty);
        set => SetValue(IsPasswordProperty, value);
    }

    public Keyboard Keyboard
    {
        get => (Keyboard)GetValue(KeyboardProperty);
        set => SetValue(KeyboardProperty, value);
    }

    public LabeledInput() => InitializeComponent();
}
```

- [ ] **Step 3: Build & commit**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios`
Expected: 0 errors.

```bash
git add Gymers/Controls/LabeledInput.xaml Gymers/Controls/LabeledInput.xaml.cs
git commit -m "feat(controls): add LabeledInput field"
```

---

### Task 15: SearchField control

**Files:**
- Create: `Gymers/Controls/SearchField.xaml`
- Create: `Gymers/Controls/SearchField.xaml.cs`

- [ ] **Step 1: Create `Gymers/Controls/SearchField.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:c="clr-namespace:Gymers.Controls"
             x:Class="Gymers.Controls.SearchField"
             x:Name="ThisField">
    <Border BackgroundColor="{StaticResource SurfaceMuted}"
            StrokeThickness="0"
            HeightRequest="56"
            Padding="16,0">
        <Border.StrokeShape>
            <RoundRectangle CornerRadius="16" />
        </Border.StrokeShape>
        <Grid ColumnDefinitions="Auto,*" ColumnSpacing="12" VerticalOptions="Center">
            <Label Grid.Column="0"
                   FontFamily="{StaticResource FontLucide}"
                   FontSize="18"
                   TextColor="{StaticResource TextMuted}"
                   VerticalTextAlignment="Center"
                   Text="{x:Static c:Icons.Search}" />
            <Entry Grid.Column="1"
                   VerticalOptions="Center"
                   BindingContext="{x:Reference ThisField}"
                   Text="{Binding Text, Mode=TwoWay}"
                   Placeholder="{Binding Placeholder}" />
        </Grid>
    </Border>
</ContentView>
```

- [ ] **Step 2: Create `Gymers/Controls/SearchField.xaml.cs`**

```csharp
namespace Gymers.Controls;

public partial class SearchField : ContentView
{
    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(SearchField), string.Empty);

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(SearchField), string.Empty,
            BindingMode.TwoWay);

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public SearchField() => InitializeComponent();
}
```

- [ ] **Step 3: Build & commit**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios`
Expected: 0 errors.

```bash
git add Gymers/Controls/SearchField.xaml Gymers/Controls/SearchField.xaml.cs
git commit -m "feat(controls): add SearchField with leading magnifying-glass glyph"
```

---

### Task 16: TopAppBar control

**Files:**
- Create: `Gymers/Controls/TopAppBar.xaml`
- Create: `Gymers/Controls/TopAppBar.xaml.cs`

- [ ] **Step 1: Create `Gymers/Controls/TopAppBar.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="Gymers.Controls.TopAppBar"
             x:Name="ThisBar">
    <Border BackgroundColor="{StaticResource Surface}"
            StrokeThickness="0"
            Padding="24,16">
        <Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="16" VerticalOptions="Center">
            <!-- Avatar -->
            <Border Grid.Column="0" WidthRequest="40" HeightRequest="40"
                    BackgroundColor="{StaticResource PaleBlue}" StrokeThickness="0"
                    IsVisible="{Binding ShowAvatar, Source={x:Reference ThisBar}}">
                <Border.StrokeShape>
                    <RoundRectangle CornerRadius="999" />
                </Border.StrokeShape>
                <Label Text="A"
                       FontFamily="{StaticResource FontManropeBold}"
                       FontSize="18"
                       TextColor="{StaticResource NavyHeading}"
                       HorizontalTextAlignment="Center"
                       VerticalTextAlignment="Center" />
            </Border>

            <!-- Title -->
            <Label Grid.Column="1"
                   Style="{StaticResource H1Page}"
                   VerticalTextAlignment="Center"
                   BindingContext="{x:Reference ThisBar}"
                   Text="{Binding Title}" />

            <!-- Trailing icon -->
            <Border Grid.Column="2" WidthRequest="40" HeightRequest="40"
                    BackgroundColor="Transparent" StrokeThickness="0">
                <Border.StrokeShape>
                    <RoundRectangle CornerRadius="999" />
                </Border.StrokeShape>
                <Label x:Name="TrailingGlyph"
                       FontFamily="{StaticResource FontLucide}"
                       FontSize="18"
                       TextColor="{StaticResource TextPrimary}"
                       HorizontalTextAlignment="Center"
                       VerticalTextAlignment="Center" />
            </Border>
        </Grid>
    </Border>
</ContentView>
```

- [ ] **Step 2: Create `Gymers/Controls/TopAppBar.xaml.cs`**

```csharp
namespace Gymers.Controls;

public partial class TopAppBar : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(TopAppBar), string.Empty);

    public static readonly BindableProperty ShowAvatarProperty =
        BindableProperty.Create(nameof(ShowAvatar), typeof(bool), typeof(TopAppBar), true);

    public static readonly BindableProperty TrailingIconGlyphProperty =
        BindableProperty.Create(nameof(TrailingIconGlyph), typeof(string), typeof(TopAppBar), string.Empty,
            propertyChanged: (b, _, n) => ((TopAppBar)b).TrailingGlyph.Text = (string)n);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool ShowAvatar
    {
        get => (bool)GetValue(ShowAvatarProperty);
        set => SetValue(ShowAvatarProperty, value);
    }

    public string TrailingIconGlyph
    {
        get => (string)GetValue(TrailingIconGlyphProperty);
        set => SetValue(TrailingIconGlyphProperty, value);
    }

    public TopAppBar() => InitializeComponent();
}
```

- [ ] **Step 3: Build & commit**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios`
Expected: 0 errors.

```bash
git add Gymers/Controls/TopAppBar.xaml Gymers/Controls/TopAppBar.xaml.cs
git commit -m "feat(controls): add TopAppBar with avatar, title, trailing glyph"
```

---

### Task 17: BottomTabBar control

**Files:**
- Create: `Gymers/Controls/BottomTabBar.xaml`
- Create: `Gymers/Controls/BottomTabBar.xaml.cs`

- [ ] **Step 1: Create `Gymers/Controls/BottomTabBar.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:c="clr-namespace:Gymers.Controls"
             x:Class="Gymers.Controls.BottomTabBar"
             x:Name="ThisBar">
    <Border BackgroundColor="{StaticResource Surface}" StrokeThickness="0">
        <Border.StrokeShape>
            <RoundRectangle CornerRadius="24,24,0,0" />
        </Border.StrokeShape>
        <Grid ColumnDefinitions="*,*,*,*" Padding="16,12,16,32">
            <!-- Dashboard -->
            <Border x:Name="DashboardPill" Grid.Column="0" StrokeThickness="0" Padding="16,6">
                <Border.StrokeShape>
                    <RoundRectangle CornerRadius="999" />
                </Border.StrokeShape>
                <VerticalStackLayout Spacing="4" HorizontalOptions="Center">
                    <Label x:Name="DashboardGlyph" FontFamily="{StaticResource FontLucide}"
                           FontSize="18" HorizontalTextAlignment="Center"
                           Text="{x:Static c:Icons.Users}" />
                    <Label x:Name="DashboardLabel" Style="{StaticResource LabelTab}"
                           HorizontalTextAlignment="Center" Text="Dashboard" />
                </VerticalStackLayout>
                <Border.GestureRecognizers>
                    <TapGestureRecognizer Tapped="OnDashboardTapped" />
                </Border.GestureRecognizers>
            </Border>

            <!-- Members -->
            <Border x:Name="MembersPill" Grid.Column="1" StrokeThickness="0" Padding="16,6">
                <Border.StrokeShape>
                    <RoundRectangle CornerRadius="999" />
                </Border.StrokeShape>
                <VerticalStackLayout Spacing="4" HorizontalOptions="Center">
                    <Label x:Name="MembersGlyph" FontFamily="{StaticResource FontLucide}"
                           FontSize="18" HorizontalTextAlignment="Center"
                           Text="{x:Static c:Icons.Users}" />
                    <Label x:Name="MembersLabel" Style="{StaticResource LabelTab}"
                           HorizontalTextAlignment="Center" Text="Members" />
                </VerticalStackLayout>
                <Border.GestureRecognizers>
                    <TapGestureRecognizer Tapped="OnMembersTapped" />
                </Border.GestureRecognizers>
            </Border>

            <!-- Payments -->
            <Border x:Name="PaymentsPill" Grid.Column="2" StrokeThickness="0" Padding="16,6">
                <Border.StrokeShape>
                    <RoundRectangle CornerRadius="999" />
                </Border.StrokeShape>
                <VerticalStackLayout Spacing="4" HorizontalOptions="Center">
                    <Label x:Name="PaymentsGlyph" FontFamily="{StaticResource FontLucide}"
                           FontSize="18" HorizontalTextAlignment="Center"
                           Text="{x:Static c:Icons.DollarSign}" />
                    <Label x:Name="PaymentsLabel" Style="{StaticResource LabelTab}"
                           HorizontalTextAlignment="Center" Text="Payments" />
                </VerticalStackLayout>
                <Border.GestureRecognizers>
                    <TapGestureRecognizer Tapped="OnPaymentsTapped" />
                </Border.GestureRecognizers>
            </Border>

            <!-- Attendance -->
            <Border x:Name="AttendancePill" Grid.Column="3" StrokeThickness="0" Padding="16,6">
                <Border.StrokeShape>
                    <RoundRectangle CornerRadius="999" />
                </Border.StrokeShape>
                <VerticalStackLayout Spacing="4" HorizontalOptions="Center">
                    <Label x:Name="AttendanceGlyph" FontFamily="{StaticResource FontLucide}"
                           FontSize="18" HorizontalTextAlignment="Center"
                           Text="{x:Static c:Icons.Calendar}" />
                    <Label x:Name="AttendanceLabel" Style="{StaticResource LabelTab}"
                           HorizontalTextAlignment="Center" Text="Attendance" />
                </VerticalStackLayout>
                <Border.GestureRecognizers>
                    <TapGestureRecognizer Tapped="OnAttendanceTapped" />
                </Border.GestureRecognizers>
            </Border>
        </Grid>
    </Border>
</ContentView>
```

- [ ] **Step 2: Create `Gymers/Controls/BottomTabBar.xaml.cs`**

```csharp
namespace Gymers.Controls;

public enum AppTab { Dashboard, Members, Payments, Attendance }

public partial class BottomTabBar : ContentView
{
    public static readonly BindableProperty ActiveTabProperty =
        BindableProperty.Create(nameof(ActiveTab), typeof(AppTab), typeof(BottomTabBar), AppTab.Dashboard,
            propertyChanged: (b, _, _) => ((BottomTabBar)b).ApplyActive());

    public AppTab ActiveTab
    {
        get => (AppTab)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    public BottomTabBar()
    {
        InitializeComponent();
        ApplyActive();
    }

    void ApplyActive()
    {
        var pale         = (Color)Application.Current!.Resources["PaleBlue"];
        var navyHeading  = (Color)Application.Current.Resources["NavyHeading"];
        var muted        = (Color)Application.Current.Resources["TextMuted"];

        DashboardPill.BackgroundColor  = ActiveTab == AppTab.Dashboard  ? pale : Colors.Transparent;
        MembersPill.BackgroundColor    = ActiveTab == AppTab.Members    ? pale : Colors.Transparent;
        PaymentsPill.BackgroundColor   = ActiveTab == AppTab.Payments   ? pale : Colors.Transparent;
        AttendancePill.BackgroundColor = ActiveTab == AppTab.Attendance ? pale : Colors.Transparent;

        DashboardGlyph.TextColor  = DashboardLabel.TextColor  = ActiveTab == AppTab.Dashboard  ? navyHeading : muted;
        MembersGlyph.TextColor    = MembersLabel.TextColor    = ActiveTab == AppTab.Members    ? navyHeading : muted;
        PaymentsGlyph.TextColor   = PaymentsLabel.TextColor   = ActiveTab == AppTab.Payments   ? navyHeading : muted;
        AttendanceGlyph.TextColor = AttendanceLabel.TextColor = ActiveTab == AppTab.Attendance ? navyHeading : muted;
    }

    async void OnDashboardTapped(object? sender, TappedEventArgs e)  => await Shell.Current.GoToAsync("//Dashboard");
    async void OnMembersTapped(object? sender, TappedEventArgs e)    => await Shell.Current.GoToAsync("//Members");
    async void OnPaymentsTapped(object? sender, TappedEventArgs e)   => await Shell.Current.GoToAsync("//Payments");
    async void OnAttendanceTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("//Attendance");
}
```

- [ ] **Step 3: Build & commit**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios`
Expected: 0 errors.

```bash
git add Gymers/Controls/BottomTabBar.xaml Gymers/Controls/BottomTabBar.xaml.cs
git commit -m "feat(controls): add BottomTabBar with 4 tab routing"
```

---

### Task 18: KpiCard control

**Files:**
- Create: `Gymers/Controls/KpiCard.xaml`
- Create: `Gymers/Controls/KpiCard.xaml.cs`

- [ ] **Step 1: Create `Gymers/Controls/KpiCard.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:c="clr-namespace:Gymers.Controls"
             x:Class="Gymers.Controls.KpiCard"
             x:Name="ThisCard">
    <Border x:Name="Cardroot" Style="{StaticResource Card}" Padding="32" HeightRequest="180">
        <Grid RowDefinitions="Auto,*,Auto" RowSpacing="16">
            <!-- Header row: label + trailing icon -->
            <Grid Grid.Row="0" ColumnDefinitions="*,Auto">
                <Label x:Name="LabelText" Style="{StaticResource LabelKpi}" Grid.Column="0"
                       BindingContext="{x:Reference ThisCard}"
                       Text="{Binding Label}" />
                <Label x:Name="TrailingIcon" Grid.Column="1"
                       FontFamily="{StaticResource FontLucide}"
                       FontSize="16"
                       VerticalTextAlignment="Center" />
            </Grid>
            <!-- Value -->
            <Label x:Name="ValueText" Style="{StaticResource DisplayKpi}" Grid.Row="1"
                   BindingContext="{x:Reference ThisCard}"
                   Text="{Binding Value}" />
            <!-- Delta + caption -->
            <HorizontalStackLayout Grid.Row="2" Spacing="6" VerticalOptions="End">
                <c:DeltaChip x:Name="Delta"
                             BindingContext="{x:Reference ThisCard}"
                             Text="{Binding DeltaText}"
                             Direction="{Binding DeltaDirection}" />
                <Label x:Name="CaptionText" Style="{StaticResource BodySm}"
                       VerticalTextAlignment="Center"
                       BindingContext="{x:Reference ThisCard}"
                       Text="{Binding Caption}" />
            </HorizontalStackLayout>
        </Grid>
    </Border>
</ContentView>
```

- [ ] **Step 2: Create `Gymers/Controls/KpiCard.xaml.cs`**

```csharp
namespace Gymers.Controls;

public enum KpiVariant { Light, Dark }

public partial class KpiCard : ContentView
{
    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(KpiCard), string.Empty);

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(string), typeof(KpiCard), string.Empty);

    public static readonly BindableProperty DeltaTextProperty =
        BindableProperty.Create(nameof(DeltaText), typeof(string), typeof(KpiCard), string.Empty);

    public static readonly BindableProperty DeltaDirectionProperty =
        BindableProperty.Create(nameof(DeltaDirection), typeof(DeltaDirection), typeof(KpiCard), DeltaDirection.Up);

    public static readonly BindableProperty CaptionProperty =
        BindableProperty.Create(nameof(Caption), typeof(string), typeof(KpiCard), string.Empty);

    public static readonly BindableProperty VariantProperty =
        BindableProperty.Create(nameof(Variant), typeof(KpiVariant), typeof(KpiCard), KpiVariant.Light,
            propertyChanged: (b, _, _) => ((KpiCard)b).ApplyVariant());

    public static readonly BindableProperty TrailingIconGlyphProperty =
        BindableProperty.Create(nameof(TrailingIconGlyph), typeof(string), typeof(KpiCard), string.Empty,
            propertyChanged: (b, _, n) => ((KpiCard)b).TrailingIcon.Text = (string)n);

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
    public string DeltaText
    {
        get => (string)GetValue(DeltaTextProperty);
        set => SetValue(DeltaTextProperty, value);
    }
    public DeltaDirection DeltaDirection
    {
        get => (DeltaDirection)GetValue(DeltaDirectionProperty);
        set => SetValue(DeltaDirectionProperty, value);
    }
    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }
    public KpiVariant Variant
    {
        get => (KpiVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }
    public string TrailingIconGlyph
    {
        get => (string)GetValue(TrailingIconGlyphProperty);
        set => SetValue(TrailingIconGlyphProperty, value);
    }

    public KpiCard()
    {
        InitializeComponent();
        ApplyVariant();
    }

    void ApplyVariant()
    {
        var surface     = (Color)Application.Current!.Resources["Surface"];
        var navyDeep    = (Color)Application.Current.Resources["NavyDeep"];
        var textPrimary = (Color)Application.Current.Resources["TextPrimary"];
        var periwinkle  = (Color)Application.Current.Resources["Periwinkle"];
        var textSec     = (Color)Application.Current.Resources["TextSecondary"];

        bool dark = Variant == KpiVariant.Dark;
        Cardroot.BackgroundColor = dark ? navyDeep : surface;
        ValueText.TextColor      = dark ? Colors.White : textPrimary;
        LabelText.TextColor      = dark ? periwinkle : textSec;
        CaptionText.TextColor    = dark ? periwinkle : textSec;
        TrailingIcon.TextColor   = dark ? periwinkle : textSec;
        Delta.OnDarkSurface      = dark;
    }
}
```

- [ ] **Step 3: Build & commit**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios`
Expected: 0 errors.

```bash
git add Gymers/Controls/KpiCard.xaml Gymers/Controls/KpiCard.xaml.cs
git commit -m "feat(controls): add KpiCard with Light/Dark variants"
```

---

### Task 19: ListRow control

**Files:**
- Create: `Gymers/Controls/ListRow.xaml`
- Create: `Gymers/Controls/ListRow.xaml.cs`

- [ ] **Step 1: Create `Gymers/Controls/ListRow.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:c="clr-namespace:Gymers.Controls"
             x:Class="Gymers.Controls.ListRow"
             x:Name="ThisRow">
    <Border BackgroundColor="{StaticResource SurfaceMuted}"
            StrokeThickness="0" Padding="16">
        <Border.StrokeShape>
            <RoundRectangle CornerRadius="24" />
        </Border.StrokeShape>
        <Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="16" VerticalOptions="Center">
            <!-- Leading slot -->
            <ContentPresenter Grid.Column="0"
                              VerticalOptions="Center"
                              BindingContext="{x:Reference ThisRow}"
                              Content="{Binding LeadingContent}" />
            <!-- Title + Subtitle -->
            <VerticalStackLayout Grid.Column="1" Spacing="2" VerticalOptions="Center">
                <Label Style="{StaticResource H4Item}"
                       BindingContext="{x:Reference ThisRow}"
                       Text="{Binding Title}" />
                <Label Style="{StaticResource Caption}"
                       BindingContext="{x:Reference ThisRow}"
                       Text="{Binding Subtitle}" />
            </VerticalStackLayout>
            <!-- Trailing chevron -->
            <Border Grid.Column="2" WidthRequest="32" HeightRequest="32"
                    BackgroundColor="{StaticResource Lime}" StrokeThickness="0"
                    BindingContext="{x:Reference ThisRow}"
                    IsVisible="{Binding TrailingChevron}">
                <Border.StrokeShape>
                    <RoundRectangle CornerRadius="999" />
                </Border.StrokeShape>
                <Label FontFamily="{StaticResource FontLucide}"
                       FontSize="14"
                       TextColor="{StaticResource OliveDark}"
                       HorizontalTextAlignment="Center"
                       VerticalTextAlignment="Center"
                       Text="{x:Static c:Icons.ChevronRight}" />
            </Border>
        </Grid>
    </Border>
</ContentView>
```

- [ ] **Step 2: Create `Gymers/Controls/ListRow.xaml.cs`**

```csharp
namespace Gymers.Controls;

public partial class ListRow : ContentView
{
    public static readonly BindableProperty LeadingContentProperty =
        BindableProperty.Create(nameof(LeadingContent), typeof(View), typeof(ListRow));

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(ListRow), string.Empty);

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(ListRow), string.Empty);

    public static readonly BindableProperty TrailingChevronProperty =
        BindableProperty.Create(nameof(TrailingChevron), typeof(bool), typeof(ListRow), false);

    public View? LeadingContent
    {
        get => (View?)GetValue(LeadingContentProperty);
        set => SetValue(LeadingContentProperty, value);
    }
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }
    public bool TrailingChevron
    {
        get => (bool)GetValue(TrailingChevronProperty);
        set => SetValue(TrailingChevronProperty, value);
    }

    public ListRow() => InitializeComponent();
}
```

- [ ] **Step 3: Build & commit**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios`
Expected: 0 errors.

```bash
git add Gymers/Controls/ListRow.xaml Gymers/Controls/ListRow.xaml.cs
git commit -m "feat(controls): add ListRow with leading slot + lime chevron"
```

---

## Phase 6 — Shell + pages

### Task 20: AppShell rewrite (Login route + 4-tab TabBar)

**Files:**
- Modify: `Gymers/AppShell.xaml`
- Modify: `Gymers/AppShell.xaml.cs`
- Delete: `Gymers/MainPage.xaml`
- Delete: `Gymers/MainPage.xaml.cs`

- [ ] **Step 1: Replace `Gymers/AppShell.xaml`**

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Shell
    x:Class="Gymers.AppShell"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:pages="clr-namespace:Gymers.Pages"
    Shell.NavBarIsVisible="False"
    Shell.TabBarIsVisible="False"
    FlyoutBehavior="Disabled"
    Title="Gymers">

    <ShellContent Title="Login"
                  Route="Login"
                  ContentTemplate="{DataTemplate pages:LoginPage}" />

    <TabBar>
        <ShellContent Route="Dashboard"  ContentTemplate="{DataTemplate pages:DashboardPage}" />
        <ShellContent Route="Members"    ContentTemplate="{DataTemplate pages:MembersPage}" />
        <ShellContent Route="Payments"   ContentTemplate="{DataTemplate pages:PaymentsPage}" />
        <ShellContent Route="Attendance" ContentTemplate="{DataTemplate pages:AttendancePage}" />
    </TabBar>

</Shell>
```

- [ ] **Step 2: Verify `Gymers/AppShell.xaml.cs` is the default no-op (no edit needed unless it has stale code)**

Run: `cat Gymers/AppShell.xaml.cs`

Expected: contains only the partial class with `InitializeComponent()`. If it has anything else, replace with:

```csharp
namespace Gymers;

public partial class AppShell : Shell
{
    public AppShell() => InitializeComponent();
}
```

- [ ] **Step 3: Delete the old MainPage**

```bash
rm Gymers/MainPage.xaml Gymers/MainPage.xaml.cs
```

(After this step the build will fail because the page files don't exist yet — that's expected; Task 21–25 add them.)

- [ ] **Step 4: Stage the changes (don't commit yet — pages required first)**

```bash
git add Gymers/AppShell.xaml Gymers/AppShell.xaml.cs Gymers/MainPage.xaml Gymers/MainPage.xaml.cs
```

(The next pass of `git add` after the pages exist will commit Shell + all five pages together.)

---

### Task 21: LoginPage

**Files:**
- Create: `Gymers/Pages/LoginPage.xaml`
- Create: `Gymers/Pages/LoginPage.xaml.cs`

- [ ] **Step 1: Create `Gymers/Pages/LoginPage.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:c="clr-namespace:Gymers.Controls"
             x:Class="Gymers.Pages.LoginPage"
             BackgroundColor="{StaticResource BgApp}">

    <ScrollView>
        <VerticalStackLayout Padding="24,80,24,40" Spacing="24">

            <!-- Wordmark -->
            <Label Text="GYMERS"
                   FontFamily="{StaticResource FontManropeExtraBold}"
                   FontSize="30"
                   CharacterSpacing="2"
                   TextColor="{StaticResource NavyHeading}"
                   HorizontalTextAlignment="Center" />

            <!-- Welcome -->
            <VerticalStackLayout Spacing="4">
                <Label Style="{StaticResource H1Page}" Text="Welcome back" />
                <Label Style="{StaticResource BodyMd}" Text="Sign in to manage your gym" />
            </VerticalStackLayout>

            <!-- Card with form -->
            <Border Style="{StaticResource Card}">
                <VerticalStackLayout Spacing="16">
                    <c:LabeledInput Label="Username" Placeholder="admin" />
                    <c:LabeledInput Label="Password" Placeholder="••••••••" IsPassword="True" />

                    <!-- Admin/Staff segment -->
                    <HorizontalStackLayout Spacing="8" HorizontalOptions="Center">
                        <Border x:Name="AdminPill" StrokeThickness="0" Padding="20,8"
                                BackgroundColor="{StaticResource NavyDeep}">
                            <Border.StrokeShape>
                                <RoundRectangle CornerRadius="999" />
                            </Border.StrokeShape>
                            <Label x:Name="AdminLabel" Text="Admin"
                                   FontFamily="{StaticResource FontInterSemiBold}"
                                   FontSize="14"
                                   TextColor="White" />
                            <Border.GestureRecognizers>
                                <TapGestureRecognizer Tapped="OnSelectAdmin" />
                            </Border.GestureRecognizers>
                        </Border>
                        <Border x:Name="StaffPill" StrokeThickness="0" Padding="20,8"
                                BackgroundColor="Transparent">
                            <Border.StrokeShape>
                                <RoundRectangle CornerRadius="999" />
                            </Border.StrokeShape>
                            <Label x:Name="StaffLabel" Text="Staff"
                                   FontFamily="{StaticResource FontInterSemiBold}"
                                   FontSize="14"
                                   TextColor="{StaticResource TextSecondary}" />
                            <Border.GestureRecognizers>
                                <TapGestureRecognizer Tapped="OnSelectStaff" />
                            </Border.GestureRecognizers>
                        </Border>
                    </HorizontalStackLayout>

                    <c:PrimaryButton Text="SIGN IN" Clicked="OnSignIn" />
                </VerticalStackLayout>
            </Border>

            <Label Style="{StaticResource BodySm}"
                   TextColor="{StaticResource TextMuted}"
                   HorizontalTextAlignment="Center"
                   Text="Demo: any username/password works" />
        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

- [ ] **Step 2: Create `Gymers/Pages/LoginPage.xaml.cs`**

```csharp
namespace Gymers.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage() => InitializeComponent();

    void OnSelectAdmin(object? sender, TappedEventArgs e)
    {
        var navy = (Color)Application.Current!.Resources["NavyDeep"];
        var sec  = (Color)Application.Current.Resources["TextSecondary"];
        AdminPill.BackgroundColor = navy;
        StaffPill.BackgroundColor = Colors.Transparent;
        AdminLabel.TextColor = Colors.White;
        StaffLabel.TextColor = sec;
    }

    void OnSelectStaff(object? sender, TappedEventArgs e)
    {
        var navy = (Color)Application.Current!.Resources["NavyDeep"];
        var sec  = (Color)Application.Current.Resources["TextSecondary"];
        AdminPill.BackgroundColor = Colors.Transparent;
        StaffPill.BackgroundColor = navy;
        AdminLabel.TextColor = sec;
        StaffLabel.TextColor = Colors.White;
    }

    async void OnSignIn(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Dashboard");
    }
}
```

- [ ] **Step 3: Build (will succeed once Task 22-25 add the other pages)**

For now, skip building until all five pages exist.

---

### Task 22: DashboardPage

**Files:**
- Create: `Gymers/Pages/DashboardPage.xaml`
- Create: `Gymers/Pages/DashboardPage.xaml.cs`

- [ ] **Step 1: Create `Gymers/Pages/DashboardPage.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:c="clr-namespace:Gymers.Controls"
             x:Class="Gymers.Pages.DashboardPage"
             BackgroundColor="{StaticResource BgApp}"
             Shell.NavBarIsVisible="False">

    <Grid RowDefinitions="Auto,*,Auto">

        <c:TopAppBar Grid.Row="0" Title="Dashboard"
                     TrailingIconGlyph="{x:Static c:Icons.Bell}" />

        <ScrollView Grid.Row="1" Padding="24,16,24,16">
            <VerticalStackLayout Spacing="16">

                <c:KpiCard Variant="Light"
                           Label="Total Members" Value="1,250"
                           DeltaText="+5%" DeltaDirection="Up"
                           Caption="this month"
                           TrailingIconGlyph="{x:Static c:Icons.Users}" />

                <c:KpiCard Variant="Light"
                           Label="Today's Attendance" Value="350"
                           DeltaText="+12%" DeltaDirection="Up"
                           Caption="vs yesterday"
                           TrailingIconGlyph="{x:Static c:Icons.Calendar}" />

                <c:KpiCard Variant="Dark"
                           Label="Monthly Earnings" Value="$45,000"
                           DeltaText="+8%" DeltaDirection="Up"
                           Caption="projected growth"
                           TrailingIconGlyph="{x:Static c:Icons.DollarSign}" />

                <!-- Live Capacity card -->
                <Border Style="{StaticResource Card}">
                    <VerticalStackLayout Spacing="32">
                        <Grid ColumnDefinitions="*,Auto">
                            <VerticalStackLayout Grid.Column="0" Spacing="4">
                                <Label Style="{StaticResource H3Card}" Text="Live Capacity" />
                                <Label Style="{StaticResource BodyMd}"
                                       Text="Real-time gym floor occupancy" />
                            </VerticalStackLayout>
                            <VerticalStackLayout Grid.Column="1" Spacing="0" HorizontalOptions="End">
                                <Label FontFamily="{StaticResource FontInterSemiBold}"
                                       FontSize="30"
                                       TextColor="{StaticResource NavyDeep}"
                                       HorizontalTextAlignment="End"
                                       Text="78%" />
                                <Label Style="{StaticResource LabelZone}"
                                       TextColor="{StaticResource Olive}"
                                       HorizontalTextAlignment="End"
                                       Text="Peak Hour" />
                            </VerticalStackLayout>
                        </Grid>

                        <!-- Progress bar -->
                        <Border BackgroundColor="{StaticResource SurfaceMuted}" StrokeThickness="0"
                                HeightRequest="32">
                            <Border.StrokeShape>
                                <RoundRectangle CornerRadius="999" />
                            </Border.StrokeShape>
                            <Border Background="{StaticResource LiveCapacityGradientBrush}"
                                    StrokeThickness="0"
                                    HorizontalOptions="Start"
                                    WidthRequest="240" HeightRequest="32">
                                <Border.StrokeShape>
                                    <RoundRectangle CornerRadius="999" />
                                </Border.StrokeShape>
                            </Border>
                        </Border>

                        <!-- Zone grid -->
                        <Grid ColumnDefinitions="*,*" RowDefinitions="Auto,Auto"
                              ColumnSpacing="16" RowSpacing="16">
                            <VerticalStackLayout Grid.Row="0" Grid.Column="0">
                                <Label Style="{StaticResource LabelZone}" Text="Cardio Zone" />
                                <Label Style="{StaticResource StatLg}" Text="92%" />
                            </VerticalStackLayout>
                            <VerticalStackLayout Grid.Row="0" Grid.Column="1">
                                <Label Style="{StaticResource LabelZone}" Text="Weight Room" />
                                <Label Style="{StaticResource StatLg}" Text="65%" />
                            </VerticalStackLayout>
                            <VerticalStackLayout Grid.Row="1" Grid.Column="0">
                                <Label Style="{StaticResource LabelZone}" Text="Yoga Studio" />
                                <Label Style="{StaticResource StatLg}" Text="40%" />
                            </VerticalStackLayout>
                            <VerticalStackLayout Grid.Row="1" Grid.Column="1">
                                <Label Style="{StaticResource LabelZone}" Text="Pool Area" />
                                <Label Style="{StaticResource StatLg}" Text="15%" />
                            </VerticalStackLayout>
                        </Grid>
                    </VerticalStackLayout>
                </Border>

                <!-- Coach Spotlight -->
                <Border Style="{StaticResource Card}">
                    <VerticalStackLayout Spacing="24">
                        <Border WidthRequest="80" HeightRequest="80"
                                BackgroundColor="{StaticResource PaleBlue}"
                                StrokeThickness="0" HorizontalOptions="Start">
                            <Border.StrokeShape>
                                <RoundRectangle CornerRadius="24" />
                            </Border.StrokeShape>
                            <Label Text="MS"
                                   FontFamily="{StaticResource FontManropeBold}"
                                   FontSize="28"
                                   TextColor="{StaticResource NavyHeading}"
                                   HorizontalTextAlignment="Center"
                                   VerticalTextAlignment="Center" />
                        </Border>

                        <VerticalStackLayout Spacing="0">
                            <Label Style="{StaticResource H3Card}" Text="Marcus Sterling" />
                            <Label FontFamily="{StaticResource FontInterSemiBold}"
                                   FontSize="14"
                                   TextColor="{StaticResource NavyDeep}"
                                   Text="Lead Performance Coach" />
                        </VerticalStackLayout>

                        <VerticalStackLayout Spacing="16">
                            <Grid ColumnDefinitions="*,Auto">
                                <Label Grid.Column="0" Style="{StaticResource BodyMd}" Text="Client Rating" />
                                <Label Grid.Column="1"
                                       FontFamily="{StaticResource FontInterSemiBold}"
                                       FontSize="14"
                                       TextColor="{StaticResource TextPrimary}"
                                       Text="4.9/5.0" />
                            </Grid>
                            <Grid ColumnDefinitions="*,Auto">
                                <Label Grid.Column="0" Style="{StaticResource BodyMd}" Text="Sessions Completed" />
                                <Label Grid.Column="1"
                                       FontFamily="{StaticResource FontInterSemiBold}"
                                       FontSize="14"
                                       TextColor="{StaticResource TextPrimary}"
                                       Text="142" />
                            </Grid>
                        </VerticalStackLayout>

                        <c:PrimaryButton Text="VIEW PERFORMANCE PROFILE" />
                    </VerticalStackLayout>
                </Border>

                <!-- Today's Classes -->
                <Border Style="{StaticResource Card}">
                    <VerticalStackLayout Spacing="32">
                        <Grid ColumnDefinitions="*,Auto" VerticalOptions="Center">
                            <VerticalStackLayout Grid.Column="0" Spacing="4">
                                <Label Style="{StaticResource H2Section}" Text="Today's Classes" />
                                <Label Style="{StaticResource BodyMd}"
                                       Text="Schedule for the next 4 hours" />
                            </VerticalStackLayout>
                            <c:SecondaryButton Grid.Column="1" Text="View Schedule"
                                               VerticalOptions="Center" />
                        </Grid>

                        <VerticalStackLayout x:Name="ClassList" Spacing="16" />
                    </VerticalStackLayout>
                </Border>

            </VerticalStackLayout>
        </ScrollView>

        <c:BottomTabBar Grid.Row="2" ActiveTab="Dashboard" />
    </Grid>
</ContentPage>
```

- [ ] **Step 2: Create `Gymers/Pages/DashboardPage.xaml.cs`**

```csharp
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;

namespace Gymers.Pages;

public partial class DashboardPage : ContentPage
{
    public DashboardPage()
    {
        InitializeComponent();
        BuildClassList();
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

- [ ] **Step 3: Continue to next task (build will succeed at end of Task 25)**

---

### Task 23: MembersPage

**Files:**
- Create: `Gymers/Pages/MembersPage.xaml`
- Create: `Gymers/Pages/MembersPage.xaml.cs`

- [ ] **Step 1: Create `Gymers/Pages/MembersPage.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:c="clr-namespace:Gymers.Controls"
             x:Class="Gymers.Pages.MembersPage"
             BackgroundColor="{StaticResource BgApp}"
             Shell.NavBarIsVisible="False">

    <Grid RowDefinitions="Auto,*,Auto">

        <c:TopAppBar Grid.Row="0" Title="Members"
                     TrailingIconGlyph="{x:Static c:Icons.Plus}" />

        <ScrollView Grid.Row="1" Padding="24,16">
            <VerticalStackLayout Spacing="16">
                <c:SearchField Placeholder="Search by name…" />

                <c:KpiCard Variant="Light"
                           Label="Active Members" Value="1,250"
                           DeltaText="+5%" DeltaDirection="Up"
                           Caption="this month"
                           TrailingIconGlyph="{x:Static c:Icons.Users}" />

                <Label Style="{StaticResource H2Section}" Text="All Members" />

                <VerticalStackLayout x:Name="MemberList" Spacing="12" />
            </VerticalStackLayout>
        </ScrollView>

        <c:BottomTabBar Grid.Row="2" ActiveTab="Members" />
    </Grid>
</ContentPage>
```

- [ ] **Step 2: Create `Gymers/Pages/MembersPage.xaml.cs`**

```csharp
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;

namespace Gymers.Pages;

public partial class MembersPage : ContentPage
{
    public MembersPage()
    {
        InitializeComponent();
        BuildMemberList();
    }

    void BuildMemberList()
    {
        foreach (var m in SampleData.Members)
        {
            MemberList.Children.Add(new ListRow
            {
                LeadingContent = MakeInitialAvatar(m.Name),
                Title          = m.Name,
                Subtitle       = $"{m.Tier} · {m.Status} · Expires {m.Expires:MM/dd/yyyy}"
            });
        }
    }

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

- [ ] **Step 3: Continue to next task**

---

### Task 24: PaymentsPage

**Files:**
- Create: `Gymers/Pages/PaymentsPage.xaml`
- Create: `Gymers/Pages/PaymentsPage.xaml.cs`

- [ ] **Step 1: Create `Gymers/Pages/PaymentsPage.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:c="clr-namespace:Gymers.Controls"
             x:Class="Gymers.Pages.PaymentsPage"
             BackgroundColor="{StaticResource BgApp}"
             Shell.NavBarIsVisible="False">

    <Grid RowDefinitions="Auto,*,Auto">
        <c:TopAppBar Grid.Row="0" Title="Payments"
                     TrailingIconGlyph="{x:Static c:Icons.Plus}" />

        <ScrollView Grid.Row="1" Padding="24,16">
            <VerticalStackLayout Spacing="16">

                <c:KpiCard Variant="Dark"
                           Label="Today's Earnings" Value="$1,250"
                           DeltaText="+8%" DeltaDirection="Up"
                           Caption="vs yesterday"
                           TrailingIconGlyph="{x:Static c:Icons.DollarSign}" />

                <Border Style="{StaticResource Card}">
                    <VerticalStackLayout Spacing="16">
                        <Label Style="{StaticResource H3Card}" Text="Record Payment" />
                        <c:LabeledInput Label="Member"  Placeholder="Member name" />
                        <c:LabeledInput Label="Amount"  Placeholder="0.00" Keyboard="Numeric" />
                        <c:LabeledInput Label="Method"  Placeholder="Card / Cash / Bank" />
                        <c:PrimaryButton Text="RECORD PAYMENT" />
                    </VerticalStackLayout>
                </Border>

                <Label Style="{StaticResource H2Section}" Text="Recent Payments" />

                <VerticalStackLayout x:Name="PaymentList" Spacing="12" />
            </VerticalStackLayout>
        </ScrollView>

        <c:BottomTabBar Grid.Row="2" ActiveTab="Payments" />
    </Grid>
</ContentPage>
```

- [ ] **Step 2: Create `Gymers/Pages/PaymentsPage.xaml.cs`**

```csharp
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;

namespace Gymers.Pages;

public partial class PaymentsPage : ContentPage
{
    public PaymentsPage()
    {
        InitializeComponent();
        BuildPaymentList();
    }

    void BuildPaymentList()
    {
        foreach (var p in SampleData.Payments)
        {
            var member = SampleData.GetMember(p.MemberId);
            PaymentList.Children.Add(new ListRow
            {
                LeadingContent = MakeAmountPill(p.Amount),
                Title          = member.Name,
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
}
```

- [ ] **Step 3: Continue to next task**

---

### Task 25: AttendancePage + final build & commit

**Files:**
- Create: `Gymers/Pages/AttendancePage.xaml`
- Create: `Gymers/Pages/AttendancePage.xaml.cs`

- [ ] **Step 1: Create `Gymers/Pages/AttendancePage.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:c="clr-namespace:Gymers.Controls"
             x:Class="Gymers.Pages.AttendancePage"
             BackgroundColor="{StaticResource BgApp}"
             Shell.NavBarIsVisible="False">

    <Grid RowDefinitions="Auto,*,Auto">
        <c:TopAppBar Grid.Row="0" Title="Attendance"
                     TrailingIconGlyph="{x:Static c:Icons.Calendar}" />

        <ScrollView Grid.Row="1" Padding="24,16">
            <VerticalStackLayout Spacing="16">

                <c:KpiCard Variant="Light"
                           Label="Today's Check-Ins" Value="350"
                           DeltaText="+12%" DeltaDirection="Up"
                           Caption="vs yesterday"
                           TrailingIconGlyph="{x:Static c:Icons.Calendar}" />

                <Border Style="{StaticResource Card}">
                    <VerticalStackLayout Spacing="16">
                        <Label Style="{StaticResource H3Card}" Text="Check In" />
                        <c:SearchField Placeholder="Search member by name…" />
                        <c:PrimaryButton Text="CHECK IN" />
                    </VerticalStackLayout>
                </Border>

                <Label Style="{StaticResource H2Section}" Text="Recent Check-ins" />

                <VerticalStackLayout x:Name="CheckInList" Spacing="12" />
            </VerticalStackLayout>
        </ScrollView>

        <c:BottomTabBar Grid.Row="2" ActiveTab="Attendance" />
    </Grid>
</ContentPage>
```

- [ ] **Step 2: Create `Gymers/Pages/AttendancePage.xaml.cs`**

```csharp
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;

namespace Gymers.Pages;

public partial class AttendancePage : ContentPage
{
    public AttendancePage()
    {
        InitializeComponent();
        BuildCheckInList();
    }

    void BuildCheckInList()
    {
        foreach (var c in SampleData.CheckIns)
        {
            var member = SampleData.GetMember(c.MemberId);
            CheckInList.Children.Add(new ListRow
            {
                LeadingContent = MakeStatusDot(),
                Title          = member.Name,
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
}
```

- [ ] **Step 3: Build the whole app**

Run: `dotnet build Gymers/Gymers.csproj -f net10.0-ios`
Expected: Build succeeded. 0 errors. (Warnings about unused `using` are OK; remove if any.)

- [ ] **Step 4: Commit Shell + all five pages together**

```bash
git add Gymers/AppShell.xaml Gymers/AppShell.xaml.cs Gymers/Pages/ Gymers/MainPage.xaml Gymers/MainPage.xaml.cs
git commit -m "feat: add AppShell routing and all five page implementations (Login, Dashboard, Members, Payments, Attendance)"
```

(`MainPage.xaml` and `.cs` are tracked as deletions — `git add` records their removal.)

---

## Phase 7 — Simulator verification

### Task 26: Run on iOS 26.2 simulator and verify against Figma

**Files:**
- No file changes — verification only.

- [ ] **Step 1: List available iOS simulators**

```bash
xcrun simctl list devices available | grep -i "iPhone\|iOS"
```

Find an iPhone running iOS 26.2 (e.g. `iPhone 15 Pro (XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX) (Shutdown)`).

- [ ] **Step 2: Boot the simulator**

```bash
xcrun simctl boot "iPhone 15 Pro"
open -a Simulator
```

(Substitute the real device name from Step 1 if it differs.)

- [ ] **Step 3: Build & launch the app on the simulator**

```bash
dotnet build Gymers/Gymers.csproj -t:Run -f net10.0-ios
```

(.NET MAUI auto-selects the booted simulator. If it picks the wrong one, pass `-p:_DeviceName=:v2:udid=<udid>` with the UDID printed in Step 1.)

Expected: app installs and launches; `LoginPage` shows.

- [ ] **Step 4: Walk the demo flow**

1. Tap **SIGN IN** → `DashboardPage` appears.
2. From Dashboard, tap **Members** tab → `MembersPage`.
3. From Members, tap **Payments** → `PaymentsPage`.
4. From Payments, tap **Attendance** → `AttendancePage`.
5. From Attendance, tap **Dashboard** → back to `DashboardPage`.

Each switch must update the active-tab pill (`PaleBlue` bg, `NavyHeading` glyph + label).

- [ ] **Step 5: Capture five screenshots**

```bash
mkdir -p docs/status/screenshots
xcrun simctl io booted screenshot docs/status/screenshots/01-login.png
# … navigate to each screen, run the same command with 02-…05- filenames.
```

- [ ] **Step 6: Compare each screenshot to its Figma frame**

Use macOS Digital Color Meter to spot-check primary colors:
- `NavyDeep` should be `#002159` ± a couple of digits (ΔE < 5 in Lab space).
- `Lime` should be `#C7F339`.
- Card background `#FFFFFF` against page `#F9F9F9`.

Verify Manrope (headings) and Inter (body) render correctly — letters should look like Manrope/Inter, not Helvetica/SF Pro fallback.

- [ ] **Step 7: Commit screenshots**

```bash
git add docs/status/screenshots/
git commit -m "docs: add iOS 26.2 simulator screenshots of all five screens"
```

- [ ] **Step 8: Verify the success criteria from spec §9**

Checklist (all must pass):
- [ ] `dotnet build … -f net10.0-ios` is clean.
- [ ] App launches.
- [ ] All 5 screens render without glitches.
- [ ] Login → Dashboard navigation works.
- [ ] Each tab swap works in any order.
- [ ] Sample data matches spec §7.
- [ ] No crashes after walking through every tab combination.

---

## Done

After Task 26, v1 is complete. The app:
- Builds clean for `net10.0-ios`.
- Runs on iOS 26.2 simulator.
- Shows all 5 admin/staff screens with hardcoded sample data.
- Visually matches the Figma design tokens (palette, type scale, spacing, radii, shadows).

Out-of-scope items (SQLite, sign-out, validation, PDF receipts, reports export, Android testing, Mac Catalyst) remain on the roadmap; spec §2 covers them.
