using IHLDota2Bot.Discord.Helpers;

namespace IHLDota2Bot.Discord.Tests;

/// <summary>
/// Backs the <c>!help &lt;command&gt;</c> lookup. Verifies known commands resolve
/// (case-insensitively, including aliases), and unknown commands fail cleanly.
/// </summary>
public class CommandHelpTextTests
{
    [Theory]
    [InlineData("register")]
    [InlineData("queue")]
    [InlineData("q")] // alias
    [InlineData("report")]
    [InlineData("ban")]
    [InlineData("pool_status")]
    public void TryGet_KnownCommand_ReturnsDetail(string command)
    {
        var found = CommandHelpText.TryGet(command, out var detail);

        Assert.True(found);
        Assert.False(string.IsNullOrWhiteSpace(detail));
    }

    [Theory]
    [InlineData("QUEUE")]
    [InlineData("Queue")]
    [InlineData("ReGiStEr")]
    public void TryGet_IsCaseInsensitive(string command)
    {
        Assert.True(CommandHelpText.TryGet(command, out var detail));
        Assert.False(string.IsNullOrWhiteSpace(detail));
    }

    [Theory]
    [InlineData("not-a-command")]
    [InlineData("")]
    [InlineData("queueueue")]
    public void TryGet_UnknownCommand_ReturnsFalseWithNullDetail(string command)
    {
        var found = CommandHelpText.TryGet(command, out var detail);

        Assert.False(found);
        Assert.Null(detail);
    }
}
