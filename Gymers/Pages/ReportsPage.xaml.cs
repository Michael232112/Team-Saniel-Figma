using Gymers.Data;
using Gymers.Models;
using Gymers.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace Gymers.Pages;

public partial class ReportsPage : ContentPage
{
    readonly DataStore     _data;
    readonly ReportService _reports;

    ReportPeriod _period = ReportPeriod.Month;

    public ReportsPage(DataStore data, ReportService reports)
    {
        _data    = data;
        _reports = reports;
        InitializeComponent();

        if (!Session.Current.IsAdmin)
        {
            Loaded += async (_, _) => await Shell.Current.GoToAsync("//Dashboard");
            return;
        }

        WeekButton.Clicked  += (_, _) => SetPeriod(ReportPeriod.Week);
        MonthButton.Clicked += (_, _) => SetPeriod(ReportPeriod.Month);
        AllButton.Clicked   += (_, _) => SetPeriod(ReportPeriod.All);

        RevenuePdfButton.Clicked    += (_, _) => SharePdf(ReportKind.Revenue);
        RevenueCsvButton.Clicked    += (_, _) => ShareCsv(ReportKind.Revenue);
        AttendancePdfButton.Clicked += (_, _) => SharePdf(ReportKind.Attendance);
        AttendanceCsvButton.Clicked += (_, _) => ShareCsv(ReportKind.Attendance);
        RosterPdfButton.Clicked     += (_, _) => SharePdf(ReportKind.Roster);
        RosterCsvButton.Clicked     += (_, _) => ShareCsv(ReportKind.Roster);

        SetPeriod(_period);
    }

    void SetPeriod(ReportPeriod period)
    {
        _period = period;
        RefreshPeriodButtons();
        RefreshSummaries();
    }

    void RefreshPeriodButtons()
    {
        var pale  = (Color)Application.Current!.Resources["PaleBlue"];
        var navy  = (Color)Application.Current.Resources["NavyHeading"];
        var muted = (Color)Application.Current.Resources["TextMuted"];

        Paint(WeekButton,  _period == ReportPeriod.Week);
        Paint(MonthButton, _period == ReportPeriod.Month);
        Paint(AllButton,   _period == ReportPeriod.All);

        void Paint(Button b, bool active)
        {
            b.BackgroundColor = active ? pale  : Colors.Transparent;
            b.TextColor       = active ? navy  : muted;
        }
    }

    void RefreshSummaries()
    {
        RevenueSummary.Text    = _reports.Summarize(ReportKind.Revenue,    _period);
        AttendanceSummary.Text = _reports.Summarize(ReportKind.Attendance, _period);
        RosterSummary.Text     = _reports.Summarize(ReportKind.Roster,     _period);
    }

    async void SharePdf(ReportKind kind)
    {
        try
        {
            HideStatus();
            var path = await _reports.GeneratePdfAsync(kind, _period);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"Gymers — {kind.Label()} Report ({_period.Label()})",
                File  = new ShareFile(path)
            });
        }
        catch (Exception ex)
        {
            ShowError($"Couldn't generate PDF: {ex.Message}");
        }
    }

    async void ShareCsv(ReportKind kind)
    {
        try
        {
            HideStatus();
            var path = await _reports.GenerateCsvAsync(kind, _period);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"Gymers — {kind.Label()} Report ({_period.Label()}) [CSV]",
                File  = new ShareFile(path)
            });
        }
        catch (Exception ex)
        {
            ShowError($"Couldn't generate CSV: {ex.Message}");
        }
    }

    void ShowError(string text)
    {
        StatusLabel.Text = text;
        StatusLabel.TextColor = (Color)Application.Current!.Resources["Danger"];
        StatusLabel.IsVisible = true;
    }

    void HideStatus() => StatusLabel.IsVisible = false;
}
