using System;
using IHLDota2Bot.Common.DTOs;

namespace IHLDota2Bot.Desktop.Services;

/// <summary>
/// Current signed-in player state, shared app-wide. Pure state holder (no API
/// dependency) so the <see cref="AuthHeaderHandler"/> can read the token without a
/// dependency cycle. Raises <see cref="Changed"/> on every mutation so the root
/// view-model can re-evaluate which screen to show.
/// </summary>
public sealed class AppSession
{
    public string? Token { get; private set; }
    public MeResponse? Me { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);
    public bool IsRegistered => Me?.Registered == true;

    public event EventHandler? Changed;

    public void SetToken(string token)
    {
        Token = token;
        Raise();
    }

    public void SetMe(MeResponse? me)
    {
        Me = me;
        Raise();
    }

    public void Clear()
    {
        Token = null;
        Me = null;
        Raise();
    }

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}
