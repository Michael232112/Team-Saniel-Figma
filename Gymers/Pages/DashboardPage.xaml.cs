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
