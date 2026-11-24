using MomentumGroves.Mobile.Configuration;
using MomentumGroves.Mobile.Data;
using MomentumGroves.Mobile.Data.Supabase;
using MomentumGroves.Mobile.Infrastructure.Supabase;
using MomentumGroves.Mobile.Service.Supabase;
using MomentumGroves.Mobile.Tests.Infrastructure;
using Supabase.Gotrue;

namespace MomentumGroves.Mobile.Tests.Data;

public sealed class SupabaseProviderTests
{
    private static readonly SupabaseConfiguration Config =
        SupabaseConfiguration.Parse("https://momentum.invalid", "sb_publishable_example");

    private const string SignedIn = """
        {"access_token":"header.eyJleHAiOjQxMDI0NDQ4MDB9.signature","refresh_token":"fake-refresh",
         "token_type":"bearer","expires_in":3600,
         "user":{"id":"11111111-1111-1111-1111-111111111111","email":"owner@example.invalid","email_confirmed_at":"2024-01-01T00:00:00Z"}}
        """;

    [Test]
    public async Task SignedInCloudFoodReadUsesAuthenticatedCustomSchemaAndSecureStorage()
    {
        var secureStorage = new MemorySecureStorage();
        var handler = new ScriptedHttpHandler(
            request =>
            {
                Assert.That(request.RequestUri!.PathAndQuery, Is.EqualTo("/auth/v1/token?grant_type=password"));
                return ScriptedHttpHandler.Json(SignedIn);
            },
            request =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo("/rest/v1/rpc/mobile_foods"));
                    Assert.That(request.Headers.GetValues("Content-Profile").Single(), Is.EqualTo("momentum_groves"));
                    Assert.That(request.Headers.Authorization!.Parameter, Does.EndWith(".signature"));
                });
                return ScriptedHttpHandler.Json("""
                    [{"id":4294967296,"version":"test","name":"Oats","unit":"g","kcal":370,
                      "protein":13,"carbs":60,"fat":7,"fiber":null}]
                    """);
            });
        using var http = new HttpClient(handler);
        var persistence = new SupabaseSessionStorage(secureStorage, Config);
        var session = new SupabaseSessionService(Config, persistence, http);
        await session.SignInAsync("owner@example.invalid", "fake-password");

        var item = (await new SupabaseFoodStore(session).GetFoodsAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(item.CloudId, Is.EqualTo(4294967296L));
            Assert.That(item.Name, Is.EqualTo("Oats"));
            Assert.That(secureStorage.Count, Is.EqualTo(1));
            Assert.That(handler.Remaining, Is.Zero);
        });
        Assert.That((await persistence.LoadSessionAsync())?.RefreshToken, Is.EqualTo("fake-refresh"));
    }

    [Test]
    public void SignedOutCloudTrainingReadDoesNotSendADataApiRequest()
    {
        using var http = new HttpClient(new ScriptedHttpHandler());
        var session = new SupabaseSessionService(Config, new SupabaseSessionStorage(new MemorySecureStorage(), Config), http);

        var error = Assert.ThrowsAsync<DataStoreException>(() => new SupabaseTrainingStore(session).ReadAsync());

        Assert.That(error!.Message, Does.Contain("Sign in"));
    }
}
