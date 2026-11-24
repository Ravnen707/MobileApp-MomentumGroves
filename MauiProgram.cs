using MomentumGroves.Mobile.Configuration;
using MomentumGroves.Mobile.Data;
using MomentumGroves.Mobile.Data.Local;
using MomentumGroves.Mobile.Data.Supabase;
using MomentumGroves.Mobile.Infrastructure.Supabase;
using MomentumGroves.Mobile.Views;
using MomentumGroves.Mobile.Service;
using MomentumGroves.Mobile.Service.Supabase;
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
        var url = Preferences.Default.Get(SupabaseConfiguration.ProjectUrlKey, "");
        var key = Preferences.Default.Get(SupabaseConfiguration.PublishableKey, "");
        var cloudEnabled = Preferences.Default.Get(SupabaseConfiguration.CloudEnabledKey, false);
        builder.Services.AddSingleton(new DataProvider(cloudEnabled));
        builder.Services.AddSingleton(Preferences.Default);
        builder.Services.AddSingleton(SecureStorage.Default);
        builder.Services.AddSingleton<LocalFoodStore>();
        if (cloudEnabled)
        {
            builder.Services.AddSingleton(_ => SupabaseConfiguration.Parse(url, key));
            builder.Services.AddSingleton(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromSeconds(15)
            });
            builder.Services.AddSingleton<SupabaseSessionStorage>();
            builder.Services.AddSingleton<SupabaseSessionService>();
            builder.Services.AddSingleton(services => new SupabaseTrainingImageCache(
                services.GetRequiredService<SupabaseConfiguration>(),
                services.GetRequiredService<SupabaseSessionService>(),
                services.GetRequiredService<HttpClient>(), FileSystem.CacheDirectory));
            builder.Services.AddSingleton<IFoodStore, SupabaseFoodStore>();
            builder.Services.AddSingleton<ITrainingStore, SupabaseTrainingStore>();
        }
        else
        {
            builder.Services.AddSingleton<IFoodStore>(services => services.GetRequiredService<LocalFoodStore>());
            builder.Services.AddSingleton<ITrainingStore>(services => services.GetRequiredService<TrainingSyncService>());
        }

        builder.Services.AddSingleton<DataSettingsPage>(services => new(
            services.GetRequiredService<DataProvider>(), services.GetRequiredService<IPreferences>(),
            () => services.GetService<SupabaseSessionService>()));

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
