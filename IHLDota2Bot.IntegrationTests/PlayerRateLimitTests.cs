using System.Net;

namespace IHLDota2Bot.IntegrationTests;

/// <summary>
/// Verifies the opt-in fixed-window rate limit on the player-facing surface: off by
/// default (dev/tests unaffected) and returning 429 past the configured limit.
/// Exercised via <c>/api/auth/steam/login</c> with an invalid returnUrl, which
/// answers a cheap 400 without touching Steam.
/// </summary>
public class PlayerRateLimitTests
{
    private const string Probe = "/api/auth/steam/login?returnUrl=not-a-loopback-url";

    [Fact]
    public async Task Disabled_ByDefault_NeverReturns429()
    {
        await using var factory = new ConfigurableApiFactory([]);
        using var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync(Probe);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Enabled_RequestsPastTheLimit_Return429()
    {
        await using var factory = new ConfigurableApiFactory(
            new Dictionary<string, string?>
            {
                ["PlayerRateLimit:PermitLimit"] = "2",
                ["PlayerRateLimit:WindowSeconds"] = "60",
            }
        );
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Probe)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Probe)).StatusCode);

        var limited = await client.GetAsync(Probe);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }
}
