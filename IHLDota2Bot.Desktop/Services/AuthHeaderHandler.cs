using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace IHLDota2Bot.Desktop.Services;

/// <summary>
/// Attaches the player's Bearer token (when signed in) to every outgoing API request,
/// so the <see cref="ApiClient"/> never has to thread the token through each call.
/// When a request that carried a token comes back 401 the token is dead (expired or
/// rotated server-side) — the saved copy is dropped and the session cleared, which
/// sends the UI back to the login screen instead of surfacing endless generic errors.
/// </summary>
public sealed class AuthHeaderHandler(AppSession session, TokenStore store) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var hadToken = !string.IsNullOrEmpty(session.Token);
        if (hadToken)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);

        var response = await base.SendAsync(request, cancellationToken);

        if (hadToken && response.StatusCode == HttpStatusCode.Unauthorized)
        {
            store.Clear();
            session.Clear();
        }

        return response;
    }
}
