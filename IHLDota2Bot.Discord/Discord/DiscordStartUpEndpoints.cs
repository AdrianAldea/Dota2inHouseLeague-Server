using IHLDota2Bot.Common.Requests;
using Microsoft.AspNetCore.Http.HttpResults;

namespace IHLDota2Bot.Discord.Discord;

public static class DiscordStartUpEndpoints
{
    public static void MapDiscordStartUpEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/start", StartServer);
        group.MapPost("/stop", StopServer);
        group.MapPost("/message", HandleMessage);
    }

    private static async Task HandleMessage(
        MessageRequest messageRequest,
        Services.DiscordService service,
        CancellationToken cancellationToken
    )
    {
        await service.SendMessageAsync(messageRequest.Message, cancellationToken);
    }

    private static async Task<Results<Ok<bool>, StatusCodeHttpResult>> StartServer(
        Services.DiscordService service,
        CancellationToken cancellationToken
    )
    {
        await service.StartAsync(cancellationToken);
        return TypedResults.Ok(service.IsRunning);
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