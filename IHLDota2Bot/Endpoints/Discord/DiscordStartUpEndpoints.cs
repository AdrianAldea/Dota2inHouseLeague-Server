namespace IHLDota2Bot.Endpoints.Discord;

using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

public static class DiscordStartUpEndpoints
{
    public static void MapDiscordStartUpEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/start-server", StartServer);
    }

    private static async Task<Results<Ok<bool>, StatusCodeHttpResult>> StartServer(
        DiscordService discordService,
        [FromBody] DiscordStartUpRequest request,
        CancellationToken cancellationToken
    )
    {
        await discordService.StartServer(new DiscordStartUpRequest{  });

        return TypedResults.Ok(true);
    }
}