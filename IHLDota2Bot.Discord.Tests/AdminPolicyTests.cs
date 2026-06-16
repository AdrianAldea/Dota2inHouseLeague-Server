using IHLDota2Bot.Discord.Configuration;
using IHLDota2Bot.Discord.Helpers;

namespace IHLDota2Bot.Discord.Tests;

/// <summary>
/// Authorization rule for admin commands. Kept pure (no Discord socket types) so
/// the id-allowlist and role-name matching can be asserted directly.
/// </summary>
public class AdminPolicyTests
{
    [Fact]
    public void NullUser_IsNotAdmin()
    {
        var options = new DiscordOptions { AdminUserIds = [1] };
        Assert.False(AdminPolicy.IsAdmin(null, [], options));
    }

    [Fact]
    public void UserInAdminIds_IsAdmin()
    {
        var options = new DiscordOptions { AdminUserIds = [10, 20] };
        Assert.True(AdminPolicy.IsAdmin(20, [], options));
    }

    [Fact]
    public void UserNotInAdminIds_NoRole_IsNotAdmin()
    {
        var options = new DiscordOptions { AdminUserIds = [10] };
        Assert.False(AdminPolicy.IsAdmin(99, ["Member"], options));
    }

    [Fact]
    public void UserWithMatchingRoleName_IsAdmin()
    {
        var options = new DiscordOptions { AdminRoleName = "Mods" };
        Assert.True(AdminPolicy.IsAdmin(99, ["Member", "Mods"], options));
    }

    [Fact]
    public void RoleMatch_IsCaseInsensitive()
    {
        var options = new DiscordOptions { AdminRoleName = "mods" };
        Assert.True(AdminPolicy.IsAdmin(99, ["MODS"], options));
    }

    [Fact]
    public void RoleConfigured_ButUserLacksIt_IsNotAdmin()
    {
        var options = new DiscordOptions { AdminRoleName = "Mods" };
        Assert.False(AdminPolicy.IsAdmin(99, ["Member"], options));
    }

    [Fact]
    public void NoAdminRoleConfigured_RolesIgnored()
    {
        var options = new DiscordOptions { AdminRoleName = null, AdminUserIds = [1] };
        Assert.False(AdminPolicy.IsAdmin(99, ["Anything"], options));
    }
}
