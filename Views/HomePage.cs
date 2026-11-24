namespace MomentumGroves.Mobile.Views;

public sealed class HomePage : ContentPage
{
    public HomePage(MainPage food, OfflineTrainingPage training, PcSyncPage syncPage)
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

        foreach (var (label, page) in new (string, Page)[] { ("Training", training), ("Food", food), ("Sync to PC", syncPage) })
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
