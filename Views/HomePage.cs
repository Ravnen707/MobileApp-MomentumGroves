using System.Net;
using MomentumGroves.Mobile.Service;

namespace MomentumGroves.Mobile.Views;

public sealed class HomePage : ContentPage
{
    public HomePage(MainPage food, OfflineTrainingPage training, SyncService foodSync, TrainingSyncService trainingSync)
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
        var status = new Label { TextColor = Color.FromArgb("#eef2f1") };
        var sync = new Button
        {
            Text = "Sync to PC", BackgroundColor = Color.FromArgb("#a6da7e"), TextColor = Color.FromArgb("#101417")
        };
        buttons.Add(sync);

        void EnableButtons(bool enabled)
        {
            foreach (var button in buttons)
                button.IsEnabled = enabled;
        }

        foreach (var (label, page) in new (string, Page)[] { ("Training", training), ("Food", food) })
        {
            var button = new Button { Text = label, BackgroundColor = Color.FromArgb("#27312b"), TextColor = Colors.White };
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

        sync.Clicked += async (_, _) =>
        {
            if (!trainingSync.IsConfigured)
            {
                status.Text = "Save your PC's HTTPS address and sync key in Food → Saved foods first.";
                return;
            }

            EnableButtons(false);
            status.Text = "Syncing with the PC…";
            try
            {
                var results = await Task.WhenAll(
                    SyncSectionAsync("Food", async () =>
                    {
                        var result = await foodSync.TransferAsync();
                        return result.sent == 0 ? "There were no pending foods."
                            : $"Added {result.imported}. Already on PC: {result.skipped}.";
                    }),
                    SyncSectionAsync("Training", async () =>
                    {
                        var conflicts = await trainingSync.SyncAsync();
                        return conflicts == 0 ? "Up to date."
                            : $"Synced. Review {conflicts} unresolved weights in Training.";
                    }));
                status.Text = string.Join(Environment.NewLine, results);
            }
            finally
            {
                EnableButtons(true);
            }
        };
        content.Children.Add(sync);
        content.Children.Add(status);
        Content = content;
    }

    private static async Task<string> SyncSectionAsync(string section, Func<Task<string>> sync)
    {
        string result;
        try
        {
            result = await sync();
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            result = "The PC rejected the sync key. Check Food settings.";
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            result = "The PC doesn't have this sync endpoint. Check the web app version.";
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            result = "Couldn't sync with the PC. Pending changes are still saved.";
        }
        catch (Exception)
        {
            result = "Sync couldn't finish. Pending changes are still saved.";
        }

        return $"{section}: {result}";
    }
}
