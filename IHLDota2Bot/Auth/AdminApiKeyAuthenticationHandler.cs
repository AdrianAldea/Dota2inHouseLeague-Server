using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using IHLDota2Bot.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace IHLDota2Bot.Auth;

/// <summary>
/// Validates the <c>X-Api-Key</c> header on requests to the admin route group.
/// When the configured key is non-empty any request that omits or mismatches it
/// gets a 401; when the key is empty the handler succeeds unconditionally (with a
/// startup warning logged by the admin endpoint registration) so development works
/// without configuring secrets.
/// </summary>
public sealed class AdminApiKeyAuthenticationHandler(
    IOptionsMonitor<AdminApiKeyAuthenticationHandler.SchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<AdminApiKeyOptions> apiKeyOptions
)
    : AuthenticationHandler<AdminApiKeyAuthenticationHandler.SchemeOptions>(
        options,
        loggerFactory,
        encoder
    )
{
    public const string SchemeName = "AdminApiKey";
    public const string HeaderName = "X-Api-Key";

    public sealed class SchemeOptions : AuthenticationSchemeOptions { }

    private readonly string configuredKey = apiKeyOptions.Value.AdminApiKey;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // No key configured — gate is disabled; succeed with an anonymous principal.
        if (string.IsNullOrEmpty(configuredKey))
        {
            var anon = new ClaimsPrincipal(new ClaimsIdentity(SchemeName));
            return Task.FromResult(
                AuthenticateResult.Success(new AuthenticationTicket(anon, SchemeName))
            );
        }

        if (!Request.Headers.TryGetValue(HeaderName, out var value) || value.Count == 0)
            return Task.FromResult(AuthenticateResult.Fail("Missing X-Api-Key header."));

        // Constant-time comparison so response timing can't be used to guess the
        // key byte-by-byte. (FixedTimeEquals short-circuits on length mismatch,
        // which only reveals the key's length — acceptable.)
        var presented = Encoding.UTF8.GetBytes(value[0] ?? string.Empty);
        var expected = Encoding.UTF8.GetBytes(configuredKey);
        if (!CryptographicOperations.FixedTimeEquals(presented, expected))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.Name, "service")], SchemeName)
        );
        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName))
        );
    }
}
