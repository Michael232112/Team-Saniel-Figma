using System.ComponentModel;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;

namespace Gymers.Pages;

public partial class EquipmentPage : ContentPage
{
    readonly DataStore _data;

    public EquipmentPage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        Search.PropertyChanged += OnSearchChanged;
        _data.Equipment.CollectionChanged += (_, _) => Render(Search.Text ?? "");
        ApplyStatusKpi();
        Render("");
    }

    void OnSearchChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchField.Text))
            Render(Search.Text ?? "");
    }

    void ApplyStatusKpi()
    {
        int operational = _data.OperationalEquipmentCount();
        int total       = _data.Equipment.Count;
        StatusKpi.Value   = operational.ToString();
        StatusKpi.Caption = $"of {total} item{(total == 1 ? "" : "s")}";
    }

    void Render(string query)
    {
        EquipmentList.Children.Clear();
        var matches = _data.SearchEquipment(query).ToList();

        if (matches.Count == 0)
        {
            EquipmentList.Children.Add(new Label
            {
                Text = $"No equipment matches \"{query.Trim()}\".",
                Style = (Style)Application.Current!.Resources["BodyMd"],
                TextColor = (Color)Application.Current.Resources["TextMuted"],
                HorizontalTextAlignment = TextAlignment.Center
            });
            return;
        }

        foreach (var e in matches)
            EquipmentList.Children.Add(BuildRow(e));
    }

    View BuildRow(Equipment e) => new ListRow
    {
        LeadingContent = AvatarFactory.MakeInitial(e.Name),
        Title          = e.Name,
        Subtitle       = $"{e.Category} · {e.Status} · {e.Location}"
    };
}
