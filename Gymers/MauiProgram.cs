using Microsoft.Extensions.Logging;
using Gymers.Data;
using QuestPDF.Infrastructure;

namespace Gymers;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();

		QuestPDF.Settings.License = LicenseType.Community;

		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("Manrope-Bold.ttf",      "ManropeBold");
				fonts.AddFont("Manrope-ExtraBold.ttf", "ManropeExtraBold");
				fonts.AddFont("Manrope-SemiBold.ttf",  "ManropeSemiBold");
				fonts.AddFont("Inter-Regular.ttf",     "InterRegular");
				fonts.AddFont("Inter-Medium.ttf",      "InterMedium");
				fonts.AddFont("Inter-SemiBold.ttf",    "InterSemiBold");
				fonts.AddFont("Lucide.ttf",            "LucideIcons");
			});

		builder.Services.AddSingleton<DataStore>();

		builder.Services.AddTransient<Pages.LoginPage>();
		builder.Services.AddTransient<Pages.DashboardPage>();
		builder.Services.AddTransient<Pages.MembersPage>();
		builder.Services.AddTransient<Pages.PaymentsPage>();
		builder.Services.AddTransient<Pages.AttendancePage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
