using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace IHLDota2Bot.Desktop.Services;

/// <summary>
/// Attaches the player's Bearer token (when signed in) to every outgoing API request,
/// so the <see cref="ApiClient"/> never has to thread the token through each call.
/// </summary>
public sealed class AuthHeaderHandler(AppSession session) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrEmpty(session.Token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);

        return base.SendAsync(request, cancellationToken);
    }
}
