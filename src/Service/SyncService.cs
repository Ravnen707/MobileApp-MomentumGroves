using System.Net.Http.Json;
using MomentumGroves.Mobile.DTO;

namespace MomentumGroves.Mobile.Service;

/// <summary>Transfers pending foods and removes only desktop-acknowledged local entries.</summary>
public sealed class SyncService
{
    private readonly LocalFoodService _storage;
    private readonly HttpClient _client = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(60) };
    private const string AddressKey = "pc_sync_address";
    private const string SecretKey = "pc_sync_secret";

    public SyncService(LocalFoodService storage)
    {
        _storage = storage;
    }

    public string Address
    {
        get => Preferences.Get(AddressKey, "");
        set => Preferences.Set(AddressKey, value.Trim());
    }

    public async Task<string> GetSecretAsync()
    {
        var secret = await SecureStorage.Default.GetAsync(SecretKey);
        if (secret is not null)
        {
            return secret;
        }

        secret = Preferences.Get(SecretKey, "");
        if (secret.Length > 0)
        {
            await SaveSecretAsync(secret);
        }

        return secret;
    }

    public async Task SaveSecretAsync(string secret)
    {
        await SecureStorage.Default.SetAsync(SecretKey, secret.Trim());
        Preferences.Remove(SecretKey);
    }

    public static bool IsHttpsAddress(string? address) =>
        Uri.TryCreate(address?.Trim(), UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0 && uri.UserInfo.Length == 0 &&
        uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0;

    public async Task<(int sent, int imported, int skipped)> TransferAsync(CancellationToken token = default)
    {
        var foods = await _storage.GetFoodsAsync();
        if (foods.Count == 0)
        {
            return (0, 0, 0);
        }

        var address = Address.Trim().TrimEnd('/');
        var secret = await GetSecretAsync();
        if (!IsHttpsAddress(address) || secret.Length < 16)
        {
            throw new InvalidOperationException("Enter your PC's HTTPS address and a sync key of at least 16 characters.");
        }

        var imported = 0;
        var skipped = 0;
        foreach (var batch in foods.Chunk(100))
        {
            var payload = batch.Select(food => new FoodSyncDto
            {
                Id = food.Id, Name = food.Name, Unit = food.Unit, Kcal = food.Kcal,
                Protein = food.Protein, Carbs = food.Carbs, Fat = food.Fat, Fiber = food.Fiber
            }).ToList();
            using var request = new HttpRequestMessage(HttpMethod.Post, address + "/api/mobile/foods/import")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Add("X-Momentum-Sync-Key", secret);
            using var response = await _client.SendAsync(request, token);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"The PC rejected the transfer (HTTP {(int)response.StatusCode}).", null, response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<SyncResult>(token);
            if (result?.AcknowledgedIds == null || result.AcknowledgedIds.Count != batch.Length ||
                !result.AcknowledgedIds.ToHashSet().SetEquals(batch.Select(food => food.Id)))
            {
                throw new InvalidOperationException(
                    "The PC didn't confirm every food in this batch. The batch is still saved on this phone.");
            }

            // The server commits the batch before acknowledging it. Retrying is safe.
            await _storage.DeleteBatchAsync(result.AcknowledgedIds);

            imported += result.Imported;
            skipped += result.Skipped;
        }

        return (foods.Count, imported, skipped);
    }

    private sealed class SyncResult
    {
        public int Imported { get; set; }
        public int Skipped { get; set; }
        public List<Guid>? AcknowledgedIds { get; set; }
    }
}
