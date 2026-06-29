using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace IHLDota2Bot.Desktop.Services;

/// <summary>
/// Persists the player JWT between launches so the client can auto-login. On Windows
/// the token is encrypted with DPAPI (current-user scope); on other platforms it is
/// stored as a plain file under the user's app-data folder (no DPAPI available).
/// </summary>
public sealed class TokenStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IHLDota", "token.bin");

    public void Save(string token)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var data = Encoding.UTF8.GetBytes(token);
            var bytes = OperatingSystem.IsWindows()
                ? ProtectedData.Protect(data, optionalEntropy: null, DataProtectionScope.CurrentUser)
                : data;
            File.WriteAllBytes(_path, bytes);
        }
        catch
        {
            // Persistence is best-effort — a failure just means the user logs in again next launch.
        }
    }

    public string? Load()
    {
        try
        {
            if (!File.Exists(_path))
                return null;

            var bytes = File.ReadAllBytes(_path);
            var data = OperatingSystem.IsWindows()
                ? ProtectedData.Unprotect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser)
                : bytes;
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            return null;
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }
        catch
        {
            // ignore
        }
    }
}
