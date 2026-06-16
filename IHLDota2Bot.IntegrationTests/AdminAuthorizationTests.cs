using IHLDota2Bot.Auth;
using IHLDota2Bot.Clients;
using IHLDota2Bot.Data;
using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IHLDota2Bot.IntegrationTests;

/// <summary>
/// Verifies the server-side admin API-key gate on <c>/api/gamemanager/admin/*</c>.
/// </summary>
public class AdminAuthorizationTests : IAsyncLifetime
{
    private const string CorrectKey = "test-admin-key-abc123";

    private ApiFactoryWithKey factory = null!;
    private HttpClient client = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactoryWithKey(CorrectKey);
        client = factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task AdminEndpoint_WithCorrectKey_Returns200()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/gamemanager/admin/pool_status");
        request.Headers.Add(AdminApiKeyAuthenticationHandler.HeaderName, CorrectKey);

        var response = await client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_WithWrongKey_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/gamemanager/admin/pool_status");
        request.Headers.Add(AdminApiKeyAuthenticationHandler.HeaderName, "wrong-key");

        var response = await client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_WithNoKey_Returns401()
    {
        var response = await client.GetAsync("/api/gamemanager/admin/pool_status");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NonAdminEndpoint_WithNoKey_Returns200()
    {
        // Non-admin endpoints must remain accessible without any key.
        var response = await client.GetAsync("/api/gamemanager/queue/status");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Variant of <see cref="TestSupport.ApiFactory"/> that configures a non-empty
    /// <c>AdminApiKey</c> so the auth gate is active during the test run.
    /// </summary>
    private sealed class ApiFactoryWithKey(string apiKey) : WebApplicationFactory<Dota2Service>
    {
        private readonly InMemoryDatabaseRoot dbRoot = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureServices(services =>
            {
                var efDescriptors = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions)
                        || (d.ServiceType.IsGenericType
                            && d.ServiceType.GetGenericArguments().Contains(typeof(AppDbContext))))
                    .ToList();
                foreach (var d in efDescriptors)
                    services.Remove(d);

                services.AddDbContextFactory<AppDbContext>(o => o.UseInMemoryDatabase("ihl-itests-auth", dbRoot));

                services
                    .AddHttpClient(DiscordClient.HttpClientName, c => c.BaseAddress = new Uri("http://localhost/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new TestSupport.StubOkHandler());
            });

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GameManager:AdminApiKey"] = apiKey
                });
            });
        }
    }
}
