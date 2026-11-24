using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MomentumGroves.Mobile.Configuration;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace MomentumGroves.Mobile.Infrastructure.Supabase;

internal sealed class SupabaseSessionStorage(ISecureStorage storage, SupabaseConfiguration configuration)
    : IGotrueSessionPersistence<Session>
{
    private readonly string _key = "supabase.session." +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(configuration.Url)));

    // The installed client supports asynchronous persistence, including session refresh.
    public void SaveSession(Session session) => throw new NotSupportedException();
    public void DestroySession() => throw new NotSupportedException();
    public Session? LoadSession() => throw new NotSupportedException();

    public async Task SaveSessionAsync(Session session, CancellationToken cancellationToken = default) =>
        await storage.SetAsync(_key, JsonSerializer.Serialize(session));

    public Task DestroySessionAsync(CancellationToken cancellationToken = default)
    {
        storage.Remove(_key);
        return Task.CompletedTask;
    }

    public async Task<Session?> LoadSessionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = await storage.GetAsync(_key);
            return payload is null ? null : JsonSerializer.Deserialize<Session>(payload);
        }
        catch (JsonException)
        {
            await DestroySessionAsync(cancellationToken);
            return null;
        }
    }
}
