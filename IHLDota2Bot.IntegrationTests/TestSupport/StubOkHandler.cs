using System.Net;

namespace IHLDota2Bot.IntegrationTests.TestSupport;

/// <summary>
/// Primary handler for the Discord webhook HttpClient under test: answers every
/// request with 200/empty-JSON immediately. Keeps `DiscordClient.SendMessageAsync`
/// (fire-and-forget in production) from attempting real network/service-discovery
/// calls — which would otherwise add resilience-retry latency and log noise to
/// report/ban/challenge flows.
/// </summary>
internal sealed class StubOkHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    ) =>
        Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            }
        );
}
