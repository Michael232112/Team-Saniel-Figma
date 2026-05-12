using System.ComponentModel;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Gymers.Services;
using Microsoft.Maui.Controls.Shapes;

namespace Gymers.Pages;

public partial class EquipmentPage : ContentPage
{
    readonly DataStore _data;
    Equipment? _editingEquipment;

    Entry? _nameEntry;
    Entry? _categoryEntry;
    Picker? _statusPicker;
    Entry? _locationEntry;
    Label? _formError;

    public EquipmentPage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        AddEquipmentButton.IsVisible = Session.Current.IsAdmin;
        AddEquipmentButton.Clicked += (_, _) => ShowEquipmentForm(null);
        Search.PropertyChanged += OnSearchChanged;
        _data.Equipment.CollectionChanged += (_, _) =>
        {
            ApplyStatusKpi();
            Render(Search.Text ?? "");
        };
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

    View BuildRow(Equipment e)
    {
        var row = new VerticalStackLayout { Spacing = 8 };
        row.Children.Add(new ListRow
        {
            LeadingContent = AvatarFactory.MakeInitial(e.Name),
            Title          = e.Name,
            Subtitle       = $"{e.Category} · {e.Status} · {e.Location}"
        });

        if (Session.Current.IsAdmin)
            row.Children.Add(BuildActions(() => ShowEquipmentForm(e), () => DeleteEquipmentAsync(e)));

        return row;
    }

    static HorizontalStackLayout BuildActions(Action edit, Action delete)
    {
        var editButton = new SecondaryButton { Text = "EDIT" };
        var deleteButton = new SecondaryButton { Text = "DELETE" };
        editButton.Clicked += (_, _) => edit();
        deleteButton.Clicked += (_, _) => delete();
        return new HorizontalStackLayout
        {
            Spacing = 18,
            HorizontalOptions = LayoutOptions.End,
            Children = { editButton, deleteButton }
        };
    }

    void ShowEquipmentForm(Equipment? item)
    {
        _editingEquipment = item;
        _nameEntry = new Entry { Text = item?.Name ?? "", Placeholder = "Equipment name" };
        _categoryEntry = new Entry { Text = item?.Category ?? "", Placeholder = "Category" };
        _statusPicker = Picker("Status", new[] { "Operational", "Maintenance", "Retired" });
        _locationEntry = new Entry { Text = item?.Location ?? "", Placeholder = "Location" };
        _formError = ErrorLabel();
        _statusPicker.SelectedItem = item?.Status ?? "Operational";

        ShowOverlay(item is null ? "Add Equipment" : "Edit Equipment",
        new View[]
        {
            Label("Name"), _nameEntry,
            Label("Category"), _categoryEntry,
            Label("Status"), _statusPicker,
            Label("Location"), _locationEntry,
            _formError
        }, SaveEquipmentAsync);
    }

    async void SaveEquipmentAsync()
    {
        string name = _nameEntry?.Text?.Trim() ?? "";
        string category = _categoryEntry?.Text?.Trim() ?? "";
        string status = _statusPicker?.SelectedItem as string ?? "";
        string location = _locationEntry?.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(category) ||
            string.IsNullOrWhiteSpace(status) ||
            string.IsNullOrWhiteSpace(location))
        {
            ShowFormError("Name, category, status, and location are required.");
            return;
        }

        try
        {
            if (_editingEquipment is null)
                await _data.AddEquipmentAsync(name, category, status, location);
            else
                await _data.UpdateEquipmentAsync(_editingEquipment with
                {
                    Name = name,
                    Category = category,
                    Status = status,
                    Location = location
                });

            HideOverlay();
        }
        catch (Exception ex)
        {
            ShowFormError($"Couldn't save equipment: {ex.Message}");
        }
    }

    async void DeleteEquipmentAsync(Equipment item)
    {
        if (!await DisplayAlertAsync("Delete equipment?", $"Remove {item.Name} from Equipment?", "DELETE", "CANCEL"))
            return;

        try
        {
            await _data.DeleteEquipmentAsync(item);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Delete failed", ex.Message, "OK");
        }
    }

    void ShowOverlay(string title, View[] fields, Action save)
    {
        FormOverlay.Children.Clear();
        var saveButton = new PrimaryButton { Text = _editingEquipment is null ? "ADD" : "SAVE" };
        var cancelButton = new SecondaryButton { Text = "CANCEL" };
        saveButton.Clicked += (_, _) => save();
        cancelButton.Clicked += (_, _) => HideOverlay();

        var stack = new VerticalStackLayout { Spacing = 10 };
        stack.Children.Add(new Label { Text = title, Style = (Style)Application.Current!.Resources["H2Section"] });
        foreach (var field in fields) stack.Children.Add(field);
        stack.Children.Add(saveButton);
        stack.Children.Add(cancelButton);

        FormOverlay.Children.Add(new Border
        {
            Padding = 18,
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            VerticalOptions = LayoutOptions.Center,
            Content = stack
        });
        FormOverlay.IsVisible = true;
    }

    void HideOverlay() => FormOverlay.IsVisible = false;

    void ShowFormError(string message)
    {
        if (_formError is null) return;
        _formError.Text = message;
        _formError.IsVisible = true;
    }

    static Picker Picker(string title, IEnumerable<string> values)
    {
        var picker = new Picker { Title = title };
        foreach (var value in values) picker.Items.Add(value);
        return picker;
    }

    static Label Label(string text) => new()
    {
        Text = text,
        Style = (Style)Application.Current!.Resources["BodySm"],
        TextColor = (Color)Application.Current.Resources["TextMuted"]
    };

    static Label ErrorLabel() => new()
    {
        IsVisible = false,
        Style = (Style)Application.Current!.Resources["BodySm"],
        TextColor = (Color)Application.Current.Resources["Danger"]
    };
}
