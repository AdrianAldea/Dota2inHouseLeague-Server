using System.Net;
using System.Net.Http.Headers;
using IHLDota2Bot.IntegrationTests.TestSupport;

namespace IHLDota2Bot.IntegrationTests;

/// <summary>
/// Verifies the player JWT gate on <c>/api/auth/me</c> and the "act as me" endpoints:
/// the <c>Player</c> policy requires a valid Bearer token regardless of the (anonymous)
/// admin scheme. Legacy id-based reads stay open.
/// </summary>
public class PlayerAuthorizationTests : IAsyncLifetime
{
    private ApiFactory factory = null!;
    private HttpClient client = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory();
        client = factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithGarbageToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MeAction_ReadyWithoutToken_Returns401()
    {
        var response = await client.PostAsync("/api/gamemanager/ready", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LegacyQueueStatus_RemainsOpen_Returns200()
    {
        // The public, id-free read must stay accessible without any auth.
        var response = await client.GetAsync("/api/gamemanager/queue/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
