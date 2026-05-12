using System.ComponentModel;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Gymers.Services;
using Microsoft.Maui.Controls.Shapes;

namespace Gymers.Pages;

public partial class MembersPage : ContentPage
{
    readonly DataStore _data;
    Member? _editingMember;

    Entry? _nameEntry;
    Picker? _tierPicker;
    Picker? _statusPicker;
    DatePicker? _expiryPicker;
    Label? _formError;

    public MembersPage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        AddMemberButton.IsVisible = Session.Current.IsAdmin;
        AddMemberButton.Clicked += (_, _) => ShowMemberForm(null);
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

    View BuildRow(Member m)
    {
        var row = new VerticalStackLayout { Spacing = 8 };
        row.Children.Add(new ListRow
        {
            LeadingContent = AvatarFactory.MakeInitial(m.Name),
            Title          = m.Name,
            Subtitle       = $"{m.Tier} · {m.Status} · Expires {m.Expires:MM/dd/yyyy}"
        });

        if (Session.Current.IsAdmin)
            row.Children.Add(BuildActions(() => ShowMemberForm(m), () => DeleteMemberAsync(m)));

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

    void ShowMemberForm(Member? member)
    {
        _editingMember = member;
        var expiryDate = (member?.Expires ?? DateOnly.FromDateTime(DateTime.Today.AddMonths(1))).ToDateTime(TimeOnly.MinValue);
        var minimumDate = member is null || expiryDate >= DateTime.Today ? DateTime.Today : expiryDate;
        _nameEntry = new Entry { Text = member?.Name ?? "", Placeholder = "Member name" };
        _tierPicker = Picker("Tier", Enum.GetNames<MembershipTier>());
        _statusPicker = Picker("Status", new[] { "Active", "Expiring Soon", "Inactive" });
        _expiryPicker = new DatePicker
        {
            MinimumDate = minimumDate,
            Date = expiryDate
        };
        _formError = ErrorLabel();

        _tierPicker.SelectedItem = (member?.Tier ?? MembershipTier.Basic).ToString();
        _statusPicker.SelectedItem = member?.Status ?? "Active";

        ShowOverlay(member is null ? "Add Member" : "Edit Member",
        new View[]
        {
            Label("Name"), _nameEntry,
            Label("Tier"), _tierPicker,
            Label("Status"), _statusPicker,
            Label("Expires"), _expiryPicker,
            _formError
        }, SaveMemberAsync);
    }

    async void SaveMemberAsync()
    {
        string name = _nameEntry?.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowFormError("Member name is required.");
            return;
        }

        if (_tierPicker?.SelectedItem is not string tierText ||
            !Enum.TryParse(tierText, out MembershipTier tier))
        {
            ShowFormError("Choose a membership tier.");
            return;
        }

        string status = _statusPicker?.SelectedItem as string ?? "Active";
        var expires = DateOnly.FromDateTime(_expiryPicker?.Date ?? DateTime.Today);

        try
        {
            if (_editingMember is null)
                await _data.AddMemberAsync(name, tier, status, expires);
            else
                await _data.UpdateMemberAsync(_editingMember with { Name = name, Tier = tier, Status = status, Expires = expires });

            HideOverlay();
        }
        catch (Exception ex)
        {
            ShowFormError($"Couldn't save member: {ex.Message}");
        }
    }

    async void DeleteMemberAsync(Member member)
    {
        if (!await DisplayAlertAsync("Delete member?", $"Remove {member.Name} from Members?", "DELETE", "CANCEL"))
            return;

        try
        {
            await _data.DeleteMemberAsync(member);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Delete failed", ex.Message, "OK");
        }
    }

    void ShowOverlay(string title, View[] fields, Action save)
    {
        FormOverlay.Children.Clear();
        var saveButton = new PrimaryButton { Text = _editingMember is null ? "ADD" : "SAVE" };
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
