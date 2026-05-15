namespace Dota2Bot.Endpoints.GameManager;

public class InviteRequest
{
    public IList<ulong> PlayersIds { get; set; } = [];
}