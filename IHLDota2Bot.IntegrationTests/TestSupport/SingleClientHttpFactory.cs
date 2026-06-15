namespace IHLDota2Bot.IntegrationTests.TestSupport;

/// <summary>
/// Minimal <see cref="IHttpClientFactory"/> that always hands back one supplied
/// <see cref="HttpClient"/>. Used to point the Discord <c>SteamClient</c> at the
/// live Game Manager TestServer so the two projects are exercised end-to-end over
/// real HTTP (and the shared Common DTOs over the real serialization path).
/// </summary>
internal sealed class SingleClientHttpFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}
