using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using MomentumGroves.Mobile.Configuration;
using MomentumGroves.Mobile.Data;
using MomentumGroves.Mobile.Service.Supabase;
using static MomentumGroves.Mobile.DTO.TrainingData;

namespace MomentumGroves.Mobile.Data.Supabase;

internal sealed class SupabaseTrainingImageCache(
    SupabaseConfiguration configuration, SupabaseSessionService session, HttpClient http, string cacheRoot)
{
    private const string ReferencePrefix = "/TrainingImages/";
    private const string Bucket = "momentum-training-images";
    private const int MaxBytes = 8_000_000;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyDictionary<string, string>> RefreshAsync(Programmes programmes)
    {
        var images = programmes.Groups.SelectMany(group => group.Programmes)
            .SelectMany(programme => programme.Exercises).Select(exercise => exercise.Image)
            .OfType<string>().Distinct(StringComparer.Ordinal);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        await _gate.WaitAsync();
        try
        {
            var identity = await session.GetImageSessionAsync();
            var scope = Hash($"{configuration.Url}/{identity.Account}");
            var directory = Path.Combine(cacheRoot, "supabase-training-images", scope);
            foreach (var image in images)
            {
                var name = ObjectName(image);
                if (name is null)
                    continue;

                var path = Path.Combine(directory, Hash(name) + Path.GetExtension(name).ToLowerInvariant());
                if (File.Exists(path) || (!timeout.IsCancellationRequested &&
                    await TryDownloadAsync(name, path, identity.Token, timeout.Token)))
                    paths[image] = path;
            }
        }
        catch (DataStoreException)
        {
            // Image access is optional once the programme has been loaded.
        }
        finally
        {
            _gate.Release();
        }

        return paths;
    }

    private async Task<bool> TryDownloadAsync(string name, string path, string accessToken, CancellationToken token)
    {
        var temporary = path + ".tmp";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{configuration.Url}/storage/v1/object/authenticated/{Bucket}/{Uri.EscapeDataString(name)}");
            request.Headers.Add("apikey", configuration.Key);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!response.IsSuccessStatusCode || mediaType is not ("image/jpeg" or "image/png" or "image/webp"))
                return false;

            await response.Content.LoadIntoBufferAsync(MaxBytes, token);
            var bytes = await response.Content.ReadAsByteArrayAsync(token);
            if (!HasImageHeader(bytes, mediaType))
                return false;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(temporary, bytes, token);
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or
                                           UnauthorizedAccessException or OperationCanceledException)
        {
            return false;
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static bool HasImageHeader(ReadOnlySpan<byte> bytes, string mediaType) => mediaType switch
    {
        "image/jpeg" => bytes.StartsWith(new byte[] { 0xff, 0xd8, 0xff }),
        "image/png" => bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
        "image/webp" => bytes.Length >= 12 && bytes.StartsWith("RIFF"u8) && bytes[8..].StartsWith("WEBP"u8),
        _ => false
    };

    private static string? ObjectName(string reference)
    {
        if (!reference.StartsWith(ReferencePrefix, StringComparison.Ordinal))
            return null;
        var name = Uri.UnescapeDataString(reference[ReferencePrefix.Length..]);
        return name.Length is > 0 and <= 255 && name.IndexOfAny(['/', '\\', '?', '#', ':']) == -1 &&
            !name.Any(char.IsControl) &&
            Path.GetExtension(name).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp" ? name : null;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
