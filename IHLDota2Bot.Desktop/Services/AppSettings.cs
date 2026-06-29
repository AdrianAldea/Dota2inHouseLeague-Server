using System;
using System.IO;
using System.Text.Json;

namespace IHLDota2Bot.Desktop.Services;

/// <summary>
/// Desktop runtime config. Read once from <c>appsettings.json</c> next to the exe;
/// the <c>IHL_API_BASE</c> environment variable overrides it (handy for pointing a
/// dev build at a staging server without editing files).
/// </summary>
public sealed class AppSettings
{
    /// <summary>Root of the Game Manager service, e.g. <c>http://localhost:5193</c>.</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5193";

    public static AppSettings Load()
    {
        var settings = new AppSettings();

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
                if (loaded is not null)
                    settings = loaded;
            }
        }
        catch
        {
            // Malformed/missing config falls back to defaults — never block startup on it.
        }

        var envOverride = Environment.GetEnvironmentVariable("IHL_API_BASE");
        if (!string.IsNullOrWhiteSpace(envOverride))
            settings.ApiBaseUrl = envOverride;

        return settings;
    }
}
