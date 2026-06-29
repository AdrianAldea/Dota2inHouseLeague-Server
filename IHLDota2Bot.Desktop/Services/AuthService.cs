using System.Threading;
using System.Threading.Tasks;
using IHLDota2Bot.Common.DTOs;

namespace IHLDota2Bot.Desktop.Services;

/// <summary>
/// Orchestrates the player auth lifecycle: Steam sign-in, registration, auto-login from
/// the saved token, and logout. Updates <see cref="AppSession"/> (which the UI observes)
/// and persists the token via <see cref="TokenStore"/>.
/// </summary>
public sealed class AuthService(ApiClient api, AppSession session, SteamAuthService steam, TokenStore store)
{
    public AppSession Session => session;

    /// <summary>Restores a previous session if the saved token still validates. Returns true if signed in.</summary>
    public async Task<bool> TryAutoLoginAsync()
    {
        var token = store.Load();
        if (string.IsNullOrEmpty(token))
            return false;

        session.SetToken(token);
        var me = await SafeMeAsync();
        if (me is null)
        {
            // Token expired/invalid — drop it so the user lands on the login screen.
            store.Clear();
            session.Clear();
            return false;
        }

        session.SetMe(me);
        return true;
    }

    /// <summary>Runs the full "Sign in through Steam" loopback flow.</summary>
    public async Task<bool> SignInWithSteamAsync(CancellationToken ct = default)
    {
        var result = await steam.SignInAsync(ct);
        if (result is null)
            return false;

        session.SetToken(result.Token);   // set before /me so the header carries it
        session.SetMe(await SafeMeAsync());
        store.Save(result.Token);
        return true;
    }

    /// <summary>Completes registration for a new Steam user, then refreshes the session.</summary>
    public async Task<bool> RegisterAsync(string username)
    {
        var result = await api.RegisterAsync(username);
        if (result is null)
            return false;

        session.SetToken(result.Token);
        store.Save(result.Token);
        session.SetMe(await SafeMeAsync());
        return true;
    }

    public void Logout()
    {
        store.Clear();
        session.Clear();
    }

    private async Task<MeResponse?> SafeMeAsync()
    {
        try
        {
            return await api.GetMeAsync();
        }
        catch
        {
            return null;
        }
    }
}
