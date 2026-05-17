using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace IHLDota2Bot.Endpoints.Discord;

public static class DiscordStartUpEndpoints
{
    public static void MapDiscordStartUpEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/startdiscord", StartServer);
    }

    private static async Task<Results<Ok<bool>, StatusCodeHttpResult>> StartServer(
        GameManagerService service,
        [FromBody] DiscordStartUpRequest request,
        CancellationToken cancellationToken
    )
    {
        await service.ConnectToDiscord(new DiscordStartUpRequest { });

        return TypedResults.Ok(true);
    }
}