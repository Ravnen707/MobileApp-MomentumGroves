using MomentumGroves.Mobile.Configuration;
using MomentumGroves.Mobile.Data;
using MomentumGroves.Mobile.Service;
using MomentumGroves.Mobile.Service.Supabase;
using static MomentumGroves.Mobile.Configuration.SupabaseConfiguration;

namespace MomentumGroves.Mobile.Views;

public sealed class DataSettingsPage : ContentPage
{
    private readonly Func<SupabaseSessionService?> _session;
    private bool _signedOut;
    private readonly Label _account = new() { TextColor = Colors.White };
    private readonly Label _status = new() { TextColor = Color.FromArgb("#ecd08c") };
    private readonly VerticalStackLayout _actions = new() { Spacing = 12 };

    internal DataSettingsPage(DataProvider provider, IPreferences preferences, Func<SupabaseSessionService?> session)
    {
        _session = session;
        Title = "Settings";
        BackgroundColor = Color.FromArgb("#101417");
        var url = Input("Supabase project URL", preferences.Get(ProjectUrlKey, ""));
        var key = Input("Supabase publishable key", preferences.Get(PublishableKey, ""));
        var email = Input("Email");
        email.Keyboard = Keyboard.Email;
        var password = Input("Password");
        password.IsPassword = true;

        _actions.Children.Add(new Label { Text = $"Active data provider: {provider.Name}", TextColor = Colors.White });
        _actions.Children.Add(new Label
        {
            Text = "The full training editor requires the PC. Cloud training refreshes exercise images from Supabase. Manual PC sync is available in standalone mode.",
            TextColor = Colors.White
        });
        _actions.Children.Add(url);
        _actions.Children.Add(key);
        AddAction("Use Supabase after restart", () =>
        {
            var config = SupabaseConfiguration.Parse(url.Text ?? "", key.Text ?? "");
            if (provider.IsCloud && !string.Equals(config.Url, preferences.Get(ProjectUrlKey, ""), StringComparison.Ordinal)
                && !_signedOut)
                throw new DataStoreException("Sign out before switching Supabase projects.");

            preferences.Set(ProjectUrlKey, config.Url);
            preferences.Set(PublishableKey, config.Key);
            preferences.Set(CloudEnabledKey, true);
            _status.Text = "Force stop the app in Android App info, then reopen it to use these settings.";
            return Task.CompletedTask;
        });
        AddAction("Use standalone after restart", async () =>
        {
            preferences.Set(CloudEnabledKey, false);
            if (provider.IsCloud)
                await SignOutAsync();
            _status.Text = "Force stop the app in Android App info, then reopen it to return to standalone storage.";
        });

        if (provider.IsCloud)
        {
            _actions.Children.Add(_account);
            _actions.Children.Add(email);
            _actions.Children.Add(password);
            AddAction("Sign in", async () =>
            {
                try
                {
                    var auth = _session()!;
                    await auth.SignInAsync(email.Text ?? "", password.Text ?? "");
                    await UpdateAccountAsync();
                    _status.Text = "Signed in. Open Food or Training to refresh shared data.";
                }
                finally
                {
                    password.Text = "";
                }
            });
            AddAction("Sign out", SignOutAsync);
        }

        _actions.Children.Add(_status);
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 20, Children = { _actions } } };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RunAsync(UpdateAccountAsync);
    }

    private async Task UpdateAccountAsync()
    {
        if (_session() is { } auth)
        {
            var email = await auth.AccountAsync();
            _signedOut = email is null;
            _account.Text = email is null ? "Signed out" : $"Signed in as {email}";
        }
    }

    private async Task SignOutAsync()
    {
        try
        {
            await _session()!.SignOutAsync();
        }
        finally
        {
            _signedOut = true;
            _account.Text = "Signed out";
        }
    }

    private void AddAction(string text, Func<Task> action)
    {
        var button = new Button { Text = text };
        button.Clicked += async (_, _) => await RunAsync(action);
        _actions.Children.Add(button);
    }

    private async Task RunAsync(Func<Task> action)
    {
        _actions.IsEnabled = false;
        try
        {
            await action();
        }
        catch (DataStoreException exception)
        {
            _status.Text = exception.Message;
        }
        catch (Exception)
        {
            _status.Text = "Could not complete the settings operation.";
        }
        finally
        {
            _actions.IsEnabled = true;
        }
    }

    private static Entry Input(string label, string value = "") =>
        new() { Placeholder = label, Text = value, TextColor = Colors.White };
}
