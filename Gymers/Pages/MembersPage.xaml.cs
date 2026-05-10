using System.ComponentModel;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;

namespace Gymers.Pages;

public partial class MembersPage : ContentPage
{
    readonly DataStore _data;

    public MembersPage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        Search.PropertyChanged += OnSearchChanged;
        _data.Members.CollectionChanged += (_, _) => Render(Search.Text ?? "");
        Render("");
    }

    void OnSearchChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchField.Text))
            Render(Search.Text ?? "");
    }

    void Render(string query)
    {
        MemberList.Children.Clear();
        var matches = _data.SearchMembers(query).ToList();

        if (matches.Count == 0)
        {
            MemberList.Children.Add(new Label
            {
                Text = $"No members match \"{query.Trim()}\".",
                Style = (Style)Application.Current!.Resources["BodyMd"],
                TextColor = (Color)Application.Current.Resources["TextMuted"],
                HorizontalTextAlignment = TextAlignment.Center
            });
            return;
        }

        foreach (var m in matches)
            MemberList.Children.Add(BuildRow(m));
    }

    static View BuildRow(Member m) => new ListRow
    {
        LeadingContent = AvatarFactory.MakeInitial(m.Name),
        Title          = m.Name,
        Subtitle       = $"{m.Tier} · {m.Status} · Expires {m.Expires:MM/dd/yyyy}"
    };
}
