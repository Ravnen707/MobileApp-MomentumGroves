using MomentumGroves.Mobile.Configuration;
using MomentumGroves.Mobile.Data;
using MomentumGroves.Mobile.Infrastructure.Supabase;
using Supabase.Gotrue.Exceptions;
using static Supabase.Gotrue.Constants;
using Supabase.Postgrest.Exceptions;

namespace MomentumGroves.Mobile.Service.Supabase;

internal sealed class SupabaseSessionService(
    SupabaseConfiguration configuration, SupabaseSessionStorage persistence, HttpClient http)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private global::Supabase.Client? _client;

    public Task<string?> AccountAsync() => UseAsync(client =>
        Task.FromResult(client.Auth.CurrentSession?.User?.Email));

    public Task SignInAsync(string email, string password) =>
        UseAsync(async client =>
        {
            if (await client.Auth.SignIn(email.Trim(), password) is null)
                throw new DataStoreException("Supabase did not return a session.");
            return true;
        });

    public async Task SignOutAsync()
    {
        await _gate.WaitAsync();
        try
        {
            try
            {
                if (_client?.Auth.CurrentSession is not null)
                    await _client.Auth.SignOut(SignOutScope.Local);
            }
            catch (Exception exception) when (exception is GotrueException or HttpRequestException or OperationCanceledException)
            {
                throw new DataStoreException("Signed out on this phone. Supabase could not revoke the remote session.");
            }
            finally
            {
                _client?.Auth.StopAutoRefresh();
                _client = null;
                await persistence.DestroySessionAsync();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<T> RpcAsync<T>(string name, object parameters) where T : class =>
        UseAsync(async client =>
        {
            if (client.Auth.CurrentSession is null)
                throw new DataStoreException("Sign in to Supabase in Settings.");
            var result = await client.Rpc<T>(name, parameters);
            return result ?? throw new DataStoreException("Supabase returned an empty response.");
        });

    private async Task<T> UseAsync<T>(Func<global::Supabase.Client, Task<T>> action)
    {
        await _gate.WaitAsync();
        try
        {
            if (_client is null)
            {
                var client = new global::Supabase.Client(configuration.Url, configuration.Key, new global::Supabase.SupabaseOptions
                {
                    Schema = "momentum_groves",
                    HttpClient = http,
                    SessionHandler = persistence,
                    AutoRefreshToken = true,
                    AutoConnectRealtime = false
                });
                await client.InitializeAsync();
                _client = client;
            }

            return await action(_client);
        }
        catch (GotrueException exception)
        {
            if (exception.ErrorCode is "refresh_token_not_found" or "refresh_token_already_used" or "session_not_found")
            {
                _client?.Auth.StopAutoRefresh();
                _client = null;
                await persistence.DestroySessionAsync();
                throw new DataStoreException("Your Supabase session expired. Sign in again.");
            }

            throw new DataStoreException(exception.StatusCode is 400 or 401 or 403
                ? "Supabase rejected the sign-in. Check your account details."
                : "Supabase authentication is unavailable. Try again.");
        }
        catch (PostgrestException exception)
        {
            throw new DataStoreException(exception.StatusCode switch
            {
                401 => "Supabase requires a valid session. Review your sign-in in Settings.",
                403 => "This account cannot access Momentum Groves in Supabase.",
                _ => "Supabase could not complete the request. Check the configuration or try again."
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            throw new DataStoreException("Supabase is unreachable. Check your connection and try again.");
        }
        finally
        {
            _gate.Release();
        }
    }
}
