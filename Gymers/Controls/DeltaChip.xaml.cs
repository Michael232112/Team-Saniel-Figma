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
        GlyphLabel.Text      = Direction == DeltaDirection.Up ? Icons.ArrowUp : Icons.ArrowUp;
    }
}

public enum DeltaDirection { Up, Down }
