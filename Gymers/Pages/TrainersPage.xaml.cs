using System.ComponentModel;
using System.Globalization;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Gymers.Services;
using Microsoft.Maui.Controls.Shapes;

namespace Gymers.Pages;

public partial class TrainersPage : ContentPage
{
    readonly DataStore _data;
    Trainer? _editingTrainer;

    Entry? _nameEntry;
    Entry? _titleEntry;
    Entry? _ratingEntry;
    Entry? _sessionsEntry;
    Label? _formError;

    public TrainersPage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        AddTrainerButton.IsVisible = Session.Current.IsAdmin;
        AddTrainerButton.Clicked += (_, _) => ShowTrainerForm(null);
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

    View BuildRow(Trainer t)
    {
        var row = new VerticalStackLayout { Spacing = 8 };
        row.Children.Add(new ListRow
        {
            LeadingContent = AvatarFactory.MakeInitial(t.Name),
            Title          = t.Name,
            Subtitle       = $"{t.Title} · {t.Rating:0.0}/5.0 · {t.SessionsCompleted} sessions\n{TrainerSchedules.GetFor(t.Id)}"
        });

        if (Session.Current.IsAdmin)
            row.Children.Add(BuildActions(() => ShowTrainerForm(t), () => DeleteTrainerAsync(t)));

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

    void ShowTrainerForm(Trainer? trainer)
    {
        _editingTrainer = trainer;
        _nameEntry = new Entry { Text = trainer?.Name ?? "", Placeholder = "Trainer name" };
        _titleEntry = new Entry { Text = trainer?.Title ?? "", Placeholder = "Title" };
        _ratingEntry = new Entry
        {
            Text = trainer?.Rating.ToString("0.0", CultureInfo.InvariantCulture) ?? "4.5",
            Placeholder = "Rating",
            Keyboard = Keyboard.Numeric
        };
        _sessionsEntry = new Entry
        {
            Text = trainer?.SessionsCompleted.ToString(CultureInfo.InvariantCulture) ?? "0",
            Placeholder = "Sessions completed",
            Keyboard = Keyboard.Numeric
        };
        _formError = ErrorLabel();

        ShowOverlay(trainer is null ? "Add Trainer" : "Edit Trainer",
        new View[]
        {
            Label("Name"), _nameEntry,
            Label("Title"), _titleEntry,
            Label("Rating"), _ratingEntry,
            Label("Sessions Completed"), _sessionsEntry,
            _formError
        }, SaveTrainerAsync);
    }

    async void SaveTrainerAsync()
    {
        string name = _nameEntry?.Text?.Trim() ?? "";
        string title = _titleEntry?.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(title))
        {
            ShowFormError("Name and title are required.");
            return;
        }

        if (!decimal.TryParse(_ratingEntry?.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal rating) ||
            rating < 0 || rating > 5)
        {
            ShowFormError("Rating must be between 0.0 and 5.0.");
            return;
        }

        if (!int.TryParse(_sessionsEntry?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sessions) ||
            sessions < 0)
        {
            ShowFormError("Sessions must be 0 or higher.");
            return;
        }

        try
        {
            if (_editingTrainer is null)
                await _data.AddTrainerAsync(name, title, rating, sessions);
            else
                await _data.UpdateTrainerAsync(_editingTrainer with
                {
                    Name = name,
                    Title = title,
                    Rating = rating,
                    SessionsCompleted = sessions
                });

            HideOverlay();
        }
        catch (Exception ex)
        {
            ShowFormError($"Couldn't save trainer: {ex.Message}");
        }
    }

    async void DeleteTrainerAsync(Trainer trainer)
    {
        if (!await DisplayAlertAsync("Delete trainer?", $"Remove {trainer.Name} from Trainers?", "DELETE", "CANCEL"))
            return;

        try
        {
            await _data.DeleteTrainerAsync(trainer);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Delete failed", ex.Message, "OK");
        }
    }

    void ShowOverlay(string title, View[] fields, Action save)
    {
        FormOverlay.Children.Clear();
        var saveButton = new PrimaryButton { Text = _editingTrainer is null ? "ADD" : "SAVE" };
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
