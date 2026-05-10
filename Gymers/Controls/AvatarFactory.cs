using Microsoft.Maui.Controls.Shapes;

namespace Gymers.Controls;

public static class AvatarFactory
{
    public static View MakeInitial(string name)
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
