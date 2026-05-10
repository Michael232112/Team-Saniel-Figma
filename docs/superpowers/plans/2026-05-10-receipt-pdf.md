# Gymers Mobile App — Receipt PDF Generation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tap any row in Recent Payments → generate a one-page PDF receipt → open the system share sheet for save / email / print. Re-issuing past receipts is deterministic from SQLite.

**Architecture:** Add a `ReceiptService` DI singleton that builds a `ReceiptDocument` (a `QuestPDF.Infrastructure.IDocument`) and writes it to `FileSystem.CacheDirectory/receipts/gymers-receipt-{ReceiptNumber}.pdf`, then hands the file to `Microsoft.Maui.ApplicationModel.DataTransfer.Share`. Per-row tap is enabled by extending the shared `ListRow` ContentView with a `Tapped` event + `CommandParameter` BindableProperty (other consumers ignore both).

**Tech Stack:** .NET 10, MAUI, C# 12, XAML. Add `QuestPDF` 2026.5.0. Existing project. iOS 26.2 sim + Mac Catalyst targets.

**Spec:** `docs/superpowers/specs/2026-05-10-receipt-pdf-design.md` (commit `9bb831c`).

---

## Files Touched

| File                                   | Action |
| -------------------------------------- | ------ |
| `Gymers/Gymers.csproj`                 | Modify (add `QuestPDF` PackageReference) |
| `Gymers/MauiProgram.cs`                | Modify (set Community license, register `ReceiptService`) |
| `Gymers/Controls/ListRow.xaml`         | Modify (inner `TapGestureRecognizer`) |
| `Gymers/Controls/ListRow.xaml.cs`      | Modify (`Tapped` event + `CommandParameter` BindableProperty) |
| `Gymers/Services/ReceiptDocument.cs`   | Create |
| `Gymers/Services/ReceiptService.cs`    | Create |
| `Gymers/Pages/PaymentsPage.xaml.cs`    | Modify (per-row tap → receipt → share sheet, flip `TrailingChevron=true`) |
| `docs/status/build_status_docx.py`     | Modify (move bullet to completed row) |
| `docs/status/gymers-mobile-app-status-update.html` | Modify (mirror status doc change) |

Nine files. No XAML beyond the `ListRow` `TapGestureRecognizer` line. No model or DB changes.

---

## Run helper (referenced by every task)

When a task says "build and run," do this from the repo root.

**Build (Mac Catalyst, primary verification target):**
```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
```

**Build (iOS, must also stay green):**
```bash
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

**Quit any running instance and relaunch:**
```bash
pkill -f "Gymers/bin/Debug/net10.0-maccatalyst" 2>/dev/null; sleep 1
open Gymers/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Gymers.app
```

**Find the SQLite file (created by the predecessor SQLite slice):**
```bash
find ~/Library/Containers -name "gymers.db3" 2>/dev/null
```

**Find the receipts cache directory (after first PDF generation):**
```bash
find ~/Library/Containers -path "*/receipts/gymers-receipt-*.pdf" 2>/dev/null
```

**Wipe the receipts cache (forces fresh generation):**
```bash
find ~/Library/Containers -path "*/receipts/gymers-receipt-*.pdf" -delete 2>/dev/null
```

**Why Mac Catalyst, not iOS sim:** the iOS simulator is unusable on this hardware (sustained UI lag). Mac Catalyst runs the same MAUI code paths natively. iOS-target builds must still succeed (it's the primary deploy target), but verification happens on Mac Catalyst.

---

## Task 1: Foundation — NuGet package and license

After this task the project compiles with `QuestPDF` 2026.5.0 available, the Community license is set at startup, and a placeholder DI registration for `ReceiptService` is wired in. The service class itself is added later — for now this task only proves the package restores cleanly. No runtime behavior change yet.

**Files:**
- Modify: `Gymers/Gymers.csproj`
- Modify: `Gymers/MauiProgram.cs` (license only — service registration lands in Task 4 once the class exists)

- [ ] **Step 1: Add the `QuestPDF` PackageReference**

In `Gymers/Gymers.csproj`, find the `<ItemGroup>` containing the existing `<PackageReference>` lines:

```xml
<ItemGroup>
    <PackageReference Include="Microsoft.Maui.Controls" Version="$(MauiVersion)" />
    <PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.0" />
    <PackageReference Include="sqlite-net-pcl" Version="1.9.172" />
</ItemGroup>
```

Add a new line at the bottom of the `<ItemGroup>`:

```xml
<PackageReference Include="QuestPDF" Version="2026.5.0" />
```

The full `<ItemGroup>` becomes:

```xml
<ItemGroup>
    <PackageReference Include="Microsoft.Maui.Controls" Version="$(MauiVersion)" />
    <PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.0" />
    <PackageReference Include="sqlite-net-pcl" Version="1.9.172" />
    <PackageReference Include="QuestPDF" Version="2026.5.0" />
</ItemGroup>
```

- [ ] **Step 2: Set the QuestPDF Community license in `MauiProgram.cs`**

Open `Gymers/MauiProgram.cs`. Add a new `using` directive at the top, just below the existing two:

```csharp
using QuestPDF.Infrastructure;
```

Inside `CreateMauiApp()`, immediately after `var builder = MauiApp.CreateBuilder();` and before the `builder.UseMauiApp<App>()` chain, add:

```csharp
QuestPDF.Settings.License = LicenseType.Community;
```

The top of `CreateMauiApp` becomes:

```csharp
public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();

    QuestPDF.Settings.License = LicenseType.Community;

    builder
        .UseMauiApp<App>()
        .ConfigureFonts(fonts =>
```

(The license must be set before any `IDocument.GeneratePdf` call. Setting it here, at the entry point, guarantees that.)

- [ ] **Step 3: Build both targets to verify the package restores cleanly**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: each ends with `Build succeeded. 0 Warning(s) 0 Error(s)`. The first build will pull `QuestPDF` and its `SkiaSharp` transitive dependency; this may add ~30 seconds to the restore step.

- [ ] **Step 4: Smoke-test the app**

Use the run helper to relaunch on Mac Catalyst. Verify the app behaves identically to before — Login → Dashboard → Members search → Payments record → Attendance check-in all work. The license is set at startup but no PDF code runs yet.

- [ ] **Step 5: Commit**

```bash
git add Gymers/Gymers.csproj Gymers/MauiProgram.cs
git commit -m "feat(receipts): add QuestPDF + Community license

Foundation for receipt PDF generation. License is set at app
startup so any subsequent IDocument.GeneratePdf call is licensed
on first invocation. ReceiptService class itself lands in a
later task."
```

---

## Task 2: Extend `ListRow` with `Tapped` event + `CommandParameter`

After this task the shared `ListRow` ContentView fires a `Tapped` event when the user taps anywhere on the row body, with a per-row `CommandParameter` available to the handler. Members and Attendance pages don't subscribe to `Tapped` and don't set `CommandParameter`, so their behavior is unchanged. Build still green; no UI behavior change yet because nothing subscribes.

**Files:**
- Modify: `Gymers/Controls/ListRow.xaml`
- Modify: `Gymers/Controls/ListRow.xaml.cs`

- [ ] **Step 1: Add a `TapGestureRecognizer` to the inner `Border` in `ListRow.xaml`**

Open `Gymers/Controls/ListRow.xaml`. Find the `<Border>` opening tag at line 7:

```xml
<Border BackgroundColor="{StaticResource SurfaceMuted}"
        StrokeThickness="0" Padding="16">
```

Replace with:

```xml
<Border BackgroundColor="{StaticResource SurfaceMuted}"
        StrokeThickness="0" Padding="16">
    <Border.GestureRecognizers>
        <TapGestureRecognizer Tapped="OnRowTapped" />
    </Border.GestureRecognizers>
```

Note: do **not** add a closing `</Border.GestureRecognizers>` after the existing `<Grid>` — only the open block in front. The existing structure already closes with `</Grid></Border>` at the end. After the edit the `<Border>` opens, then `<Border.GestureRecognizers>` block, then `<Border.StrokeShape>` block, then the `<Grid>`. Verify by re-reading the file.

The full updated file should read:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:c="clr-namespace:Gymers.Controls"
             x:Class="Gymers.Controls.ListRow"
             x:Name="ThisRow">
    <Border BackgroundColor="{StaticResource SurfaceMuted}"
            StrokeThickness="0" Padding="16">
        <Border.GestureRecognizers>
            <TapGestureRecognizer Tapped="OnRowTapped" />
        </Border.GestureRecognizers>
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

- [ ] **Step 2: Add `CommandParameter` BindableProperty + `Tapped` event + handler in `ListRow.xaml.cs`**

Replace the entire contents of `Gymers/Controls/ListRow.xaml.cs` with:

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

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(ListRow));

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
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public event EventHandler? Tapped;

    public ListRow() => InitializeComponent();

    void OnRowTapped(object? sender, TappedEventArgs e) => Tapped?.Invoke(this, EventArgs.Empty);
}
```

Three things changed:
- Added `CommandParameterProperty` + `CommandParameter` getter/setter — pages can stash the row's domain object here (we'll use the `Payment` record).
- Added a public `Tapped` event of plain `EventHandler` type (we don't surface tap location to consumers — they only need to know "this row was tapped").
- Added `OnRowTapped` — the XAML `TapGestureRecognizer` (signature `EventHandler<TappedEventArgs>`) is wired to this handler, which re-fires our simpler `Tapped` event so consumers can subscribe with `row.Tapped += SomeHandler` where the handler matches `(object?, EventArgs)`.

- [ ] **Step 3: Build both targets**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: `0 Warning(s) 0 Error(s)` on both.

- [ ] **Step 4: Smoke-test the app**

Relaunch on Mac Catalyst. Verify:
- Members tab still works (search filters, no rows accidentally tappable in a way that visibly changes anything — they may now register taps but no handler is wired).
- Payments tab: rows still display correctly. Tapping does nothing visible (no subscriber yet).
- Attendance tab: rows still display correctly.

`ListRow` now has a tap mechanism but no consumer — Task 5 wires Payments to it.

- [ ] **Step 5: Commit**

```bash
git add Gymers/Controls/ListRow.xaml Gymers/Controls/ListRow.xaml.cs
git commit -m "feat(controls): add Tapped event + CommandParameter to ListRow

Inner Border now hosts a TapGestureRecognizer that re-fires as a
public Tapped event on the ContentView. CommandParameter
BindableProperty lets consumers stash a per-row domain object.
Members/Attendance don't subscribe, so their behavior is
unchanged. Wired into Payments in a later task."
```

---

## Task 3: `ReceiptDocument` — pure layout, no I/O

After this task `ReceiptDocument` exists as a compiled class implementing `QuestPDF.Infrastructure.IDocument`. It produces the spec layout when given a `Payment` and `Member`. It is not wired into anything yet. Build still green; behavior unchanged.

**Files:**
- Create: `Gymers/Services/ReceiptDocument.cs`

- [ ] **Step 1: Create the `Gymers/Services` directory if it doesn't exist**

```bash
mkdir -p Gymers/Services
```

- [ ] **Step 2: Create `Gymers/Services/ReceiptDocument.cs`**

```csharp
using System.Globalization;
using Gymers.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Gymers.Services;

public sealed class ReceiptDocument : IDocument
{
    static readonly string Teal      = "#0F766E";
    static readonly string Navy      = "#18212F";
    static readonly string MutedGrey = "#667085";
    static readonly string Divider   = "#E2E8F0";

    readonly Payment _payment;
    readonly Member? _member;

    public ReceiptDocument(Payment payment, Member? member)
    {
        _payment = payment;
        _member  = member;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title    = $"Gymers Receipt #{_payment.ReceiptNumber}",
        Author   = "Gymers",
        Subject  = "Payment Receipt",
        Producer = "Gymers Mobile App"
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(48);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(t => t.FontSize(11).FontColor(Navy));

            page.Header().Element(ComposeHeader);
            page.Content().PaddingVertical(16).Element(ComposeBody);
            page.Footer().Element(ComposeFooter);
        });
    }

    void ComposeHeader(IContainer header)
    {
        header.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("GYMERS")
                        .FontSize(28).Bold().FontColor(Teal);
                    c.Item().Text("Gym Management System")
                        .FontSize(11).FontColor(MutedGrey);
                });
                row.ConstantItem(120).AlignRight().Text("RECEIPT")
                    .FontSize(20).Bold().FontColor(Navy);
            });
            col.Item().PaddingTop(12).LineHorizontal(1).LineColor(Divider);
        });
    }

    void ComposeBody(IContainer body)
    {
        var memberName = _member?.Name ?? "(member removed)";
        var memberId   = _member?.Id   ?? "—";
        var memberTier = _member is null ? "—" : $"{_member.Tier} tier";

        body.Column(col =>
        {
            col.Spacing(20);

            col.Item().Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.Span("Receipt #").FontColor(MutedGrey);
                    t.Span(_payment.ReceiptNumber.ToString(CultureInfo.InvariantCulture))
                        .Bold().FontColor(Navy);
                });
                row.RelativeItem().AlignRight().Text(
                    _payment.At.ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture))
                    .FontColor(MutedGrey);
            });

            col.Item().Column(c =>
            {
                c.Item().PaddingBottom(4).Text("Member")
                    .FontSize(10).Bold().FontColor(Teal);
                c.Item().Text(memberName).FontSize(14).Bold();
                c.Item().Text($"ID: {memberId} · {memberTier}")
                    .FontSize(11).FontColor(MutedGrey);
            });

            col.Item().Column(c =>
            {
                c.Item().PaddingBottom(4).Text("Payment")
                    .FontSize(10).Bold().FontColor(Teal);
                c.Item().Row(r =>
                {
                    r.ConstantItem(120).Text("Amount").FontColor(MutedGrey);
                    r.RelativeItem().Text($"${_payment.Amount.ToString("0.00", CultureInfo.InvariantCulture)}")
                        .Bold();
                });
                c.Item().Row(r =>
                {
                    r.ConstantItem(120).Text("Method").FontColor(MutedGrey);
                    r.RelativeItem().Text(_payment.Method);
                });
            });

            col.Item().LineHorizontal(1).LineColor(Divider);
        });
    }

    void ComposeFooter(IContainer footer)
    {
        footer.Column(col =>
        {
            col.Item().Text("Thank you for being a Gymers member.")
                .FontSize(11).FontColor(Navy);
            col.Item().Text("This receipt was issued by the Gymers app.")
                .FontSize(10).FontColor(MutedGrey);
        });
    }
}
```

What this does:
- Implements `IDocument` so `QuestPDF`'s `GeneratePdf(stream)` / `GeneratePdf(path)` extension methods accept it.
- `GetMetadata()` populates the PDF's title bar text in Preview / Files.
- `Compose()` lays out a single A4 page with the spec layout: header band (GYMERS wordmark + "RECEIPT" + divider), body (receipt number, timestamp, member section, payment section), and footer (thank-you lines).
- All decimal/date formatting is `CultureInfo.InvariantCulture` so receipts are locale-stable for evaluators.
- Member-removed fallback: `_member` is nullable; if null, name renders as `(member removed)` and ID/tier render as `—`.

- [ ] **Step 3: Build both targets**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: `0 Warning(s) 0 Error(s)` on both. (If the build complains about `QuestPDF.Helpers.Colors` ambiguity with `Microsoft.Maui.Graphics.Colors`, the `using QuestPDF.Helpers;` line scopes correctly — the conflict only arises if you also `using Microsoft.Maui.Graphics;` in this file, which we don't.)

- [ ] **Step 4: Smoke-test the app**

Relaunch on Mac Catalyst. App behavior unchanged — `ReceiptDocument` is unused. This step only confirms nothing broke.

- [ ] **Step 5: Commit**

```bash
git add Gymers/Services/ReceiptDocument.cs
git commit -m "feat(receipts): add ReceiptDocument layout

Pure QuestPDF IDocument implementation. Builds a one-page A4
receipt from a Payment + (optional) Member, falling back to
'(member removed)' when the member id no longer resolves. All
formatting uses InvariantCulture so receipts are locale-stable.
Not yet wired into the app."
```

---

## Task 4: `ReceiptService` — file write + DI registration

After this task `ReceiptService` is registered in DI and can be injected into pages. It writes the PDF to `FileSystem.CacheDirectory/receipts/gymers-receipt-{ReceiptNumber}.pdf` and returns the path. Build still green; nothing calls it yet.

**Files:**
- Create: `Gymers/Services/ReceiptService.cs`
- Modify: `Gymers/MauiProgram.cs`

- [ ] **Step 1: Create `Gymers/Services/ReceiptService.cs`**

```csharp
using Gymers.Models;
using Microsoft.Maui.Storage;
using QuestPDF.Fluent;

namespace Gymers.Services;

public sealed class ReceiptService
{
    public async Task<string> GenerateAsync(Payment payment, Member? member)
    {
        var dir = Path.Combine(FileSystem.CacheDirectory, "receipts");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"gymers-receipt-{payment.ReceiptNumber}.pdf");
        var doc  = new ReceiptDocument(payment, member);

        await Task.Run(() => doc.GeneratePdf(path));
        return path;
    }
}
```

What this does:
- Builds the cache directory idempotently (`CreateDirectory` no-ops if it already exists).
- Filename uses the receipt number so the share sheet's title and the saved file name match what the user saw on screen.
- `Task.Run` offloads QuestPDF's synchronous `GeneratePdf(path)` to the thread pool — keeps the UI thread responsive while the share-sheet animation runs.
- File is unconditionally overwritten on each call (QuestPDF's path overload writes via a `FileStream` opened with default `FileMode.Create` semantics, which truncates any existing file).

- [ ] **Step 2: Register `ReceiptService` as a DI singleton in `MauiProgram.cs`**

Open `Gymers/MauiProgram.cs`. Add a new `using` directive at the top:

```csharp
using Gymers.Services;
```

Find this line:

```csharp
builder.Services.AddSingleton<DataStore>();
```

Add the new singleton registration immediately below it:

```csharp
builder.Services.AddSingleton<ReceiptService>();
```

So the DI block becomes:

```csharp
builder.Services.AddSingleton<DataStore>();
builder.Services.AddSingleton<ReceiptService>();

builder.Services.AddTransient<Pages.LoginPage>();
builder.Services.AddTransient<Pages.DashboardPage>();
builder.Services.AddTransient<Pages.MembersPage>();
builder.Services.AddTransient<Pages.PaymentsPage>();
builder.Services.AddTransient<Pages.AttendancePage>();
```

`ReceiptService` is stateless, so singleton is fine and avoids per-page allocation.

- [ ] **Step 3: Build both targets**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: `0 Warning(s) 0 Error(s)`.

- [ ] **Step 4: Smoke-test the app**

Relaunch on Mac Catalyst. App behavior unchanged — `ReceiptService` is registered but uninjected. Confirm nothing broke at startup; if DI registration is malformed, `MauiProgram` throws on `builder.Build()` and the app fails to launch.

- [ ] **Step 5: Commit**

```bash
git add Gymers/Services/ReceiptService.cs Gymers/MauiProgram.cs
git commit -m "feat(receipts): add ReceiptService + DI registration

Writes a receipt PDF to FileSystem.CacheDirectory/receipts/
and returns the absolute path. Registered as a DI singleton.
Nothing injects it yet — PaymentsPage wiring lands next."
```

---

## Task 5: Wire per-row tap on PaymentsPage → receipt → share sheet

After this task tapping any row in Recent Payments generates a PDF and opens the system share sheet. The trailing chevron renders on payment rows to signal interactivity. Errors surface via the existing red `StatusLabel` pattern. This is the slice's user-facing payoff.

**Files:**
- Modify: `Gymers/Pages/PaymentsPage.xaml.cs`

- [ ] **Step 1: Replace `Gymers/Pages/PaymentsPage.xaml.cs` body**

Replace the entire file with:

```csharp
using System.Globalization;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Gymers.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls.Shapes;

namespace Gymers.Pages;

public partial class PaymentsPage : ContentPage
{
    readonly DataStore _data;
    readonly ReceiptService _receipts;
    IDispatcherTimer? _statusTimer;

    public PaymentsPage(DataStore data, ReceiptService receipts)
    {
        _data     = data;
        _receipts = receipts;
        InitializeComponent();
        RecordButton.Clicked += OnRecord;
        _data.Payments.CollectionChanged += (_, _) => Render();
        Render();
    }

    async void OnRecord(object? sender, EventArgs e)
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

        var payment = await _data.RecordPaymentAsync(member, amount, method);

        MemberInput.Text = "";
        AmountInput.Text = "";
        MethodInput.Text = "";
        ShowSuccess($"Recorded ${payment.Amount:0.00} · Receipt #{payment.ReceiptNumber}.");
    }

    async void OnRowTapped(object? sender, EventArgs e)
    {
        if (sender is not ListRow row || row.CommandParameter is not Payment payment)
            return;

        try
        {
            var member = _data.Members.FirstOrDefault(m => m.Id == payment.MemberId);
            var path   = await _receipts.GenerateAsync(payment, member);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"Gymers Receipt #{payment.ReceiptNumber}",
                File  = new ShareFile(path)
            });
        }
        catch (Exception ex)
        {
            ShowError($"Couldn't generate receipt: {ex.Message}");
        }
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
            var row = new ListRow
            {
                LeadingContent   = MakeAmountPill(p.Amount),
                Title            = displayName,
                Subtitle         = $"${p.Amount:0.00} · {p.Method} · Receipt #{p.ReceiptNumber}",
                TrailingChevron  = true,
                CommandParameter = p
            };
            row.Tapped += OnRowTapped;
            PaymentList.Children.Add(row);
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

What changed vs. the previous version:
- Constructor takes `ReceiptService` and stores it. DI provides it automatically because Task 4 registered it.
- New `using` lines: `Gymers.Services` (for `ReceiptService`) and `Microsoft.Maui.ApplicationModel.DataTransfer` (for `Share`, `ShareFileRequest`, `ShareFile`).
- New `OnRowTapped` handler: pulls the `Payment` out of `CommandParameter`, looks up the member (may be null), generates the PDF, hands it to `Share.Default.RequestAsync`. Wraps the whole thing in a try/catch so any QuestPDF or share-sheet failure surfaces in `StatusLabel`.
- `Render()` now sets `TrailingChevron = true` and `CommandParameter = p` on each row, and subscribes to `row.Tapped`. Members and Attendance pages stay unchanged because they don't take this code path.
- `OnRecord` is unchanged.

The `OnRowTapped` signature `(object? sender, EventArgs e)` matches the `EventHandler` delegate that `ListRow.Tapped` declares (Task 2). We don't need tap-location data here — just "the row was tapped" — so plain `EventArgs` is correct.

- [ ] **Step 2: Build both targets**

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: `0 Warning(s) 0 Error(s)`.

- [ ] **Step 3: Manual verify — seed receipt re-issue**

Relaunch on Mac Catalyst. Sign in `admin / admin123`. Tap **Payments**.

Verify:
1. Each row in Recent Payments now shows a small lime trailing chevron (the existing `TrailingChevron` UI element, now flipped on).
2. Tap the top seeded row (`Receipt #1042`, Marcus Sterling, $99.00, Card). The macOS share sheet appears with title `Gymers Receipt #1042` and a PDF preview.
3. Choose **Save to Files…**, save anywhere convenient. Open the saved PDF in Preview.
4. The PDF shows: `GYMERS` wordmark + "RECEIPT" header band, divider, `Receipt #1042` and the timestamp, `Member` section with `Marcus Sterling` + `ID: M-001 · Premium tier`, `Payment` section with `$99.00` + `Card`, and the two thank-you lines at the bottom.

- [ ] **Step 4: Manual verify — live receipt for a freshly recorded payment**

Still in the same launch session: record a new payment using the form — `Diego Alvarez / 50 / bank` → success `Recorded $50.00 · Receipt #1043.` Top row in Recent Payments shows `Diego Alvarez · $50.00 · Bank · Receipt #1043`.

Tap the new row. Share sheet opens with title `Gymers Receipt #1043`. Save and inspect the PDF — fields all match (`Diego Alvarez`, `$50.00`, `Bank`, current timestamp).

- [ ] **Step 5: Manual verify — re-tap idempotency**

Tap the same row twice in succession. Second tap should produce a fresh share sheet (the same file is overwritten in cache; the share sheet sees the same path). No crash, no zombie file. Confirm only one `gymers-receipt-1043.pdf` exists:

```bash
find ~/Library/Containers -path "*/receipts/gymers-receipt-*.pdf" 2>/dev/null
```

- [ ] **Step 6: Manual verify — member-removed fallback**

Quit the app:

```bash
pkill -f "Gymers/bin/Debug/net10.0-maccatalyst" 2>/dev/null; sleep 1
```

Find the SQLite file and delete one member row. Use `sqlite3` from the CLI:

```bash
DB=$(find ~/Library/Containers -name "gymers.db3" 2>/dev/null | head -1)
sqlite3 "$DB" "DELETE FROM MemberRow WHERE Id = 'M-006';"
```

(`M-006` is Priya Shah; if your seed mapping differs, pick any member id that has at least one payment in the seed.)

Relaunch:

```bash
open Gymers/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Gymers.app
```

Sign in. On the Payments tab, the row whose `MemberId` matched the deleted member now shows `Unknown member` as its title (the existing `Render()` fallback). Tap that row. Share sheet still opens. Saved PDF renders with `(member removed)` as the member name and `—` for ID and tier. No crash.

- [ ] **Step 7: Manual verify — validation regression sweep**

In the same launch session, confirm Record Payment validation still works exactly as before (Receipt PDF logic does not touch this flow):
- Empty member → red `No member named "". …`
- `Lena Park / 0 / card` → red `Amount must be a positive number with up to 2 decimals.`
- `Lena Park / 25 / Crypto` → red `Method must be Card, Cash, or Bank.`

Quick cross-tab check:
- Members tab: search `lena` → only Lena visible. Search `zzz` → empty-state notice.
- Attendance tab: type `aisha`, tap suggestion, tap CHECK IN → success row appears.

If any of these regressed, the slice has accidentally broken something orthogonal — investigate before continuing.

- [ ] **Step 8: Restore the deleted member (cleanup before commit)**

After the member-removed verification passes, restore the deleted seed row so subsequent runs aren't polluted:

```bash
DB=$(find ~/Library/Containers -name "gymers.db3" 2>/dev/null | head -1)
rm "$DB"
```

(Wiping the DB is simpler than re-inserting the row by hand. The next launch re-seeds from `SampleData`.)

- [ ] **Step 9: Commit**

```bash
git add Gymers/Pages/PaymentsPage.xaml.cs
git commit -m "feat(payments): tap a row to share a PDF receipt

OnRowTapped resolves the row's Payment + Member, calls
ReceiptService.GenerateAsync, and hands the resulting file to
the system share sheet. Each row's trailing chevron flips on
to signal interactivity. Errors surface via the existing
red StatusLabel pattern."
```

---

## Task 6: Status doc — flip "Receipt PDF generation" to Completed

After this task the status doc reflects the new shipped feature. The .docx is regenerated.

**Files:**
- Modify: `docs/status/build_status_docx.py`
- Modify: `docs/status/gymers-mobile-app-status-update.html`

- [ ] **Step 1: Move the bullet from Ongoing Tasks → completed_rows in `build_status_docx.py`**

Open `docs/status/build_status_docx.py`. Find the line in the `Ongoing Tasks` block:

```python
bullet("Receipt PDF generation: payments now record correctly into the store; automatic PDF receipt creation is still pending."),
```

Delete that line.

Find the `completed_rows` list (starts around line 80). Add a new row at the end of the list, immediately after the existing SQLite persistence row:

```python
["Receipt PDF generation",
 "Completed",
 "Tapping any row in Recent Payments generates a one-page PDF receipt via QuestPDF (Community license) under FileSystem.CacheDirectory/receipts/, then opens the system share sheet for save/email/print. Re-issues are deterministic from SQLite, so any historical payment can be re-printed."],
```

The completed_rows list will end with this new entry.

- [ ] **Step 2: Mirror the change in `gymers-mobile-app-status-update.html`**

Open `docs/status/gymers-mobile-app-status-update.html`. Find the `<ul>` under the "Ongoing Tasks" heading. Remove the `<li>` for "Receipt PDF generation".

Find the Completed Features `<table>`. Add a new `<tr>` after the SQLite row:

```html
<tr>
    <td>Receipt PDF generation</td>
    <td>Completed</td>
    <td>Tapping any row in Recent Payments generates a one-page PDF receipt via QuestPDF (Community license) under FileSystem.CacheDirectory/receipts/, then opens the system share sheet for save/email/print. Re-issues are deterministic from SQLite, so any historical payment can be re-printed.</td>
</tr>
```

(Match the exact column structure of the existing rows. If the prior rows use `<th>` for the first column, adjust accordingly — re-read the file to confirm before editing.)

- [ ] **Step 3: Regenerate the .docx**

```bash
python3 docs/status/build_status_docx.py
```

Expected output: a single line printing the absolute path to the regenerated `.docx`. (The `.docx` itself is gitignored — only `build_status_docx.py` and the `.html` are tracked.)

- [ ] **Step 4: Sanity-check the regenerated doc**

Open `docs/status/Gymers-Mobile-App-Status-Update.docx` and verify:
- Completed Features table includes a row for "Receipt PDF generation".
- Ongoing Tasks bullet list no longer contains the receipt PDF item.
- Other rows and bullets are unchanged.

- [ ] **Step 5: Commit**

```bash
git add docs/status/build_status_docx.py docs/status/gymers-mobile-app-status-update.html
git commit -m "docs(status): mark Receipt PDF generation as completed

Slice landed. Tap a Recent Payments row → share sheet with the
PDF. Status doc updated to move the item from Ongoing Tasks
to Completed Features."
```

---

## Task 7: Final Verification Walk

After Task 6 is committed, run the full demo end-to-end in one launch session — exercising payment record, receipt re-issue, and the predecessor SQLite persistence — to prove the slice is complete and orthogonal regressions haven't crept in.

- [ ] **Step 1: Wipe the DB and receipts cache, then rebuild fresh**

```bash
DB=$(find ~/Library/Containers -name "gymers.db3" 2>/dev/null | head -1)
[ -n "$DB" ] && rm "$DB" && echo "Removed $DB" || echo "No DB to remove"
find ~/Library/Containers -path "*/receipts/gymers-receipt-*.pdf" -delete 2>/dev/null
pkill -f "Gymers/bin/Debug/net10.0-maccatalyst" 2>/dev/null; sleep 1
dotnet build Gymers/Gymers.csproj -f net10.0-maccatalyst -c Debug
open Gymers/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Gymers.app
```

- [ ] **Step 2: First-launch seed verification**

Sign in `admin / admin123`. Verify:
- **Members** — 6 seeded rows (Marcus, Lena, Diego, Aisha, Sam, Priya). Search filter still works.
- **Payments** — 5 seeded rows newest-first. Each row has a lime trailing chevron.
- **Attendance** — 6 seeded check-ins newest-first.

- [ ] **Step 3: Receipt — seed re-issue**

On Payments, tap `Receipt #1042` (Marcus Sterling). Share sheet opens with title `Gymers Receipt #1042`. Save to Files. Open the PDF in Preview.

PDF must contain:
- `GYMERS` wordmark in teal, "RECEIPT" header.
- `Receipt #1042` and a timestamp.
- `Member` section with `Marcus Sterling`, `ID: M-001 · Premium tier`.
- `Payment` section with `$99.00`, `Card`.
- Thank-you footer.

- [ ] **Step 4: Mutate then receipt then restart-survival**

Record a payment: `Aisha Khan / 120 / cash` → success `Recorded $120.00 · Receipt #1043.` Top row reads `Aisha Khan · $120.00 · Cash · Receipt #1043`.

Tap the new row. Share sheet shows `Gymers Receipt #1043`. Save and verify PDF fields match (`Aisha Khan`, `$120.00`, `Cash`, current time).

Quit and relaunch:

```bash
pkill -f "Gymers/bin/Debug/net10.0-maccatalyst" 2>/dev/null; sleep 1
open Gymers/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Gymers.app
```

Sign in. Payments → top row is still `Aisha Khan · $120.00 · Cash · Receipt #1043`. Tap it again → fresh share sheet → PDF still renders correctly. (This proves the receipt path doesn't depend on in-session state — it reads from the persistent DB.)

- [ ] **Step 5: Validation regression sweep**

In the post-restart session, run a quick error-path sweep. Each must still behave exactly as before:

- Login: empty fields → red `Enter username and password.` Wrong creds → red `Invalid credentials for the selected role.`
- Members: type `lena` → only Lena visible. Type `zzz` → muted `No members match "zzz".`
- Payments: empty member → red `No member named "". …`. `Lena Park / 0 / card` → red amount error. `Lena Park / 25 / Crypto` → red method error.
- Attendance: empty search, tap CHECK IN → red `Select a member first.` Unknown name `Bob` → no suggestions, CHECK IN → red `Select a member first.`

- [ ] **Step 6: iOS target builds clean**

The slice's verification target is Mac Catalyst, but iOS-target builds must remain green:

```bash
dotnet build Gymers/Gymers.csproj -f net10.0-ios -c Debug
```

Expected: `0 Warning(s) 0 Error(s)`.

- [ ] **Step 7: No-op — final verification is now complete**

If every box above is checked, the slice is done. The Task 6 commit is the final commit; no additional commit for verification.

---

## Self-review notes (for the implementer)

- **QuestPDF Community license.** Set in `MauiProgram.cs` before `builder.Build()`. If you forget, the first `IDocument.GeneratePdf` throws a license-required exception with a stack trace pointing at QuestPDF.Settings. The error message is clear; just add the line.
- **`Task.Run(() => doc.GeneratePdf(path))`** is deliberate. QuestPDF's API is sync; calling it on the UI thread janks the share-sheet animation on Mac Catalyst (visible as a stutter when the share sheet first appears).
- **Share API is async.** `Share.Default.RequestAsync` returns a `Task` that completes when the share sheet is dismissed (with or without action). Cancellation is not surfaced as an exception; the `Task` simply completes. No try/catch needed for cancel.
- **`TapGestureRecognizer` on a stroked Border** sometimes ghost-fires on Mac Catalyst when the stroke is zero. The existing `ListRow` Border has `StrokeThickness="0"` and worked fine in informal testing of similar MAUI projects; if you see double-fires, set `InputTransparent="False"` on the inner `Grid`.
- **`CommandParameter` is `object?`** in the BindableProperty declaration. The handler in `PaymentsPage` does the `is Payment payment` pattern match, so a misuse on another page (e.g. assigning a `Member` to it) would just no-op rather than crash.
- **Decimal formatting.** `payment.Amount.ToString("0.00", CultureInfo.InvariantCulture)` — never `:F2` without culture, never `ToString("C")`. We always render `$xx.xx` regardless of device locale; receipts are documents, not UI-localizable.
- **Date formatting.** `payment.At.ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture)`. The middle dot (`·`, U+00B7) matches the existing app's typography. If your editor or terminal mangles it during paste, use the literal U+00B7 character — not the ASCII bullet `•` or hyphens.
- **Receipt cache survives across launches** because `FileSystem.CacheDirectory` is OS-managed cache, not app-deleted-on-launch storage. The OS may evict it under disk pressure; we don't depend on that, since every tap regenerates the file unconditionally.
- **No XAML changes beyond the `TapGestureRecognizer` line** in `ListRow.xaml`. If you find yourself editing other `.xaml` files, you've drifted from this plan — verify the spec.
- **No tests.** Verification is manual, matching the slice's predecessor. Don't add xUnit. The QuestPDF team's own tests cover the layout engine; we'd just be re-testing it.
- **iOS sim:** don't try to verify on the iOS simulator. It's unusable on this hardware. Mac Catalyst is the verification target. iOS-target builds must still succeed (Step 6 of Task 7 covers that).
