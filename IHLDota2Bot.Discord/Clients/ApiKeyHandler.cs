using IHLDota2Bot.Discord.Configuration;
using Microsoft.Extensions.Options;

namespace IHLDota2Bot.Discord.Clients;

/// <summary>
/// Injects the <c>X-Api-Key</c> header on every outgoing Game Manager request when
/// <see cref="SteamClientOptions.ApiKey"/> is non-empty. When the key is empty the
/// request passes through unmodified (dev / unconfigured mode).
/// </summary>
public sealed class ApiKeyHandler(IOptions<SteamClientOptions> options) : DelegatingHandler
{
    private readonly string apiKey = options.Value.ApiKey;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);

        return base.SendAsync(request, cancellationToken);
    }
}
