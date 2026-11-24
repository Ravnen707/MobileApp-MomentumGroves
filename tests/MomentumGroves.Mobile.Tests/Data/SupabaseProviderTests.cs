using System.Net;
using System.Net.Http.Headers;
using static MomentumGroves.Mobile.DTO.TrainingData;
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

    [Test]
    public async Task ImageRefreshDownloadsReplacementsOnceAndDropsClearedReferences()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var handler = new ScriptedHttpHandler(_ => ScriptedHttpHandler.Json(SignedIn),
                request => Picture(request, "first.png"), request => Picture(request, "replacement.png"));
            using var http = new HttpClient(handler);
            var session = new SupabaseSessionService(Config,
                new SupabaseSessionStorage(new MemorySecureStorage(), Config), http);
            var cache = new SupabaseTrainingImageCache(Config, session, http, directory.FullName);
            await session.SignInAsync("owner@example.invalid", "fake-password");

            var first = await cache.RefreshAsync(ProgrammeWith("/TrainingImages/first.png"));
            var repeat = await cache.RefreshAsync(ProgrammeWith("/TrainingImages/first.png"));
            var replacement = await cache.RefreshAsync(ProgrammeWith("/TrainingImages/replacement.png"));
            var cleared = await cache.RefreshAsync(ProgrammeWith(null));

            Assert.Multiple(() =>
            {
                Assert.That(repeat.Values.Single(), Is.EqualTo(first.Values.Single()));
                Assert.That(replacement.Values.Single(), Is.Not.EqualTo(first.Values.Single()));
                Assert.That(cleared, Is.Empty);
                Assert.That(handler.Remaining, Is.Zero);
            });
        }
        finally
        {
            directory.Delete(recursive: true);
        }

        static Programmes ProgrammeWith(string? reference) =>
            new([new("group", "Group", [new("programme", "Programme", "",
                [new("exercise", "Exercise", null, null, 1, "8", 10, 0, reference)])])]);

        static HttpResponseMessage Picture(HttpRequestMessage request, string filename)
        {
            Assert.That(request.RequestUri!.AbsolutePath,
                Is.EqualTo("/storage/v1/object/authenticated/momentum-training-images/" + filename));
            Assert.That(request.Headers.Authorization?.Parameter, Does.EndWith(".signature"));
            var reply = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j6yQAAAAASUVORK5CYII="))
            };
            reply.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return reply;
        }
    }

    [Test]
    public async Task CloudTrainingStillLoadsWithMalformedAndMissingImages()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var handler = new ScriptedHttpHandler(_ => ScriptedHttpHandler.Json(SignedIn),
                _ => ScriptedHttpHandler.Json("""
                    {"groups":[{"id":"g","name":"Group","programmes":[{"id":"p","title":"Programme",
                    "exercises":[{"id":"a","name":"Press","kg":12,"image":{"legacy":true}},
                                 {"id":"b","name":"Row","kg":15,"image":"/TrainingImages/missing.png"}]}]}]}
                    """),
                request =>
                {
                    Assert.That(request.RequestUri!.AbsolutePath, Does.EndWith("/missing.png"));
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                });
            using var http = new HttpClient(handler);
            var session = new SupabaseSessionService(Config,
                new SupabaseSessionStorage(new MemorySecureStorage(), Config), http);
            var cache = new SupabaseTrainingImageCache(Config, session, http, directory.FullName);
            await session.SignInAsync("owner@example.invalid", "fake-password");

            var state = await new SupabaseTrainingStore(session, cache).ReadAsync();
            Assert.That(state.Programmes!.Groups[0].Programmes[0].Exercises.Select(exercise => exercise.Kg),
                Is.EqualTo(new decimal[] { 12, 15 }));
            Assert.That(state.CloudImages, Is.Empty);
            Assert.That(handler.Remaining, Is.Zero);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
