namespace IHLDota2Bot;

public class SteamAccount
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public AccountState State { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? CurrentLobbyId { get; set; }

    public override string ToString() =>
        $"Account: {Username} | State: {State} | Lobby: {CurrentLobbyId ?? "None"} | Last Used: {LastUsedAt?.ToString("dd/MM/yyyy HH:mm") ?? "Never"}";
}

public enum AccountState
{
    Ready,
    InUse,
    Cooldown,
}
