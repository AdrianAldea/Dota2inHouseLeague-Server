using System.Text.Json;

namespace IHLDota2Bot.Clients;

public class DiscordClient
{
    public const string HttpClientName = "DiscordClient";

    private readonly HttpClient httpClient;
    private readonly ILogger<DiscordClient> logger;

    public DiscordClient(IHttpClientFactory httpClientFactory, ILogger<DiscordClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(logger);

        httpClient = httpClientFactory.CreateClient(HttpClientName);
        this.logger = logger;
    }

    public async Task SendMessageAsync(
        string message,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(message))
            throw new ArgumentException("message cannot be empty", nameof(message));

        try
        {
            var response = await httpClient.PostAsJsonAsync(
                "api/message",
                new { Message = message },
                JsonSerializerOptions.Web,
                cancellationToken
            );

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Discord message");
        }
    }
}
