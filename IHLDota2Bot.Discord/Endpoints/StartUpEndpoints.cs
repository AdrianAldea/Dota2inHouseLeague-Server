using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Discord.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace IHLDota2Bot.Discord.Endpoints;

public static class StartUpEndpoints
{
    public static void MapStartUpEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/start", StartServer);
        group.MapPost("/stop", StopServer);
        group.MapPost("/message", HandleMessage);
    }

    private static async Task HandleMessage(
        MessageRequest messageRequest,
        DiscordService service,
        CancellationToken cancellationToken
    )
    {
        await service.SendMessageAsync(messageRequest.Message, cancellationToken);
    }

    private static async Task<Results<Ok<bool>, StatusCodeHttpResult>> StartServer(
        DiscordService service,
        CancellationToken cancellationToken
    )
    {
        await service.StartAsync(cancellationToken);
        return TypedResults.Ok(service.IsRunning);
    }

    private static async Task<Results<Ok, StatusCodeHttpResult>> StopServer(
        DiscordService service,
        CancellationToken cancellationToken
    )
    {
        await service.StopAsync(cancellationToken);
        return TypedResults.Ok();
    }
}
