using System.Globalization;
using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Gymers.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls.Shapes;

namespace Gymers.Pages;

public partial class PaymentsPage : ContentPage
{
    readonly DataStore _data;
    readonly ReceiptService _receipts;
    IDispatcherTimer? _statusTimer;

    public PaymentsPage(DataStore data, ReceiptService receipts)
    {
        _data     = data;
        _receipts = receipts;
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

    async void OnRowTapped(object? sender, EventArgs e)
    {
        if (sender is not ListRow row || row.CommandParameter is not Payment payment)
            return;

        try
        {
            var member = _data.Members.FirstOrDefault(m => m.Id == payment.MemberId);
            var path   = await _receipts.GenerateAsync(payment, member);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"Gymers Receipt #{payment.ReceiptNumber}",
                File  = new ShareFile(path)
            });
        }
        catch (Exception ex)
        {
            ShowError($"Couldn't generate receipt: {ex.Message}");
        }
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
            var row = new ListRow
            {
                LeadingContent   = MakeAmountPill(p.Amount),
                Title            = displayName,
                Subtitle         = $"${p.Amount:0.00} · {p.Method} · Receipt #{p.ReceiptNumber}",
                TrailingChevron  = true,
                CommandParameter = p
            };
            row.Tapped += OnRowTapped;
            PaymentList.Children.Add(row);
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
