namespace Gymers.Controls;

public enum AppTab { Dashboard, Members, Payments, Attendance, Reports, Trainers, Workouts, Equipment }

public partial class BottomTabBar : ContentView
{
    public static readonly BindableProperty ActiveTabProperty =
        BindableProperty.Create(nameof(ActiveTab), typeof(AppTab), typeof(BottomTabBar), AppTab.Dashboard,
            propertyChanged: (b, _, _) => ((BottomTabBar)b).ApplyActive());

    public AppTab ActiveTab
    {
        get => (AppTab)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    public BottomTabBar()
    {
        InitializeComponent();
        ApplyActive();
    }

    void ApplyActive()
    {
        var pale         = (Color)Application.Current!.Resources["PaleBlue"];
        var navyHeading  = (Color)Application.Current.Resources["NavyHeading"];
        var muted        = (Color)Application.Current.Resources["TextMuted"];

        DashboardPill.BackgroundColor  = ActiveTab == AppTab.Dashboard  ? pale : Colors.Transparent;
        MembersPill.BackgroundColor    = ActiveTab == AppTab.Members    ? pale : Colors.Transparent;
        PaymentsPill.BackgroundColor   = ActiveTab == AppTab.Payments   ? pale : Colors.Transparent;
        AttendancePill.BackgroundColor = ActiveTab == AppTab.Attendance ? pale : Colors.Transparent;
        ReportsPill.BackgroundColor    = ActiveTab == AppTab.Reports    ? pale : Colors.Transparent;

        DashboardGlyph.TextColor  = DashboardLabel.TextColor  = ActiveTab == AppTab.Dashboard  ? navyHeading : muted;
        MembersGlyph.TextColor    = MembersLabel.TextColor    = ActiveTab == AppTab.Members    ? navyHeading : muted;
        PaymentsGlyph.TextColor   = PaymentsLabel.TextColor   = ActiveTab == AppTab.Payments   ? navyHeading : muted;
        AttendanceGlyph.TextColor = AttendanceLabel.TextColor = ActiveTab == AppTab.Attendance ? navyHeading : muted;
        ReportsGlyph.TextColor    = ReportsLabel.TextColor    = ActiveTab == AppTab.Reports    ? navyHeading : muted;
    }

    async void OnDashboardTapped(object? sender, TappedEventArgs e)  => await Shell.Current.GoToAsync("//Dashboard");
    async void OnMembersTapped(object? sender, TappedEventArgs e)    => await Shell.Current.GoToAsync("//Members");
    async void OnPaymentsTapped(object? sender, TappedEventArgs e)   => await Shell.Current.GoToAsync("//Payments");
    async void OnAttendanceTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("//Attendance");
    async void OnReportsTapped(object? sender, TappedEventArgs e)    => await Shell.Current.GoToAsync("//Reports");
}
