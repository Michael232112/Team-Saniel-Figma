using Microsoft.Extensions.Logging;

namespace Gymers;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
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

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
