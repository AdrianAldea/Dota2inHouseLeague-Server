using System.Text.Json;

namespace IHLDota2Bot.Clients;

public class DiscordClient(IHttpClientFactory httpClientFactory)
{
    private const string DiscordClientName = "DiscordClient";
    private const string DiscordClientEndpoint = "https://localhost:7161/api/";

    public async Task SendMessageAsync(
        string message
    )
    {
        var httpClient = httpClientFactory.CreateClient(DiscordClientName);

        var response = await httpClient.PostAsJsonAsync(
            DiscordClientEndpoint + "message",
            new { Message = message },
            JsonSerializerOptions.Web
        );

        response.EnsureSuccessStatusCode();
    }
}
