using System.Net;
using IHLDota2Bot.Auth;

namespace IHLDota2Bot.Tests;

public class SteamOpenIdServiceTests
{
    private static List<KeyValuePair<string, string>> ValidCallbackQuery(string claimedId) =>
        [
            new("openid.ns", "http://specs.openid.net/auth/2.0"),
            new("openid.mode", "id_res"),
            new("openid.claimed_id", claimedId),
            new("openid.identity", claimedId),
            new("openid.sig", "abc123"),
            new("openid.signed", "signed,op_endpoint,claimed_id,identity"),
            new("returnUrl", "http://127.0.0.1:5000/callback"),
        ];

    [Fact]
    public async Task VerifyAsync_SteamConfirmsValid_ReturnsSteamId()
    {
        var stub = new StubHandler("ns:http://specs.openid.net/auth/2.0\nis_valid:true\n");
        var service = new SteamOpenIdService(new HttpClient(stub));

        var steamId = await service.VerifyAsync(
            ValidCallbackQuery("https://steamcommunity.com/openid/id/76561198000000123")
        );

        Assert.Equal("76561198000000123", steamId);
        // mode must have been switched to check_authentication when echoing back to Steam.
        Assert.Contains("openid.mode=check_authentication", stub.LastContent);
    }

    [Fact]
    public async Task VerifyAsync_SteamRejects_ReturnsNull()
    {
        var stub = new StubHandler("ns:http://specs.openid.net/auth/2.0\nis_valid:false\n");
        var service = new SteamOpenIdService(new HttpClient(stub));

        var steamId = await service.VerifyAsync(
            ValidCallbackQuery("https://steamcommunity.com/openid/id/76561198000000123")
        );

        Assert.Null(steamId);
    }

    [Fact]
    public async Task VerifyAsync_ValidButUnparseableClaimedId_ReturnsNull()
    {
        var stub = new StubHandler("is_valid:true\n");
        var service = new SteamOpenIdService(new HttpClient(stub));

        var steamId = await service.VerifyAsync(
            ValidCallbackQuery("https://example.com/not-steam")
        );

        Assert.Null(steamId);
    }

    [Fact]
    public void BuildRedirectUrl_IncludesOpenIdIdentifierSelect()
    {
        var service = new SteamOpenIdService(new HttpClient(new StubHandler("")));

        var url = service.BuildRedirectUrl(
            "http://localhost:5193",
            "http://localhost:5193/api/auth/steam/callback"
        );

        Assert.StartsWith(SteamOpenIdService.SteamLoginUrl, url);
        Assert.Contains("openid.mode=checkid_setup", url);
        Assert.Contains("identifier_select", url);
    }

    private sealed class StubHandler(string responseBody, HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public string? LastContent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            if (request.Content is not null)
                LastContent = await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody) };
        }
    }
}
