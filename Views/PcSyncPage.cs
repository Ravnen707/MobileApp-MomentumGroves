using System.Net;
using MomentumGroves.Mobile.Configuration;
using MomentumGroves.Mobile.Service;

namespace MomentumGroves.Mobile.Views;

public sealed class PcSyncPage : ContentPage
{
    private readonly SyncService _foodSync;
    private readonly TrainingSyncService _trainingSync;
    private readonly Entry _address = new()
    {
        Placeholder = "https://pc.example.com:5443", Keyboard = Keyboard.Url, TextColor = Colors.White
    };
    private readonly Entry _key = new() { IsPassword = true, TextColor = Colors.White };
    private readonly Entry _fingerprint = new() { TextColor = Colors.White, IsSpellCheckEnabled = false };
    private readonly Button _sync = new()
    {
        Text = "Sync now", BackgroundColor = Color.FromArgb("#a6da7e"), TextColor = Color.FromArgb("#101417")
    };
    private readonly Label _status = new() { TextColor = Color.FromArgb("#9ba9ac") };
    private bool _busy;
    private readonly bool _canSync;

    public PcSyncPage(SyncService foodSync, TrainingSyncService trainingSync, DataProvider provider)
    {
        _foodSync = foodSync;
        _trainingSync = trainingSync;
        _canSync = !provider.IsCloud;
        Title = _canSync ? "Sync to PC" : "PC settings";
        if (!_canSync)
            _sync.Text = "Save PC settings";
        _key.IsVisible = _canSync;
        BackgroundColor = Color.FromArgb("#101417");
        SemanticProperties.SetDescription(_address, "PC HTTPS address");
        SemanticProperties.SetDescription(_key, "Sync key");
        SemanticProperties.SetDescription(_fingerprint, "PC certificate SHA-256 fingerprint");
        _sync.Clicked += async (_, _) => await SyncAsync();
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24, Spacing = 12,
                Children =
                {
                    new Label { Text = "PC HTTPS address", TextColor = Colors.White }, _address,
                    new Label { Text = "Sync key", TextColor = Colors.White, IsVisible = _canSync }, _key,
                    new Label { Text = "Certificate fingerprint (for a private certificate)", TextColor = Colors.White },
                    _fingerprint, _sync, _status
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _address.Text = _foodSync.Address;
        _fingerprint.Text = _foodSync.CertificateFingerprint;
        _key.Text = "";
        _status.Text = "";
        if (!_canSync)
            return;

        _key.IsEnabled = _sync.IsEnabled = false;
        try
        {
            _key.Text = await _foodSync.GetSecretAsync();
        }
        catch (Exception)
        {
            _status.Text = "Couldn't read the saved sync key. Enter it again.";
        }
        finally
        {
            _key.IsEnabled = _sync.IsEnabled = true;
        }
    }

    protected override bool OnBackButtonPressed() => _busy || base.OnBackButtonPressed();

    private async Task SyncAsync()
    {
        var address = _address.Text?.Trim() ?? "";
        var key = _key.Text?.Trim() ?? "";
        var fingerprint = SyncService.NormalizeFingerprint(_fingerprint.Text ?? "");
        if (!SyncService.IsHttpsAddress(address) || (_canSync && key.Length < 16))
        {
            _status.Text = _canSync
                ? "Enter your PC's HTTPS address and a sync key of at least 16 characters."
                : "Enter your PC's HTTPS address.";
            return;
        }

        if (fingerprint.Length != 0 && (fingerprint.Length != 64 || !fingerprint.All(Uri.IsHexDigit)))
        {
            _status.Text = "Enter the PC certificate's 64-character SHA-256 fingerprint.";
            return;
        }

        _busy = true;
        _address.IsEnabled = _key.IsEnabled = _fingerprint.IsEnabled = _sync.IsEnabled = false;
        NavigationPage.SetHasBackButton(this, false);
        _status.Text = "Syncing with the PC…";
        try
        {
            if (_canSync)
                await _foodSync.SaveSecretAsync(key);
            _foodSync.Address = address;
            _foodSync.CertificateFingerprint = fingerprint;
            if (!_canSync)
            {
                _status.Text = "Saved. The full editor still requires the PC to be online.";
                return;
            }

            var results = await Task.WhenAll(
                SyncSectionAsync("Food", async () =>
                {
                    var result = await _foodSync.TransferAsync();
                    return result.sent == 0 ? "There were no pending foods."
                        : $"Added {result.imported}. Already on PC: {result.skipped}.";
                }),
                SyncSectionAsync("Training", async () =>
                {
                    var conflicts = await _trainingSync.SyncAsync();
                    return conflicts == 0 ? "Up to date."
                        : $"Synced. Review {conflicts} unresolved weights in Training.";
                }));
            _status.Text = string.Join(Environment.NewLine, results);
        }
        catch (Exception)
        {
            _status.Text = "Couldn't save the sync settings. Pending changes are still saved.";
        }
        finally
        {
            NavigationPage.SetHasBackButton(this, true);
            _address.IsEnabled = _key.IsEnabled = _fingerprint.IsEnabled = _sync.IsEnabled = true;
            _busy = false;
        }
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
            result = "The PC rejected the sync key. Check the key above.";
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            result = "The PC doesn't have this sync endpoint. Check the web app version.";
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            result = "Couldn't connect over HTTPS. Check the address, certificate fingerprint, and PC network.";
        }
        catch (Exception)
        {
            result = "Sync couldn't finish. Pending changes are still saved.";
        }

        return $"{section}: {result}";
    }
}
