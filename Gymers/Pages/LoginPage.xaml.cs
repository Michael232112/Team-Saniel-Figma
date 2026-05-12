namespace Gymers.Pages;

public partial class LoginPage : ContentPage
{
    enum SelectedRole { Admin, Staff }
    SelectedRole _role = SelectedRole.Admin;

    public LoginPage() => InitializeComponent();

    void OnSelectAdmin(object? sender, TappedEventArgs e)
    {
        _role = SelectedRole.Admin;
        var navy = (Color)Application.Current!.Resources["NavyDeep"];
        var sec  = (Color)Application.Current.Resources["TextSecondary"];
        AdminPill.BackgroundColor = navy;
        StaffPill.BackgroundColor = Colors.Transparent;
        AdminLabel.TextColor = Colors.White;
        StaffLabel.TextColor = sec;
    }

    void OnSelectStaff(object? sender, TappedEventArgs e)
    {
        _role = SelectedRole.Staff;
        var navy = (Color)Application.Current!.Resources["NavyDeep"];
        var sec  = (Color)Application.Current.Resources["TextSecondary"];
        AdminPill.BackgroundColor = Colors.Transparent;
        StaffPill.BackgroundColor = navy;
        AdminLabel.TextColor = sec;
        StaffLabel.TextColor = Colors.White;
    }

    async void OnSignIn(object? sender, EventArgs e)
    {
        var u = UsernameInput.Text?.Trim() ?? "";
        var p = PasswordInput.Text ?? "";

        if (u.Length == 0 || p.Length == 0)
        { ShowError("Enter username and password."); return; }

        bool ok = (_role == SelectedRole.Admin && u == "admin" && p == "admin123")
               || (_role == SelectedRole.Staff && u == "staff" && p == "staff123");

        if (!ok)
        { ShowError("Invalid credentials for the selected role."); return; }

        ErrorLabel.IsVisible = false;
        Services.Session.Current.SignIn(u, _role == SelectedRole.Admin);
        await Shell.Current.GoToAsync("//Dashboard");
    }

    void ShowError(string text)
    {
        ErrorLabel.Text = text;
        ErrorLabel.IsVisible = true;
    }
}
