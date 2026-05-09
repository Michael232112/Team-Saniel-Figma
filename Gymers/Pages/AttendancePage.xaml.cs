using System.ComponentModel;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Microsoft.Maui.Controls.Shapes;

namespace Gymers.Pages;

public partial class AttendancePage : ContentPage
{
    readonly DataStore _data;
    Member? _selected;
    IDispatcherTimer? _statusTimer;

    public AttendancePage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        MemberSearch.PropertyChanged += OnSearchChanged;
        CheckInButton.Clicked += OnCheckIn;
        _data.CheckIns.CollectionChanged += (_, _) => Render();
        Render();
    }

    void OnSearchChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SearchField.Text)) return;
        var q = MemberSearch.Text?.Trim() ?? "";

        if (q.Length == 0)
        {
            Suggestions.IsVisible = false;
            Suggestions.Children.Clear();
            _selected = null;
            return;
        }

        var exact = _data.FindMemberByName(q);
        if (exact is not null)
        {
            _selected = exact;
            Suggestions.IsVisible = false;
            Suggestions.Children.Clear();
            return;
        }

        _selected = null;
        var matches = _data.SearchMembers(q).Take(3).ToList();
        Suggestions.Children.Clear();
        foreach (var m in matches) Suggestions.Children.Add(BuildSuggestion(m));
        Suggestions.IsVisible = matches.Count > 0;
    }

    View BuildSuggestion(Member m)
    {
        var label = new Label
        {
            Text = m.Name,
            Style = (Style)Application.Current!.Resources["BodyMd"]
        };
        var border = new Border
        {
            BackgroundColor = (Color)Application.Current.Resources["SurfaceMuted"],
            StrokeThickness = 0,
            Padding = new Thickness(16, 8),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Content = label
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            _selected = m;
            MemberSearch.Text = m.Name;
            Suggestions.IsVisible = false;
        };
        border.GestureRecognizers.Add(tap);
        return border;
    }

    async void OnCheckIn(object? sender, EventArgs e)
    {
        if (_selected is null) { ShowError("Select a member first."); return; }
        var member = _selected;
        var c = await _data.RecordCheckInAsync(member);
        _selected = null;
        MemberSearch.Text = "";
        Suggestions.IsVisible = false;
        ShowSuccess($"Checked in {member.Name} at {c.At:hh\\:mm tt}.");
    }

    void Render()
    {
        CheckInList.Children.Clear();

        if (_data.CheckIns.Count == 0)
        {
            CheckInList.Children.Add(new Label
            {
                Text = "No check-ins yet today.",
                Style = (Style)Application.Current!.Resources["BodyMd"],
                TextColor = (Color)Application.Current.Resources["TextMuted"],
                HorizontalTextAlignment = TextAlignment.Center
            });
            return;
        }

        foreach (var c in _data.CheckIns)
        {
            var member = _data.Members.FirstOrDefault(m => m.Id == c.MemberId);
            var displayName = member?.Name ?? "Unknown member";
            CheckInList.Children.Add(new ListRow
            {
                LeadingContent = MakeStatusDot(),
                Title          = displayName,
                Subtitle       = $"Checked in · {c.At:hh\\:mm tt}"
            });
        }
    }

    static View MakeStatusDot()
    {
        var olive = (Color)Application.Current!.Resources["Olive"];
        return new Border
        {
            BackgroundColor = olive,
            StrokeThickness = 0,
            WidthRequest = 12,
            HeightRequest = 12,
            VerticalOptions = LayoutOptions.Center,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) }
        };
    }

    void ShowError(string text)
    {
        _statusTimer?.Stop();
        StatusLabel.Text = text;
        StatusLabel.TextColor = (Color)Application.Current!.Resources["Danger"];
        StatusLabel.IsVisible = true;
    }

    void ShowSuccess(string text)
    {
        StatusLabel.Text = text;
        StatusLabel.TextColor = (Color)Application.Current!.Resources["Olive"];
        StatusLabel.IsVisible = true;

        _statusTimer?.Stop();
        _statusTimer = Dispatcher.CreateTimer();
        _statusTimer.Interval = TimeSpan.FromSeconds(2.5);
        _statusTimer.IsRepeating = false;
        _statusTimer.Tick += (_, _) => StatusLabel.IsVisible = false;
        _statusTimer.Start();
    }
}
