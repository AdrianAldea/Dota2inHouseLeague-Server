namespace IHLDota2Bot.IntegrationTests.TestSupport;

/// <summary>
/// Base for HTTP-level integration tests. Each test method gets a fresh
/// <see cref="ApiFactory"/> (and therefore an isolated in-memory database) plus a
/// ready <see cref="HttpClient"/> bound to the TestServer.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected ApiFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    public Task InitializeAsync()
    {
        Factory = new ApiFactory();
        Client = Factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }
}
