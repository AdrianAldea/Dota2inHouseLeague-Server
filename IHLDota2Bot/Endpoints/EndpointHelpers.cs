using Microsoft.AspNetCore.Http.HttpResults;

namespace IHLDota2Bot.Endpoints;

/// <summary>
/// Shared try/catch + return-shape helpers for minimal-API endpoints.
/// Keeps individual endpoint handlers to one line.
/// </summary>
public static class EndpointHelpers
{
    public static async Task<Results<Ok<T>, StatusCodeHttpResult>> TryOkAsync<T>(
        ILogger logger, string action, Func<Task<T>> work)
    {
        try
        {
            return TypedResults.Ok(await work());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error: {Action}", action);
            return TypedResults.StatusCode(500);
        }
    }

    public static async Task<Results<Ok<T>, NotFound, StatusCodeHttpResult>> TryFindAsync<T>(
        ILogger logger, string action, Func<Task<T?>> work) where T : class
    {
        try
        {
            var result = await work();
            return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error: {Action}", action);
            return TypedResults.StatusCode(500);
        }
    }

    public static Results<Ok<T>, StatusCodeHttpResult> TryOk<T>(
        ILogger logger, string action, Func<T> work)
    {
        try
        {
            return TypedResults.Ok(work());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error: {Action}", action);
            return TypedResults.StatusCode(500);
        }
    }

    public static Results<Ok, StatusCodeHttpResult> Try(
        ILogger logger, string action, Action work)
    {
        try
        {
            work();
            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error: {Action}", action);
            return TypedResults.StatusCode(500);
        }
    }
}
