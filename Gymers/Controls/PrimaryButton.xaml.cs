using System.Windows.Input;

namespace Gymers.Controls;

public partial class PrimaryButton : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(PrimaryButton), string.Empty);

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(PrimaryButton));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public event EventHandler? Clicked;

    public PrimaryButton() => InitializeComponent();

    void OnTapped(object? sender, TappedEventArgs e)
    {
        if (!IsEnabled) return;
        Clicked?.Invoke(this, EventArgs.Empty);
        if (Command?.CanExecute(null) == true)
            Command.Execute(null);
    }
}
