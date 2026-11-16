using MomentumGroves.Mobile.Views;
namespace MomentumGroves.Mobile;

/// <summary>
/// Provides the Android application's entry point.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Initializes the application.
    /// </summary>
    public App(MainPage mainPage)
    {
        InitializeComponent();
        MainPage = new NavigationPage(mainPage);
    }
}
