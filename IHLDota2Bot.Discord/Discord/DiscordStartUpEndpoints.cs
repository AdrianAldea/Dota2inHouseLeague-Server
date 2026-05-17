using Microsoft.AspNetCore.Http.HttpResults;

namespace IHLDota2Bot.Discord.Discord;

public static class DiscordStartUpEndpoints
{
    public static void MapDiscordStartUpEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/start", StartServer);
        group.MapPost("/stop", StopServer);
    }

    private static async Task<Results<Ok<bool>, StatusCodeHttpResult>> StartServer(
        Services.DiscordService service,
        CancellationToken cancellationToken
    )
    {
        var resultawait = await service.StartAsync(cancellationToken);

        return TypedResults.Ok(resultawait);
    }

    private static async Task<Results<Ok, StatusCodeHttpResult>> StopServer(
        Services.DiscordService service,
        CancellationToken cancellationToken
    )
    {
        await service.StopAsync(cancellationToken);

        return TypedResults.Ok();
    }
}