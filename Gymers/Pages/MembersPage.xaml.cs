using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Microsoft.Maui.Controls.Shapes;

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
