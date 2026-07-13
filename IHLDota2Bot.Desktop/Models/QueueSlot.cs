namespace IHLDota2Bot.Desktop.Models;

/// <summary>
/// One slot in the live lobby grid. Either filled by a player (with a ready flag)
/// or an open seat. Display-only; Phase 1 maps these from <c>QueueStatusResponse</c>.
/// </summary>
public sealed class QueueSlot
{
    public string? Name { get; init; }
    public bool IsReady { get; init; }
    public bool IsEmpty => string.IsNullOrEmpty(Name);
    public string Initial => IsEmpty ? "" : Name![..1].ToUpperInvariant();
    public string StatusText =>
        IsEmpty ? "Open"
        : IsReady ? "Ready"
        : "Not ready";
}
