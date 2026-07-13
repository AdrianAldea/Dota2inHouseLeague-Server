using System.IdentityModel.Tokens.Jwt;
using System.Text;
using IHLDota2Bot.Auth;
using IHLDota2Bot.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IHLDota2Bot.Tests;

public class PlayerTokenServiceTests
{
    private static readonly PlayerJwtOptions Options = new()
    {
        Issuer = "ihl-test",
        Audience = "ihl-test-desktop",
        SigningKey = "this-is-a-test-signing-key-32-bytes!!",
        ExpiryMinutes = 60,
    };

    private static (PlayerTokenService Service, TokenValidationParameters Validation) Build()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.SigningKey));
        var service = new PlayerTokenService(
            Microsoft.Extensions.Options.Options.Create(Options),
            new PlayerSigningKey(key)
        );
        var validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Options.Issuer,
            ValidateAudience = true,
            ValidAudience = Options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        return (service, validation);
    }

    [Fact]
    public void CreateToken_RegisteredPlayer_RoundTripsClaims()
    {
        var (service, validation) = Build();
        var token = service.CreateToken(
            userId: 76561198000000001,
            steamId: "76561198000000001",
            username: "Dendi",
            registered: true
        );

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear(); // keep raw claim names (sub, steamId, …)
        var principal = handler.ValidateToken(token, validation, out _);

        Assert.Equal("76561198000000001", principal.FindFirst(PlayerClaims.Subject)!.Value);
        Assert.Equal("76561198000000001", principal.FindFirst(PlayerClaims.SteamId)!.Value);
        Assert.Equal("true", principal.FindFirst(PlayerClaims.Registered)!.Value);
        Assert.Equal("Dendi", principal.FindFirst(PlayerClaims.Name)!.Value);
    }

    [Fact]
    public void CreateToken_Unregistered_OmitsNameAndFlagsNotRegistered()
    {
        var (service, validation) = Build();
        var token = service.CreateToken(
            userId: 76561198000000002,
            steamId: "76561198000000002",
            username: null,
            registered: false
        );

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();
        var principal = handler.ValidateToken(token, validation, out _);

        Assert.Equal("false", principal.FindFirst(PlayerClaims.Registered)!.Value);
        Assert.Null(principal.FindFirst(PlayerClaims.Name));
    }

    [Fact]
    public void CreateToken_WrongKey_FailsValidation()
    {
        var (service, _) = Build();
        var token = service.CreateToken(1, "1", "x", true);

        var wrongKeyValidation = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("a-totally-different-signing-key-here!!")
            ),
        };

        Assert.ThrowsAny<SecurityTokenException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(token, wrongKeyValidation, out _)
        );
    }
}
