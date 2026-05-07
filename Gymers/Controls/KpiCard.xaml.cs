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
