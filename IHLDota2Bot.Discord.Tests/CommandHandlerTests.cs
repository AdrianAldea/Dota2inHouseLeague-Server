using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Discord.Clients;
using IHLDota2Bot.Discord.Commands;
using IHLDota2Bot.Discord.Helpers;
using IHLDota2Bot.Discord.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Moq;

namespace IHLDota2Bot.Discord.Tests;

/// <summary>
/// Command handlers run against a mocked <see cref="ISteamClient"/> and a
/// <see cref="FakeCommandContext"/>. These assert the behavior that matters per
/// command: argument validation, the "reply only on failure" rule for broadcast
/// commands, and the secret-scrub on <c>!add_account</c>.
/// </summary>
public class CommandHandlerTests
{
    private static Mock<ISteamClient> Steam() => new(MockBehavior.Strict);

    // ── Register ──

    [Fact]
    public async Task Register_NoSteamId_RepliesUsage()
    {
        var ctx = new FakeCommandContext { Parts = ["register"], AuthorId = 1, AuthorUsername = "alice" };
        await new RegisterCommandHandler(Steam().Object).HandleAsync(ctx);
        Assert.Equal(CommandMessages.RegisterUsage, ctx.SingleReply());
    }

    [Fact]
    public async Task Register_NonNumericSteamId_RepliesInvalid()
    {
        var ctx = new FakeCommandContext { Parts = ["register", "notanid"], AuthorId = 1, AuthorUsername = "alice" };
        await new RegisterCommandHandler(Steam().Object).HandleAsync(ctx);
        Assert.Equal(CommandMessages.InvalidSteamId, ctx.SingleReply());
    }

    [Fact]
    public async Task Register_Valid_PostsRequestAndConfirms()
    {
        var steam = Steam();
        steam.Setup(s => s.RegisterPlayerAsync(It.IsAny<RegisterPlayerRequest>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new RegisteredPlayer { SteamId = "76561", Username = "alice", UserId = "1" });
        var ctx = new FakeCommandContext { Parts = ["register", "76561"], AuthorId = 1, AuthorUsername = "alice" };

        await new RegisterCommandHandler(steam.Object).HandleAsync(ctx);

        Assert.Contains("alice", ctx.SingleReply());
        Assert.Contains("76561", ctx.Replies[0]);
    }

    [Fact]
    public async Task Register_ApiReturnsNull_RepliesFailure()
    {
        var steam = Steam();
        steam.Setup(s => s.RegisterPlayerAsync(It.IsAny<RegisterPlayerRequest>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((RegisteredPlayer?)null);
        var ctx = new FakeCommandContext { Parts = ["register", "76561"], AuthorId = 1, AuthorUsername = "alice" };

        await new RegisterCommandHandler(steam.Object).HandleAsync(ctx);

        Assert.Equal(CommandMessages.RegisterFailed, ctx.SingleReply());
    }

    // ── Queue (broadcast: silent on success, reply on failure) ──

    [Fact]
    public async Task JoinQueue_Success_StaysSilent()
    {
        var steam = Steam();
        steam.Setup(s => s.JoinQueueAsync(7, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new QueueOperationResponse { Success = true, Message = "joined" });
        var ctx = new FakeCommandContext { Parts = ["queue"], AuthorId = 7 };

        await new JoinQueueCommandHandler(steam.Object).HandleAsync(ctx);

        Assert.Empty(ctx.Replies); // success is announced by the Game Manager broadcast
    }

    [Fact]
    public async Task JoinQueue_Failure_RepliesWithApiMessage()
    {
        var steam = Steam();
        steam.Setup(s => s.JoinQueueAsync(7, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new QueueOperationResponse { Success = false, Message = "You are banned." });
        var ctx = new FakeCommandContext { Parts = ["queue"], AuthorId = 7 };

        await new JoinQueueCommandHandler(steam.Object).HandleAsync(ctx);

        Assert.Equal("You are banned.", ctx.SingleReply());
    }

    [Fact]
    public async Task JoinQueue_NullResponse_RepliesFallback()
    {
        var steam = Steam();
        steam.Setup(s => s.JoinQueueAsync(7, It.IsAny<CancellationToken>()))
             .ReturnsAsync((QueueOperationResponse?)null);
        var ctx = new FakeCommandContext { Parts = ["queue"], AuthorId = 7 };

        await new JoinQueueCommandHandler(steam.Object).HandleAsync(ctx);

        Assert.Equal(CommandMessages.JoinQueueFailed, ctx.SingleReply());
    }

    // ── Report ──

    [Fact]
    public async Task Report_NoWinner_RepliesUsage()
    {
        var ctx = new FakeCommandContext { Parts = ["report"], AuthorId = 1 };
        await new ReportMatchCommandHandler(Steam().Object).HandleAsync(ctx);
        Assert.Equal(CommandMessages.ReportUsage, ctx.SingleReply());
    }

    [Fact]
    public async Task Report_InvalidWinner_RepliesInvalid()
    {
        var ctx = new FakeCommandContext { Parts = ["report", "purple"], AuthorId = 1 };
        await new ReportMatchCommandHandler(Steam().Object).HandleAsync(ctx);
        Assert.Equal(CommandMessages.ReportWinnerInvalid, ctx.SingleReply());
    }

    [Fact]
    public async Task Report_ValidWinner_CallsApiCaseInsensitively()
    {
        var steam = Steam();
        steam.Setup(s => s.ReportMatchAsync(1, MatchTeam.Radiant, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new QueueOperationResponse { Success = true, Message = "ok" });
        var ctx = new FakeCommandContext { Parts = ["report", "RADIANT"], AuthorId = 1 };

        await new ReportMatchCommandHandler(steam.Object).HandleAsync(ctx);

        steam.Verify(s => s.ReportMatchAsync(1, MatchTeam.Radiant, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(ctx.Replies);
    }

    // ── Challenge ──

    [Fact]
    public async Task Challenge_NoOpponent_RepliesUsage()
    {
        var ctx = new FakeCommandContext { Parts = ["challenge"], AuthorId = 1 };
        await new ChallengeCommandHandler(Steam().Object).HandleAsync(ctx);
        Assert.Equal(CommandMessages.ChallengeUsage, ctx.SingleReply());
    }

    [Fact]
    public async Task Challenge_SelfMentionOnly_RepliesUsage()
    {
        // Mentioning only yourself is not a valid opponent.
        var ctx = new FakeCommandContext { Parts = ["challenge"], AuthorId = 1, MentionedUserIds = [1] };
        await new ChallengeCommandHandler(Steam().Object).HandleAsync(ctx);
        Assert.Equal(CommandMessages.ChallengeUsage, ctx.SingleReply());
    }

    [Fact]
    public async Task Challenge_ValidOpponent_CallsApi()
    {
        var steam = Steam();
        steam.Setup(s => s.ChallengePlayerAsync(1, 2, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new ChallengeOperationResponse { Success = true, Message = "sent" });
        var ctx = new FakeCommandContext { Parts = ["challenge"], AuthorId = 1, MentionedUserIds = [2] };

        await new ChallengeCommandHandler(steam.Object).HandleAsync(ctx);

        steam.Verify(s => s.ChallengePlayerAsync(1, 2, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(ctx.Replies);
    }

    // ── Ban (admin) ──

    [Fact]
    public void Ban_RequiresAdmin()
    {
        Assert.True(new BanCommandHandler(Steam().Object).RequiresAdmin);
    }

    [Fact]
    public async Task Ban_NoMention_RepliesUsage()
    {
        var ctx = new FakeCommandContext { Parts = ["ban"], AuthorId = 1 };
        await new BanCommandHandler(Steam().Object).HandleAsync(ctx);
        Assert.Equal(CommandMessages.BanUsage, ctx.SingleReply());
    }

    [Fact]
    public async Task Ban_WithReason_PassesReasonThrough()
    {
        var steam = Steam();
        steam.Setup(s => s.BanPlayerAsync(2, "afk farmer", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new QueueOperationResponse { Success = true, Message = "banned" });
        var ctx = new FakeCommandContext { Parts = ["ban", "<@2>", "afk", "farmer"], AuthorId = 1, MentionedUserIds = [2] };

        await new BanCommandHandler(steam.Object).HandleAsync(ctx);

        steam.Verify(s => s.BanPlayerAsync(2, "afk farmer", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ban_NoReason_PassesNull()
    {
        var steam = Steam();
        steam.Setup(s => s.BanPlayerAsync(2, null, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new QueueOperationResponse { Success = true, Message = "banned" });
        var ctx = new FakeCommandContext { Parts = ["ban", "<@2>"], AuthorId = 1, MentionedUserIds = [2] };

        await new BanCommandHandler(steam.Object).HandleAsync(ctx);

        steam.Verify(s => s.BanPlayerAsync(2, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── AddAccount (admin, scrubs the secret) ──

    [Fact]
    public async Task AddAccount_TooFewArgs_RepliesUsage_DoesNotDelete()
    {
        var ctx = new FakeCommandContext { Parts = ["add_account", "onlyuser"], AuthorId = 1 };
        await new AddAccountCommandHandler(Steam().Object, Mock.Of<ILogger<AddAccountCommandHandler>>()).HandleAsync(ctx);

        Assert.Equal(CommandMessages.AddAccountUsage, ctx.SingleReply());
        Assert.False(ctx.SourceDeleted);
    }

    [Fact]
    public async Task AddAccount_Valid_RepliesAndDeletesSourceMessage()
    {
        var steam = Steam();
        steam.Setup(s => s.AddSteamAccountAsync("bot1", "pw", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new QueueOperationResponse { Success = true, Message = "added bot1" });
        var ctx = new FakeCommandContext { Parts = ["add_account", "bot1", "pw"], AuthorId = 1 };

        await new AddAccountCommandHandler(steam.Object, Mock.Of<ILogger<AddAccountCommandHandler>>()).HandleAsync(ctx);

        Assert.Equal("added bot1", ctx.SingleReply());
        Assert.True(ctx.SourceDeleted); // password message scrubbed from chat
    }

    // ── Help / commands ──

    [Fact]
    public async Task Commands_NoArg_ListsCommands()
    {
        var ctx = new FakeCommandContext { Parts = ["commands"], AuthorId = 1 };
        await new CommandsCommandHandler().HandleAsync(ctx);

        var reply = ctx.SingleReply();
        Assert.Contains("Available commands", reply);
        Assert.Contains("!queue", reply);
    }

    [Fact]
    public async Task Commands_KnownArg_ShowsDetail()
    {
        var ctx = new FakeCommandContext { Parts = ["help", "queue"], AuthorId = 1 };
        await new CommandsCommandHandler().HandleAsync(ctx);

        Assert.Contains("matchmaking queue", ctx.SingleReply());
    }

    [Fact]
    public async Task Commands_UnknownArg_RepliesNoHelp()
    {
        var ctx = new FakeCommandContext { Parts = ["help", "bogus"], AuthorId = 1 };
        await new CommandsCommandHandler().HandleAsync(ctx);

        Assert.Equal(CommandMessages.NoHelpFor("bogus"), ctx.SingleReply());
    }

    // ── MyStats / stats lookup ──

    [Fact]
    public async Task MyStats_NotRegistered_Replies()
    {
        var steam = Steam();
        steam.Setup(s => s.GetPlayerStatsAsync(5, It.IsAny<CancellationToken>()))
             .ReturnsAsync((PlayerStatsResponse?)null);
        var ctx = new FakeCommandContext { Parts = ["mystats"], AuthorId = 5 };

        await new MyStatsCommandHandler(steam.Object).HandleAsync(ctx);

        Assert.Equal(CommandMessages.PlayerNotRegistered, ctx.SingleReply());
    }
}
