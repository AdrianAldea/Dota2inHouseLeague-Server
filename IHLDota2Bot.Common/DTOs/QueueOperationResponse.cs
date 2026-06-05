namespace IHLDota2Bot.Common.DTOs;

public record QueueOperationResponse
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
}
