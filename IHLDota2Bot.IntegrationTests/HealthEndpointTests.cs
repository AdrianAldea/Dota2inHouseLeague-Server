using System.Net;
using IHLDota2Bot.IntegrationTests.TestSupport;

namespace IHLDota2Bot.IntegrationTests;

/// <summary>
/// Exercises the ServiceDefaults health wiring (<c>MapDefaultEndpoints</c>) inside
/// the real API pipeline. These map only in Development, which the factory sets.
/// </summary>
public class HealthEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Alive_ReturnsHealthy()
    {
        var response = await Client.GetAsync("/alive");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
