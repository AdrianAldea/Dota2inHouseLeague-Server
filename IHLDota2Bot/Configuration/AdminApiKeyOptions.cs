namespace IHLDota2Bot.Configuration;

public class AdminApiKeyOptions
{
    public const string SectionName = "GameManager";

    /// <summary>
    /// Shared secret required in the <c>X-Api-Key</c> header on every
    /// <c>/api/gamemanager/admin/*</c> request. Supply via user-secrets or an
    /// environment variable (<c>GameManager__AdminApiKey</c>) — never in
    /// appsettings.json. When empty the admin gate is disabled and a startup
    /// warning is emitted; this keeps development simple without needing secrets.
    /// </summary>
    public string AdminApiKey { get; set; } = string.Empty;
}
