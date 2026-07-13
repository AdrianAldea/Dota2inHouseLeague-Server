using IHLDota2Bot.Endpoints.Auth;

namespace IHLDota2Bot.Tests;

/// <summary>
/// Guards the loopback-only <c>returnUrl</c> policy on the Steam auth endpoints:
/// the callback appends the freshly issued player JWT to this URL, so anything
/// other than the desktop client's RFC 8252 loopback redirect would be an open
/// redirect that exfiltrates the token.
/// </summary>
public class AuthReturnUrlValidationTests
{
    [Theory]
    [InlineData("http://127.0.0.1:51234/callback")]
    [InlineData("http://localhost:5000/cb")]
    [InlineData("http://[::1]:8080/callback")]
    [InlineData("https://127.0.0.1/callback")]
    [InlineData("http://127.0.0.55:7777/x")] // any 127/8 address is loopback
    public void IsAllowedReturnUrl_LoopbackHttpUrl_ReturnsTrue(string url) =>
        Assert.True(AuthEndpoints.IsAllowedReturnUrl(url));

    [Theory]
    [InlineData("https://evil.example/steal")]
    [InlineData("http://192.168.1.10:5000/cb")] // private, but not loopback
    [InlineData("http://localhost.evil.example/cb")] // loopback-lookalike host
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://127.0.0.1/x")] // loopback but not http(s)
    [InlineData("/relative/path")]
    [InlineData("127.0.0.1:5000/cb")] // missing scheme
    [InlineData("")]
    [InlineData(null)]
    public void IsAllowedReturnUrl_NonLoopbackOrNonHttpUrl_ReturnsFalse(string? url) =>
        Assert.False(AuthEndpoints.IsAllowedReturnUrl(url));
}
