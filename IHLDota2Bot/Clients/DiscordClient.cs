using System.Text.Json;

namespace IHLDota2Bot.Clients;

public class DiscordClient
{
    private const string DiscordClientName = "DiscordClient";
    private const string DiscordClientEndpoint = "https://localhost:7161/api/";
    private readonly IHttpClientFactory _httpClientFactory;

    public DiscordClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public async Task SendMessageAsync(string message)
    {
        if (string.IsNullOrEmpty(message)) throw new ArgumentException("message cannot be empty", nameof(message));

        var httpClient = _httpClientFactory.CreateClient(DiscordClientName);

        var response = await httpClient.PostAsJsonAsync(
            DiscordClientEndpoint + "message",
            new { Message = message },
            JsonSerializerOptions.Web
        );

        response.EnsureSuccessStatusCode();
    }
}
