using System.Net;
using IHLDota2Bot.IntegrationTests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IHLDota2Bot.IntegrationTests;

/// <summary>
/// Verifies the loopback-only <c>returnUrl</c> gate on <c>/api/auth/steam/login</c>
/// and <c>/api/auth/steam/callback</c>. The callback delivers the issued player JWT
/// to <c>returnUrl</c>, so a non-loopback target must be rejected with 400 before
/// any redirect (login) or OpenID verification round-trip (callback) happens.
/// </summary>
public class SteamAuthReturnUrlTests : IAsyncLifetime
{
    private ApiFactory factory = null!;
    private HttpClient client = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory();
        // No auto-redirect: the login endpoint 302s to steamcommunity.com, and we
        // want to inspect that Location header, not follow it out of the TestServer.
        client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task Login_WithLoopbackReturnUrl_RedirectsToSteam()
    {
        var returnUrl = Uri.EscapeDataString("http://127.0.0.1:51234/callback");

        var response = await client.GetAsync($"/api/auth/steam/login?returnUrl={returnUrl}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith(
            "https://steamcommunity.com/openid/login",
            response.Headers.Location.ToString()
        );
    }

    [Theory]
    [InlineData("https://evil.example/steal")]
    [InlineData("http://192.168.1.10:5000/cb")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/relative")]
    public async Task Login_WithNonLoopbackReturnUrl_Returns400(string returnUrl)
    {
        var response = await client.GetAsync(
            $"/api/auth/steam/login?returnUrl={Uri.EscapeDataString(returnUrl)}"
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("https://evil.example/steal")]
    [InlineData("http://localhost.evil.example/cb")]
    public async Task Callback_WithNonLoopbackReturnUrl_Returns400(string returnUrl)
    {
        // Hit the callback directly — an attacker doesn't have to go through /login.
        var response = await client.GetAsync(
            $"/api/auth/steam/callback?returnUrl={Uri.EscapeDataString(returnUrl)}"
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
