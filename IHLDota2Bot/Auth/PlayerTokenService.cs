using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IHLDota2Bot.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IHLDota2Bot.Auth;

/// <summary>Holds the symmetric signing key so it is shared by the token issuer and the JwtBearer validator.</summary>
public sealed record PlayerSigningKey(SymmetricSecurityKey Key);

/// <summary>
/// Issues the player JWT used by the desktop client. Claims: <c>sub</c> (league user id),
/// <c>steamId</c>, <c>registered</c>, and optionally <c>name</c>. Signed HMAC-SHA256 with
/// the shared <see cref="PlayerSigningKey"/>.
/// </summary>
public sealed class PlayerTokenService(
    IOptions<PlayerJwtOptions> options,
    PlayerSigningKey signingKey
)
{
    private readonly PlayerJwtOptions _options = options.Value;

    public string CreateToken(ulong userId, string steamId, string? username, bool registered)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(PlayerClaims.Subject, userId.ToString()),
            new(PlayerClaims.SteamId, steamId),
            new(PlayerClaims.Registered, registered ? "true" : "false"),
        };
        if (!string.IsNullOrWhiteSpace(username))
            claims.Add(new Claim(PlayerClaims.Name, username));

        var credentials = new SigningCredentials(signingKey.Key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
