using MomentumGroves.Mobile.Views;
namespace MomentumGroves.Mobile;

/// <summary>
/// Provides the Android application's entry point.
/// </summary>
public partial class App : Application
{
    private readonly HomePage _homePage;

    /// <summary>
    /// Initializes the application.
    /// </summary>
    public App(HomePage homePage)
    {
        InitializeComponent();
        _homePage = homePage;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new NavigationPage(_homePage));
    }
}
