using Microsoft.AspNetCore.Http.HttpResults;

namespace IHLDota2Bot.Endpoints.StartUp;

public static class StartUpEndpoints
{
    public static void MapStartUpEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/start", StartServer);
    }

    private static async Task<Results<Ok<bool>, StatusCodeHttpResult>> StartServer(
        Services.IDota2Service service,
        CancellationToken cancellationToken
    )
    {
        var result = await service.StartServerAsync();
        return TypedResults.Ok(result);
    }
}
