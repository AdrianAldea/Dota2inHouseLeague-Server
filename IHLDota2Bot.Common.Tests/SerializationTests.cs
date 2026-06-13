using System.Text.Json;
using IHLDota2Bot.Common.DTOs;

namespace IHLDota2Bot.Common.Tests;

/// <summary>
/// The DTOs are the HTTP contract between the API, the Discord bot and the web UI.
/// These tests confirm they survive a System.Text.Json round-trip with the same
/// (web) options the apps use — covering required members, nullable fields, enums
/// and nested collections.
/// </summary>
public class SerializationTests
{
    private static readonly JsonSerializerOptions Options = JsonSerializerOptions.Web;

    [Fact]
    public void MatchSummary_RoundTripsAllFields()
    {
        var original = new MatchSummary
        {
            Id = Guid.NewGuid(),
            LobbyId = "lobby-1",
            CreatedAt = new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 6, 13, 10, 45, 0, DateTimeKind.Utc),
            Status = MatchStatus.Completed,
            WinningTeam = MatchTeam.Dire,
            Players =
            [
                new MatchPlayerSummary { UserId = 1, Username = "alice", Team = MatchTeam.Radiant, Won = false },
                new MatchPlayerSummary { UserId = 2, Username = "bob", Team = MatchTeam.Dire, Won = true },
            ],
        };

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<MatchSummary>(json, Options);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized!.Id);
        Assert.Equal("lobby-1", deserialized.LobbyId);
        Assert.Equal(MatchStatus.Completed, deserialized.Status);
        Assert.Equal(MatchTeam.Dire, deserialized.WinningTeam);
        Assert.Equal(original.CompletedAt, deserialized.CompletedAt);
        Assert.Equal(2, deserialized.Players.Count);
        Assert.Equal("bob", deserialized.Players[1].Username);
        Assert.True(deserialized.Players[1].Won);
    }

    [Fact]
    public void MatchSummary_RoundTripsNullableFieldsForAnUnfinishedMatch()
    {
        var original = new MatchSummary
        {
            Id = Guid.NewGuid(),
            LobbyId = null,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = null,
            Status = MatchStatus.InProgress,
            WinningTeam = null,
            Players = [],
        };

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<MatchSummary>(json, Options);

        Assert.NotNull(deserialized);
        Assert.Null(deserialized!.LobbyId);
        Assert.Null(deserialized.CompletedAt);
        Assert.Null(deserialized.WinningTeam);
        Assert.Empty(deserialized.Players);
    }

    [Fact]
    public void QueueOperationResponse_IsValueEqualByContent()
    {
        var a = new QueueOperationResponse { Success = true, Message = "joined" };
        var b = new QueueOperationResponse { Success = true, Message = "joined" };
        var c = new QueueOperationResponse { Success = false, Message = "joined" };

        Assert.Equal(a, b);          // records compare by value
        Assert.NotEqual(a, c);
    }
}
