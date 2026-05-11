using System.ComponentModel;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;

namespace Gymers.Pages;

public partial class TrainersPage : ContentPage
{
    readonly DataStore _data;

    public TrainersPage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        Search.PropertyChanged += OnSearchChanged;
        _data.Trainers.CollectionChanged += (_, _) => Render(Search.Text ?? "");
        Render("");
    }

    void OnSearchChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchField.Text))
            Render(Search.Text ?? "");
    }

    void Render(string query)
    {
        TrainerList.Children.Clear();
        var matches = _data.SearchTrainers(query).ToList();

        if (matches.Count == 0)
        {
            TrainerList.Children.Add(new Label
            {
                Text = $"No trainers match \"{query.Trim()}\".",
                Style = (Style)Application.Current!.Resources["BodyMd"],
                TextColor = (Color)Application.Current.Resources["TextMuted"],
                HorizontalTextAlignment = TextAlignment.Center
            });
            return;
        }

        foreach (var t in matches)
            TrainerList.Children.Add(BuildRow(t));
    }

    static View BuildRow(Trainer t) => new ListRow
    {
        LeadingContent = AvatarFactory.MakeInitial(t.Name),
        Title          = t.Name,
        Subtitle       = $"{t.Title} · {t.Rating:0.0}/5.0 · {t.SessionsCompleted} sessions"
    };
}
