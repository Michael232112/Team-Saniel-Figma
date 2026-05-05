using Gymers.Controls;
using Gymers.Data;
using Gymers.Models;
using Microsoft.Maui.Controls.Shapes;

namespace Gymers.Pages;

public partial class PaymentsPage : ContentPage
{
    public PaymentsPage()
    {
        InitializeComponent();
        BuildPaymentList();
    }

    void BuildPaymentList()
    {
        foreach (var p in SampleData.Payments)
        {
            var member = SampleData.GetMember(p.MemberId);
            PaymentList.Children.Add(new ListRow
            {
                LeadingContent = MakeAmountPill(p.Amount),
                Title          = member.Name,
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
}
