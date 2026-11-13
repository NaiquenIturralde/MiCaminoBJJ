#if MAUI_DEVFLOW
using Microsoft.Maui.DevFlow.Agent;
using Microsoft.Maui.DevFlow.Blazor;
#endif
using Microsoft.Extensions.Logging;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;
using MiCaminoBJJ.Infrastructure.Data.Seed;
using MiCaminoBJJ.Infrastructure.Extensions;
using MiCaminoBJJ.Infrastructure.Services;
using MiCaminoBJJ.Services;

namespace MiCaminoBJJ;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("Poppins-Regular.ttf", "PoppinsRegular");
                fonts.AddFont("Poppins-Medium.ttf", "PoppinsMedium");
                fonts.AddFont("Poppins-SemiBold.ttf", "PoppinsSemiBold");
                fonts.AddFont("Poppins-Bold.ttf", "PoppinsBold");
            });

        builder.Services.AddMauiBlazorWebView();

#if MAUI_DEVFLOW
        builder.AddMauiDevFlowAgent();
        builder.AddMauiBlazorDevFlowTools();
#endif

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Database
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MiCaminoBJJ", "micaminobjj.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        builder.Services.AddInfrastructure(dbPath);

        // Session - App level with SecureStorage backing
        builder.Services.AddScoped<ISessionService, SecureSessionService>();

        // Media selection (MAUI FilePicker/MediaPicker)
        builder.Services.AddScoped<IMediaSelectionService, MauiMediaSelectionService>();

        var app = builder.Build();

        // Initialize database synchronously at startup
        using (var scope = app.Services.CreateScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            initializer.InitializeAsync().GetAwaiter().GetResult();
        }

        return app;
    }
}
