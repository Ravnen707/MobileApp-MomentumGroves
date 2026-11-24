using System.Net;
using System.Text;

namespace MomentumGroves.Mobile.Tests.Infrastructure;

internal sealed class ScriptedHttpHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] steps) : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _steps = new(steps);
    public int Remaining => _steps.Count;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!_steps.TryDequeue(out var reply))
            throw new InvalidOperationException("Unexpected HTTP request.");
        return Task.FromResult(reply(request));
    }

    public static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}
