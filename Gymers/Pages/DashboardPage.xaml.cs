using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Microsoft.Maui.Controls.Shapes;

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
