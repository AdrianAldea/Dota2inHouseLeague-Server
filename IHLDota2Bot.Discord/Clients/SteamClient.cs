using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Discord.Models;
using System.Text.Json;

namespace IHLDota2Bot.Discord.Clients;

public class SteamClient(IHttpClientFactory httpClientFactory)
{
    private const string SteamClientName = "SteamClient";
    private const string SteamClientEndpoint = "https://localhost:7114/api/gamemanager/";

    public async Task<RegisteredPlayer?> RegisterPlayerAsync(
        RegisterPlayerRequest request
    )
    {
        var httpClient = httpClientFactory.CreateClient(SteamClientName);

        var response = await httpClient.PostAsJsonAsync(
            SteamClientEndpoint + "register",
            request,
            JsonSerializerOptions.Web
        );

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RegisteredPlayer>();
    }

    public async Task<CreateLobbyResponse?> CreateLobbyAsync(
        ulong id
    )
    {
        var httpClient = httpClientFactory.CreateClient(SteamClientName);

        var response = await httpClient.PostAsJsonAsync(
            SteamClientEndpoint + "create",
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
