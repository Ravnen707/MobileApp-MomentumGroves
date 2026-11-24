using MomentumGroves.Mobile.Configuration;

namespace MomentumGroves.Mobile.Views;

public sealed class HomePage : ContentPage
{
    public HomePage(MainPage food, OfflineTrainingPage training, PcSyncPage syncPage, DataSettingsPage settings, DataProvider provider)
    {
        Title = "Momentum Groves";
        BackgroundColor = Color.FromArgb("#101417");
        var content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 14,
            Children =
            {
                new Image
                {
                    Source = "momentum_groves_logo.png",
                    Aspect = Aspect.AspectFit,
                    HeightRequest = 200,
                    HorizontalOptions = LayoutOptions.Fill
                }
            }
        };
        var buttons = new List<Button>();
        void EnableButtons(bool enabled)
        {
            foreach (var button in buttons)
                button.IsEnabled = enabled;
        }

        var pages = new List<(string Label, Page Page)> { ("Training", training), ("Food", food) };
        pages.Add((provider.IsCloud ? "PC settings" : "Sync to PC", syncPage));
        pages.Add(("Settings", settings));
        foreach (var (label, page) in pages)
        {
            var button = new Button { Text = label, BackgroundColor = Color.FromArgb(page == syncPage ? "#a6da7e" : "#27312b"), TextColor = page == syncPage ? Color.FromArgb("#101417") : Colors.White };
            buttons.Add(button);
            button.Clicked += async (_, _) =>
            {
                EnableButtons(false);
                try
                {
                    if (Navigation.NavigationStack.LastOrDefault() == this)
                        await Navigation.PushAsync(page);
                }
                finally
                {
                    EnableButtons(true);
                }
            };
            content.Children.Add(button);
        }

        Content = content;
    }
}
