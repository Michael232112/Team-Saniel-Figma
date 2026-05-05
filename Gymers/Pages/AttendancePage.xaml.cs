using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Microsoft.Maui.Controls.Shapes;

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
