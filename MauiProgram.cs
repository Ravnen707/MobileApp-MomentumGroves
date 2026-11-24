using MomentumGroves.Mobile.Views;
using ZXing.Net.Maui.Controls;

namespace MomentumGroves.Mobile;

/// <summary>
/// Configures the Android application.
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// Creates the MAUI application.
    /// </summary>
    /// <returns>The configured application.</returns>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.UseBarcodeReader();
        builder.Services.AddSingleton<Service.LocalFoodService>();
        builder.Services.AddSingleton<Service.SyncService>();
        builder.Services.AddSingleton<Service.TrainingSyncService>();
        builder.Services.AddSingleton<Service.OpenFoodFactsService>();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<OfflineTrainingPage>();
        builder.Services.AddSingleton<PcSyncPage>();
        builder.Services.AddSingleton<HomePage>();
        return builder.Build();
    }
}
