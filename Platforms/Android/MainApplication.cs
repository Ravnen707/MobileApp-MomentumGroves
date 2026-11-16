using Android.App;
using Android.Runtime;
using Microsoft.Maui;

namespace MomentumGroves.Mobile;

/// <summary>
/// Initializes the Android host.
/// </summary>
[Application]
public class MainApplication : MauiApplication
{
    /// <summary>
    /// Initializes the Android host.
    /// </summary>
    public MainApplication(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership)
    {
    }

    /// <summary>
    /// Creates the MAUI application.
    /// </summary>
    /// <returns>The application.</returns>
    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }
}
