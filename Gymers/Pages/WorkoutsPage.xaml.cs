using System.ComponentModel;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;

namespace Gymers.Pages;

public partial class WorkoutsPage : ContentPage
{
    readonly DataStore _data;

    public WorkoutsPage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        Search.PropertyChanged += OnSearchChanged;
        _data.WorkoutPlans.CollectionChanged += (_, _) => Render(Search.Text ?? "");
        Render("");
    }

    void OnSearchChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchField.Text))
            Render(Search.Text ?? "");
    }

    void Render(string query)
    {
        PlanList.Children.Clear();
        var matches = _data.SearchWorkoutPlans(query).ToList();

        if (matches.Count == 0)
        {
            PlanList.Children.Add(new Label
            {
                Text = $"No plans match \"{query.Trim()}\".",
                Style = (Style)Application.Current!.Resources["BodyMd"],
                TextColor = (Color)Application.Current.Resources["TextMuted"],
                HorizontalTextAlignment = TextAlignment.Center
            });
            return;
        }

        foreach (var p in matches)
            PlanList.Children.Add(BuildRow(p));
    }

    View BuildRow(WorkoutPlan p) => new ListRow
    {
        LeadingContent = AvatarFactory.MakeInitial(p.Name),
        Title          = p.Name,
        Subtitle       = $"{_data.TrainerName(p.TrainerId)} · {p.Level} · {p.SessionsPerWeek}×/wk · {p.DurationWeeks} wk"
    };
}
