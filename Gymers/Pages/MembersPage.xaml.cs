using System.ComponentModel;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Microsoft.Maui.Controls.Shapes;

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
        LeadingContent = MakeInitialAvatar(m.Name),
        Title          = m.Name,
        Subtitle       = $"{m.Tier} · {m.Status} · Expires {m.Expires:MM/dd/yyyy}"
    };

    static View MakeInitialAvatar(string name)
    {
        var pale = (Color)Application.Current!.Resources["PaleBlue"];
        var navy = (Color)Application.Current.Resources["NavyHeading"];
        var initial = name.Length > 0 ? name[0].ToString() : "?";
        return new Border
        {
            BackgroundColor = pale,
            StrokeThickness = 0,
            WidthRequest = 40,
            HeightRequest = 40,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            Content = new Label
            {
                Text = initial,
                FontFamily = "ManropeBold",
                FontSize = 16,
                TextColor = navy,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
    }
}
