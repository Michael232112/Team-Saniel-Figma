using System.ComponentModel;
using System.Globalization;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Gymers.Services;
using Microsoft.Maui.Controls.Shapes;

namespace Gymers.Pages;

public partial class WorkoutsPage : ContentPage
{
    readonly DataStore _data;
    WorkoutPlan? _editingPlan;

    Entry? _nameEntry;
    Picker? _trainerPicker;
    Picker? _levelPicker;
    Entry? _sessionsEntry;
    Entry? _durationEntry;
    Editor? _summaryEditor;
    Label? _formError;

    public WorkoutsPage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        AddPlanButton.IsVisible = Session.Current.IsAdmin;
        AddPlanButton.Clicked += (_, _) => ShowPlanForm(null);
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

    View BuildRow(WorkoutPlan p)
    {
        var row = new VerticalStackLayout { Spacing = 8 };
        row.Children.Add(new ListRow
        {
            LeadingContent = AvatarFactory.MakeInitial(p.Name),
            Title          = p.Name,
            Subtitle       = $"{_data.TrainerName(p.TrainerId)} · {p.Level} · {p.SessionsPerWeek}×/wk · {p.DurationWeeks} wk"
        });

        if (Session.Current.IsAdmin)
            row.Children.Add(BuildActions(() => ShowPlanForm(p), () => DeletePlanAsync(p)));

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

    void ShowPlanForm(WorkoutPlan? plan)
    {
        _editingPlan = plan;
        _nameEntry = new Entry { Text = plan?.Name ?? "", Placeholder = "Plan name" };
        _trainerPicker = new Picker { Title = "Trainer" };
        foreach (var trainer in _data.Trainers)
            _trainerPicker.Items.Add(trainer.Name);

        _levelPicker = Picker("Level", new[] { "Beginner", "Intermediate", "Advanced" });
        _sessionsEntry = new Entry
        {
            Text = plan?.SessionsPerWeek.ToString(CultureInfo.InvariantCulture) ?? "3",
            Placeholder = "Sessions per week",
            Keyboard = Keyboard.Numeric
        };
        _durationEntry = new Entry
        {
            Text = plan?.DurationWeeks.ToString(CultureInfo.InvariantCulture) ?? "4",
            Placeholder = "Duration weeks",
            Keyboard = Keyboard.Numeric
        };
        _summaryEditor = new Editor
        {
            Text = plan?.Summary ?? "",
            Placeholder = "Plan summary",
            AutoSize = EditorAutoSizeOption.TextChanges,
            HeightRequest = 84
        };
        _formError = ErrorLabel();

        _trainerPicker.SelectedIndex = plan is null
            ? (_data.Trainers.Count == 0 ? -1 : 0)
            : _data.Trainers.ToList().FindIndex(t => t.Id == plan.TrainerId);
        _levelPicker.SelectedItem = plan?.Level ?? "Beginner";

        ShowOverlay(plan is null ? "Add Workout Plan" : "Edit Workout Plan",
        new View[]
        {
            Label("Name"), _nameEntry,
            Label("Trainer"), _trainerPicker,
            Label("Level"), _levelPicker,
            Label("Sessions Per Week"), _sessionsEntry,
            Label("Duration Weeks"), _durationEntry,
            Label("Summary"), _summaryEditor,
            _formError
        }, SavePlanAsync);
    }

    async void SavePlanAsync()
    {
        string name = _nameEntry?.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowFormError("Plan name is required.");
            return;
        }

        if (_trainerPicker is null || _trainerPicker.SelectedIndex < 0 || _trainerPicker.SelectedIndex >= _data.Trainers.Count)
        {
            ShowFormError("Choose an assigned trainer.");
            return;
        }

        string level = _levelPicker?.SelectedItem as string ?? "";
        if (string.IsNullOrWhiteSpace(level))
        {
            ShowFormError("Choose a level.");
            return;
        }

        if (!int.TryParse(_sessionsEntry?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sessions) ||
            sessions < 1 || sessions > 7)
        {
            ShowFormError("Sessions per week must be 1 through 7.");
            return;
        }

        if (!int.TryParse(_durationEntry?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int duration) ||
            duration < 1)
        {
            ShowFormError("Duration must be at least 1 week.");
            return;
        }

        string summary = _summaryEditor?.Text?.Trim() ?? "";
        string trainerId = _data.Trainers[_trainerPicker.SelectedIndex].Id;

        try
        {
            if (_editingPlan is null)
                await _data.AddWorkoutPlanAsync(name, trainerId, level, sessions, duration, summary);
            else
                await _data.UpdateWorkoutPlanAsync(_editingPlan with
                {
                    Name = name,
                    TrainerId = trainerId,
                    Level = level,
                    SessionsPerWeek = sessions,
                    DurationWeeks = duration,
                    Summary = summary
                });

            HideOverlay();
        }
        catch (Exception ex)
        {
            ShowFormError($"Couldn't save workout plan: {ex.Message}");
        }
    }

    async void DeletePlanAsync(WorkoutPlan plan)
    {
        if (!await DisplayAlertAsync("Delete workout plan?", $"Remove {plan.Name} from Workout Plans?", "DELETE", "CANCEL"))
            return;

        try
        {
            await _data.DeleteWorkoutPlanAsync(plan);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Delete failed", ex.Message, "OK");
        }
    }

    void ShowOverlay(string title, View[] fields, Action save)
    {
        FormOverlay.Children.Clear();
        var saveButton = new PrimaryButton { Text = _editingPlan is null ? "ADD" : "SAVE" };
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
            Content = new ScrollView { Content = stack }
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
