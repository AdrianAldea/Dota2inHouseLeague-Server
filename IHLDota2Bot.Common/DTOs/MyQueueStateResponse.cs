namespace IHLDota2Bot.Common.DTOs;

/// <summary>
/// The signed-in player's own queue membership, served by
/// <c>GET /api/gamemanager/me/queue</c>. Lets the desktop client reconcile its
/// optimistic in-queue state with server truth (e.g. after an app restart)
/// without the public queue status having to expose user ids.
/// </summary>
public record MyQueueStateResponse
{
    /// <summary>True when the player is seated in the current lobby.</summary>
    public required bool InQueue { get; init; }

    /// <summary>True when the player is marked ready in the current lobby.</summary>
    public required bool IsReady { get; init; }
}
