using System;
using System.IO;
using System.Text;

namespace IHLDota2Bot.Desktop;

/// <summary>
/// Best-effort crash logger. Appends unhandled exceptions to
/// <c>%LOCALAPPDATA%\IHLDota2Bot\crash.log</c> so a failed launch on a tester's machine
/// leaves a readable trace they can send back, instead of the app silently vanishing.
/// Never throws — recording a crash must not cause a second one.
/// </summary>
internal static class CrashLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IHLDota2Bot",
        "crash.log"
    );

    public static void Write(Exception? ex)
    {
        if (ex is null)
            return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

            var entry = new StringBuilder()
                .AppendLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] Unhandled exception")
                .AppendLine(ex.ToString())
                .AppendLine(new string('-', 80))
                .ToString();

            File.AppendAllText(LogPath, entry);
        }
        catch
        {
            // Logging is best-effort: swallow any IO/permission failure so we never crash
            // while trying to record a crash.
        }
    }
}
