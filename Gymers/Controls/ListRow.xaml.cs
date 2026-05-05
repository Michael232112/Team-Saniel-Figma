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

    public ListRow() => InitializeComponent();
}
