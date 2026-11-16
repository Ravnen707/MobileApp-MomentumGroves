using System.Net.Http.Json;
using MomentumGroves.Mobile.DTO;

namespace MomentumGroves.Mobile.Service;

/// <summary>Transfers pending foods and removes only desktop-acknowledged local entries.</summary>
public sealed class SyncService
{
    private readonly LocalFoodService _storage;
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(15) };
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

    public string Secret
    {
        get => Preferences.Get(SecretKey, "");
        set => Preferences.Set(SecretKey, value.Trim());
    }

    public async Task<(int sent, int imported, int skipped)> TransferAsync()
    {
        var address = Address.Trim().TrimEnd('/');
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttp || uri.Port != 5000 || string.IsNullOrWhiteSpace(Secret))
        {
            throw new InvalidOperationException("Indtast PC-adressen (http://IP:5000) og forbindelsesnøglen.");
        }

        var foods = await _storage.GetFoodsAsync();
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
            request.Headers.TryAddWithoutValidation("X-Momentum-Sync-Key", Secret);
            using var response = await _client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"PC'en afviste overførslen (HTTP {(int)response.StatusCode}).");
            }

            var result = await response.Content.ReadFromJsonAsync<SyncResult>();
            if (result?.AcknowledgedIds == null || result.AcknowledgedIds.Count != batch.Length ||
                !result.AcknowledgedIds.ToHashSet().SetEquals(batch.Select(food => food.Id)))
            {
                throw new InvalidOperationException(
                    "PC'en bekræftede ikke alle fødevarer. Intet slettes fra denne overførsel.");
            }

            // The server commits the batch before acknowledging it. Retrying is safe.
            foreach (var id in result.AcknowledgedIds)
            {
                await _storage.DeleteAsync(id);
                Preferences.Remove("pc_sync_done_" + id.ToString("D"));
            }

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