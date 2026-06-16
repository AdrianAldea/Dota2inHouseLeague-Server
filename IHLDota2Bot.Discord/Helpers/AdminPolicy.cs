using IHLDota2Bot.Discord.Configuration;

namespace IHLDota2Bot.Discord.Helpers;

/// <summary>
/// Pure admin-authorization rule, decoupled from Discord's socket types so it can
/// be unit-tested directly. A user is an admin if their id is in
/// <see cref="DiscordOptions.AdminUserIds"/>, or (when an admin role is configured)
/// they hold a role whose name matches <see cref="DiscordOptions.AdminRoleName"/>
/// case-insensitively.
/// </summary>
public static class AdminPolicy
{
    public static bool IsAdmin(ulong? userId, IEnumerable<string> userRoleNames, DiscordOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (userId is null) return false;

        if (options.AdminUserIds.Contains(userId.Value)) return true;

        if (!string.IsNullOrWhiteSpace(options.AdminRoleName))
        {
            return userRoleNames.Any(r => string.Equals(r, options.AdminRoleName, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }
}
