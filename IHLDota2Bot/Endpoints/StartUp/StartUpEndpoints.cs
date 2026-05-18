using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace IHLDota2Bot.Endpoints.StartUp;

public static class StartUpEndpoints
{
    public static void MapStartUpEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/start", StartServer);
    }

    private static async Task<Results<Ok<bool>, StatusCodeHttpResult>> StartServer(
        Services.Dota2Service service,
        [FromBody] StartUpRequest request,
        CancellationToken cancellationToken
    )
    {
        await service.StartServer();

        return TypedResults.Ok(true);
    }
}
