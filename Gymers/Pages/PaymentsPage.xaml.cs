using System.Globalization;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Microsoft.Maui.Controls.Shapes;

namespace Gymers.Pages;

public partial class PaymentsPage : ContentPage
{
    readonly DataStore _data;
    IDispatcherTimer? _statusTimer;

    public PaymentsPage(DataStore data)
    {
        _data = data;
        InitializeComponent();
        RecordButton.Clicked += OnRecord;
        _data.Payments.CollectionChanged += (_, _) => Render();
        Render();
    }

    async void OnRecord(object? sender, EventArgs e)
    {
        var nameRaw   = MemberInput.Text?.Trim() ?? "";
        var amountRaw = AmountInput.Text?.Trim() ?? "";
        var methodRaw = MethodInput.Text?.Trim() ?? "";

        var member = _data.FindMemberByName(nameRaw);
        if (member is null)
        { ShowError($"No member named \"{nameRaw}\". Try {SuggestNames()}."); return; }

        if (!decimal.TryParse(amountRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            || amount <= 0
            || decimal.Round(amount, 2) != amount)
        { ShowError("Amount must be a positive number with up to 2 decimals."); return; }

        var method = methodRaw.ToLowerInvariant() switch
        {
            "card" => "Card",
            "cash" => "Cash",
            "bank" => "Bank",
            _      => null
        };
        if (method is null)
        { ShowError("Method must be Card, Cash, or Bank."); return; }

        var payment = await _data.RecordPaymentAsync(member, amount, method);

        MemberInput.Text = "";
        AmountInput.Text = "";
        MethodInput.Text = "";
        ShowSuccess($"Recorded ${payment.Amount:0.00} · Receipt #{payment.ReceiptNumber}.");
    }

    string SuggestNames() =>
        string.Join(", ", _data.Members.Take(3).Select(m => m.Name));

    void Render()
    {
        PaymentList.Children.Clear();
        foreach (var p in _data.Payments)
        {
            var member = _data.Members.FirstOrDefault(m => m.Id == p.MemberId);
            var displayName = member?.Name ?? "Unknown member";
            PaymentList.Children.Add(new ListRow
            {
                LeadingContent = MakeAmountPill(p.Amount),
                Title          = displayName,
                Subtitle       = $"${p.Amount:0.00} · {p.Method} · Receipt #{p.ReceiptNumber}"
            });
        }
    }

    static View MakeAmountPill(decimal amount)
    {
        var pale = (Color)Application.Current!.Resources["PaleBlue"];
        var navy = (Color)Application.Current.Resources["NavyHeading"];
        return new Border
        {
            BackgroundColor = pale,
            StrokeThickness = 0,
            WidthRequest = 56,
            HeightRequest = 40,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            Content = new Label
            {
                Text = $"${(int)amount}",
                FontFamily = "ManropeBold",
                FontSize = 14,
                TextColor = navy,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
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
