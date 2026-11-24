using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MomentumGroves.Mobile.Service;

public sealed class TrainingSyncService(SyncService connection)
{
    private readonly SemaphoreSlim _storageGate = new(1, 1);
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private readonly SemaphoreSlim _imageGate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public sealed record TrainingGroup(string Id, string Name, List<Programme> Programmes);
    public sealed record Programme(string Id, string Title, string Subtitle, List<Exercise> Exercises);
    public sealed record Exercise(string Id, string Name, string? Description, string? Type,
        decimal Sets, string? Reps, decimal Kg, decimal Minutes, string? Image);
    public sealed record Programmes(List<TrainingGroup> Groups);
    public sealed record WeightEdit(Guid ChangeId, string ExerciseId, decimal BaseKg, decimal Kg,
        string? Status = null);
    public sealed record TrainingState(Programmes? Programmes, List<WeightEdit> Pending, string? Version = null);
    private sealed record WeightResult(Guid ChangeId, string Status);
    private sealed record ServerResult(Programmes Programmes, List<WeightResult> Results);

    public bool IsConfigured => SyncService.IsHttpsAddress(connection.Address);
    public bool UsesPrivateCertificate => connection.CertificateFingerprint.Length > 0;
    public Uri EditorAddress => new(new Uri(connection.Address), "/Training/Phone");

    public async Task<TrainingState> ReadAsync()
    {
        if (!IsConfigured)
            return new(null, []);

        await _storageGate.WaitAsync();
        try
        {
            return await ReadFileAsync(CachePath(connection.Address));
        }
        finally
        {
            _storageGate.Release();
        }
    }

    public async Task SaveWeightAsync(string exerciseId, decimal kg, decimal baseline)
    {
        var path = CachePath(connection.Address);
        await _storageGate.WaitAsync();
        try
        {
            var state = await ReadFileAsync(path);
            if (state.Programmes?.Groups.SelectMany(group => group.Programmes)
                    .SelectMany(programme => programme.Exercises).Any(exercise => exercise.Id == exerciseId &&
                        exercise.Type != "cardio") != true)
                throw new InvalidOperationException("This exercise is no longer in the saved programme.");
            var pending = state.Pending.Find(edit => edit.ExerciseId == exerciseId);
            var original = pending is { Status: null } ? pending.BaseKg : baseline;
            state.Pending.RemoveAll(edit => edit.ExerciseId == exerciseId);
            // Also record a revert while an earlier edit may still be in flight.
            if (kg != original || pending is { Status: null })
                state.Pending.Add(new(Guid.NewGuid(), exerciseId, original, kg));
            await WriteFileAsync(path, state);
        }
        finally
        {
            _storageGate.Release();
        }
    }

    public async Task DiscardConflictsAsync()
    {
        var path = CachePath(connection.Address);
        await _storageGate.WaitAsync();
        try
        {
            var state = await ReadFileAsync(path);
            state.Pending.RemoveAll(edit => edit.Status is not null);
            await WriteFileAsync(path, state);
        }
        finally
        {
            _storageGate.Release();
        }
    }

    public async Task<int> SyncAsync()
    {
        using var client = connection.CreateClient(TimeSpan.FromSeconds(12));
        while (await SyncBatchAsync(client))
        {
            // The API accepts up to 100 queued edits per request.
        }

        var state = await ReadAsync();
        if (state.Programmes is not null)
        {
            using var download = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try
            {
                await CacheImagesAsync(client, state.Programmes, download.Token);
            }
            catch (OperationCanceledException) when (download.IsCancellationRequested)
            {
            }
            catch (IOException exception)
            {
                System.Diagnostics.Debug.WriteLine(exception.GetType().Name);
            }
        }

        return state.Pending.Count(edit => edit.Status is not null);
    }

    private async Task<bool> SyncBatchAsync(HttpClient client, CancellationToken token = default)
    {
        await _syncGate.WaitAsync(token);
        try
        {
            var address = connection.Address;
            if (!SyncService.IsHttpsAddress(address))
                return false;

            var path = CachePath(address);
            TrainingState snapshot;
            await _storageGate.WaitAsync(token);
            try
            {
                snapshot = await ReadFileAsync(path);
            }
            finally
            {
                _storageGate.Release();
            }

            var sent = snapshot.Pending.Where(edit => edit.Status is null).Take(100).ToList();
            var key = await connection.GetSecretAsync();
            if (key.Length < 16)
                throw new InvalidOperationException("Save your PC address and sync key in Food settings.");

            using var request = new HttpRequestMessage(sent.Count == 0 ? HttpMethod.Get : HttpMethod.Post,
                new Uri(new Uri(address), "/api/mobile/training/sync"));
            if (sent.Count > 0)
                request.Content = JsonContent.Create(sent, options: Json);
            else if (snapshot.Version is not null)
                request.Headers.TryAddWithoutValidation("If-None-Match", snapshot.Version);

            request.Headers.Add("X-Momentum-Sync-Key", key);
            using var response = await client.SendAsync(request, token);
            if (response.StatusCode == System.Net.HttpStatusCode.NotModified && sent.Count == 0)
                return false;

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ServerResult>(Json, token);
            if (result?.Programmes?.Groups is null || result.Results is null ||
                result.Results.Count != sent.Count ||
                !result.Results.Select(item => item.ChangeId).ToHashSet().SetEquals(sent.Select(edit => edit.ChangeId)) ||
                result.Results.Any(item => item.Status is not ("saved" or "missing" or "conflict")))
            {
                throw new InvalidOperationException("The PC returned an incomplete training sync response.");
            }

            await _storageGate.WaitAsync();
            try
            {
                var pending = (await ReadFileAsync(path)).Pending;
                var statuses = result.Results.ToDictionary(item => item.ChangeId, item => item.Status);
                foreach (var edit in sent)
                {
                    var index = pending.FindIndex(item => item.ExerciseId == edit.ExerciseId);
                    if (index < 0)
                        continue;

                    var local = pending[index];
                    var status = statuses[edit.ChangeId];
                    if (local.ChangeId == edit.ChangeId)
                    {
                        if (status == "saved")
                            pending.RemoveAt(index);
                        else
                            pending[index] = local with { Status = status };
                    }
                    else if (status == "saved")
                    {
                        pending[index] = local with { BaseKg = edit.Kg };
                    }
                }

                await WriteFileAsync(path, new(result.Programmes, pending, response.Headers.ETag?.ToString()));
                return pending.Any(edit => edit.Status is null);
            }
            finally
            {
                _storageGate.Release();
            }
        }
        finally
        {
            _syncGate.Release();
        }
    }

    public string? CachedImage(string? image)
    {
        var uri = ImageAddress(connection.Address, image);
        if (uri is null)
            return null;

        var path = ImagePath(uri);
        return File.Exists(path) ? path : null;
    }

    private async Task CacheImagesAsync(HttpClient client, Programmes programmes, CancellationToken token)
    {
        await _imageGate.WaitAsync(token);
        try
        {
            var address = connection.Address;
            var images = programmes.Groups.SelectMany(group => group.Programmes)
                .SelectMany(programme => programme.Exercises)
                .Select(exercise => ImageAddress(address, exercise.Image)).OfType<Uri>().Distinct();
            foreach (var image in images)
            {
                token.ThrowIfCancellationRequested();
                var path = ImagePath(image);
                if (File.Exists(path))
                    continue;

                var temporary = path + ".tmp";
                try
                {
                    using var response = await client.GetAsync(image, HttpCompletionOption.ResponseHeadersRead, token);
                    if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType is not
                            ("image/jpeg" or "image/png" or "image/webp"))
                        continue;

                    await response.Content.LoadIntoBufferAsync(8_000_000, token);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await using (var stream = File.Create(temporary))
                        await response.Content.CopyToAsync(stream, token);
                    File.Move(temporary, path, overwrite: true);
                }
                catch (HttpRequestException)
                {
                    // Pictures are optional. The programme and weight queue are already saved.
                    break;
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    break;
                }
                finally
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
            }

        }
        finally
        {
            _imageGate.Release();
        }
    }

    private static Uri? ImageAddress(string address, string? image)
    {
        if (!SyncService.IsHttpsAddress(address) || image is null ||
            !image.StartsWith("/TrainingImages/", StringComparison.Ordinal))
            return null;

        return Uri.TryCreate(new Uri(address), image, out var uri) &&
            uri.AbsolutePath.StartsWith("/TrainingImages/", StringComparison.Ordinal) &&
            Path.GetExtension(uri.AbsolutePath).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp"
            ? uri : null;
    }

    private static string ImagePath(Uri uri) => Path.Combine(FileSystem.CacheDirectory, "training-images",
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri))) + Path.GetExtension(uri.AbsolutePath));

    private static string CachePath(string address)
    {
        var host = new Uri(address).GetLeftPart(UriPartial.Authority);
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(host)));
        return Path.Combine(FileSystem.AppDataDirectory, $"training-{id}.json");
    }

    private static async Task<TrainingState> ReadFileAsync(string path)
    {
        if (!File.Exists(path))
            return new(null, []);

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<TrainingState>(stream, Json) ?? new(null, []);
    }

    private static async Task WriteFileAsync(string path, TrainingState state)
    {
        var temporary = path + ".tmp";
        await using (var stream = File.Create(temporary))
            await JsonSerializer.SerializeAsync(stream, state, Json);
        File.Move(temporary, path, overwrite: true);
    }
}
