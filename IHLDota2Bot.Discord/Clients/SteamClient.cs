using IHLDota2Bot.Discord.Models;
using System.Text.Json;

namespace IHLDota2Bot.Discord.Clients;

public class SteamClient(IHttpClientFactory httpClientFactory)
{
    private const string SteamClientName = "SteamClient";
    private const string SteamClientEndpoint = "https://api.example.com/steam/";

    public async Task RegisterPlayerAsync(
        RegisterPlayerRequest request
    )
    {
        var httpClient = httpClientFactory.CreateClient(SteamClientName);

        var response = await httpClient.PostAsJsonAsync(
            SteamClientEndpoint,
            request,
            JsonSerializerOptions.Web
        );

        response.EnsureSuccessStatusCode();

        await response.Content.ReadFromJsonAsync<string>();
    }

    public async Task<CreateLobbyResponse?> CreateLobbyAsync(
        ulong id
    )
    {
        var httpClient = httpClientFactory.CreateClient(SteamClientName);

        var response = await httpClient.PostAsJsonAsync(
            SteamClientEndpoint,
            id,
            JsonSerializerOptions.Web
        );

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CreateLobbyResponse>();
    }

    public async Task<ProfileResponse?> ShowProfileAsync(
        ulong id
    )
    {
        var httpClient = httpClientFactory.CreateClient(SteamClientName);

        var response = await httpClient.GetFromJsonAsync<ProfileResponse>(
            SteamClientEndpoint + id
        );

        return response;
    }
}
