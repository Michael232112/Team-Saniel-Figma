namespace Gymers.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage() => InitializeComponent();

    void OnSelectAdmin(object? sender, TappedEventArgs e)
    {
        var navy = (Color)Application.Current!.Resources["NavyDeep"];
        var sec  = (Color)Application.Current.Resources["TextSecondary"];
        AdminPill.BackgroundColor = navy;
        StaffPill.BackgroundColor = Colors.Transparent;
        AdminLabel.TextColor = Colors.White;
        StaffLabel.TextColor = sec;
    }

    void OnSelectStaff(object? sender, TappedEventArgs e)
    {
        var navy = (Color)Application.Current!.Resources["NavyDeep"];
        var sec  = (Color)Application.Current.Resources["TextSecondary"];
        AdminPill.BackgroundColor = Colors.Transparent;
        StaffPill.BackgroundColor = navy;
        AdminLabel.TextColor = sec;
        StaffLabel.TextColor = Colors.White;
    }

    async void OnSignIn(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Dashboard");
    }
}
