using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Discord.Models;
using System.Text.Json;

namespace IHLDota2Bot.Discord.Clients;

public class SteamClient
{
    public const string HttpClientName = "SteamClient";

    private readonly HttpClient httpClient;
    private readonly ILogger<SteamClient> logger;

    public SteamClient(IHttpClientFactory httpClientFactory, ILogger<SteamClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(logger);

        httpClient = httpClientFactory.CreateClient(HttpClientName);
        this.logger = logger;
    }

    public async Task<RegisteredPlayer?> RegisterPlayerAsync(RegisterPlayerRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("api/gamemanager/register", request, JsonSerializerOptions.Web, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RegisteredPlayer>(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register player {UserId}", request.UserId);
            return null;
        }
    }

    public async Task<CreateLobbyResponse?> CreateLobbyAsync(ulong id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("api/gamemanager/create", id, JsonSerializerOptions.Web, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CreateLobbyResponse>(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create lobby for user {UserId}", id);
            return null;
        }
    }

    public async Task<ProfileResponse?> ShowProfileAsync(ulong id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<ProfileResponse>($"api/gamemanager/player/{id}", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch profile for user {UserId}", id);
            return null;
        }
    }
}
