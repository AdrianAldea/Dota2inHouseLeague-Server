namespace IHLDota2Bot.Common.Requests;

public record MessageRequest
{
    public required string Message { get; init; }
}
