using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Gymers.Services;
using Microsoft.Maui.Controls.Shapes;

namespace Gymers.Pages;

public partial class DashboardPage : ContentPage
{
    readonly DataStore _data;

    public DashboardPage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        ApplyRole();
        ApplyExpirySoonBanner();
        _data.Members.CollectionChanged += (_, _) => ApplyExpirySoonBanner();
        ApplyCoachSpotlight();
        ProfileButton.Clicked += async (_, _) =>
            await Shell.Current.GoToAsync("//Trainers");
        ApplyFeaturedPlan();
        BrowsePlansButton.Clicked += async (_, _) =>
            await Shell.Current.GoToAsync("//Workouts");
        ApplyEquipmentStatus();
        BrowseEquipmentButton.Clicked += async (_, _) =>
            await Shell.Current.GoToAsync("//Equipment");
        BuildClassList();
    }

    void ApplyRole()
    {
        var session = Session.Current;
        RoleBadge.Text               = $"Signed in as {session.RoleLabel}";
        MonthlyEarningsKpi.IsVisible = session.IsAdmin;
    }

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
        ExpirySoonBody.Text = "Tap to review: " + string.Join(", ", expiring.Select(m => m.Name));
    }

    async void OnExpirySoonTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync("//Members");

    void ApplyCoachSpotlight()
    {
        var top = _data.TopTrainer();
        // Empty-trainers fallback: keep the design-time XAML text. Task 4 re-seeds on
        // every empty-table launch, so this branch is unreachable today; spec §7's
        // "No trainers configured" placeholder card is deferred until the table can be
        // wiped at runtime.
        if (top is null) return;

        CoachInitials.Text = InitialsFor(top.Name);
        CoachName.Text     = top.Name;
        CoachTitle.Text    = top.Title;
        CoachRating.Text   = $"{top.Rating:0.0}/5.0";
        CoachSessions.Text = top.SessionsCompleted.ToString("N0");
        CoachSchedule.Text = TrainerSchedules.GetFor(top.Id);
    }

    void ApplyFeaturedPlan()
    {
        var top = _data.TopPlan();
        if (top is null)
        {
            FeaturedPlanName.Text         = "No plans configured.";
            FeaturedPlanTrainer.IsVisible = false;
            FeaturedPlanMeta.IsVisible    = false;
            FeaturedPlanSummary.IsVisible = false;
            BrowsePlansButton.IsVisible   = false;
            return;
        }

        FeaturedPlanName.Text    = top.Name;
        FeaturedPlanTrainer.Text = _data.TrainerName(top.TrainerId);
        FeaturedPlanMeta.Text    = $"{top.Level}  ·  {top.SessionsPerWeek}×/wk  ·  {top.DurationWeeks} wk";
        FeaturedPlanSummary.Text = top.Summary;
    }

    void ApplyEquipmentStatus()
    {
        int total       = _data.Equipment.Count;
        int operational = _data.OperationalEquipmentCount();
        int maintenance = _data.MaintenanceEquipmentCount();

        if (total == 0)
        {
            EquipmentHeadline.Text             = "No equipment configured.";
            EquipmentTotalLabel.IsVisible      = false;
            EquipmentMaintenanceMeta.IsVisible = false;
            EquipmentSummary.IsVisible         = false;
            BrowseEquipmentButton.IsVisible    = false;
            return;
        }

        EquipmentHeadline.Text        = $"{operational} / {total}";
        EquipmentTotalLabel.Text      = $"{total} item{(total == 1 ? "" : "s")}";
        EquipmentMaintenanceMeta.Text = maintenance == 1
            ? "1 under maintenance"
            : $"{maintenance} under maintenance";
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
