using Android.App;
using Android.Content.PM;
using Microsoft.Maui;

namespace MomentumGroves.Mobile;

/// <summary>
/// Hosts the application on Android.
/// </summary>
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}