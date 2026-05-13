namespace Gymers.Controls;

public partial class TopAppBar : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(TopAppBar), string.Empty);

    public static readonly BindableProperty ShowAvatarProperty =
        BindableProperty.Create(nameof(ShowAvatar), typeof(bool), typeof(TopAppBar), true);

    public static readonly BindableProperty ShowLogoutProperty =
        BindableProperty.Create(nameof(ShowLogout), typeof(bool), typeof(TopAppBar), true);

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

    public bool ShowLogout
    {
        get => (bool)GetValue(ShowLogoutProperty);
        set => SetValue(ShowLogoutProperty, value);
    }

    public string TrailingIconGlyph
    {
        get => (string)GetValue(TrailingIconGlyphProperty);
        set => SetValue(TrailingIconGlyphProperty, value);
    }

    public TopAppBar() => InitializeComponent();

    async void OnLogoutTapped(object? sender, TappedEventArgs e)
    {
        Services.Session.Current.SignOut();
        await Shell.Current.GoToAsync("//Login");
    }
}
