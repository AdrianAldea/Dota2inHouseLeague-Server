using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

namespace IHLDota2Bot.AppHost;

/// <summary>
/// Makes sure the Docker engine is up before Aspire provisions its containers
/// (the persistent SQL Server). Docker is the container runtime for this project
/// (Aspire's default), so the AppHost shouldn't require Docker Desktop to be
/// started by hand.
///
/// On Windows this checks whether the engine responds and, if not, launches
/// Docker Desktop and polls until the engine is reachable. The whole thing runs
/// on a background thread with a hard time cap, so a slow or stuck engine can
/// never freeze AppHost startup — if we give up, Aspire surfaces its own
/// "is Docker running?" error as before. On non-Windows hosts this only probes
/// (Docker runs as a daemon/service there). Any failure is logged and swallowed.
/// </summary>
public static class DockerBootstrap
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(150);

    // Run docker from the temp dir so any tool-generated junk stays out of the repo.
    private static readonly string DockerWorkingDir = Path.GetTempPath();

    public static void EnsureRunning()
    {
        // Never let Docker bootstrapping block AppHost startup indefinitely: run it
        // on a background thread and continue after a hard cap. Join returns as soon
        // as the work finishes (the normal case is fast when Docker is already up).
        var worker = new Thread(EnsureRunningCore) { IsBackground = true, Name = "DockerBootstrap" };
        worker.Start();
        if (!worker.Join(OverallTimeout))
        {
            Console.Error.WriteLine(
                $"[DockerBootstrap] Still preparing Docker after {OverallTimeout.TotalSeconds:F0}s; continuing AppHost " +
                "startup so it can't hang. If containers fail to start, start Docker Desktop and retry.");
        }
    }

    private static void EnsureRunningCore()
    {
        try
        {
            if (IsEngineReady())
            {
                return;
            }

            // Linux/macOS run Docker as a daemon/service; we can't launch a desktop
            // app there, so just report if it's not reachable.
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("[DockerBootstrap] Docker engine not reachable. Ensure the Docker daemon is running.");
                return;
            }

            Console.WriteLine("[DockerBootstrap] Docker engine not responding; launching Docker Desktop...");
            if (!TryStartDockerDesktop(out var error))
            {
                Console.Error.WriteLine($"[DockerBootstrap] Could not launch Docker Desktop ({error}). Please start it manually.");
                return;
            }

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < StartupTimeout)
            {
                if (IsEngineReady())
                {
                    Console.WriteLine($"[DockerBootstrap] Docker engine ready after {sw.Elapsed.TotalSeconds:F0}s.");
                    return;
                }
                Thread.Sleep(PollInterval);
            }

            Console.Error.WriteLine(
                $"[DockerBootstrap] Docker engine still not ready after {StartupTimeout.TotalSeconds:F0}s; " +
                "Aspire may fail to start its containers.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DockerBootstrap] Could not ensure Docker is running: {ex.Message}");
        }
    }

    /// <summary>
    /// True when the Docker daemon answers. <c>docker version --format {{.Server.Version}}</c>
    /// only exits 0 (with a non-empty value) once the server (engine) — not just the
    /// CLI — is reachable.
    /// </summary>
    private static bool IsEngineReady()
    {
        try
        {
            var psi = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = DockerWorkingDir
            };
            psi.ArgumentList.Add("version");
            psi.ArgumentList.Add("--format");
            psi.ArgumentList.Add("{{.Server.Version}}");

            using var process = new Process { StartInfo = psi };
            // Drain stderr on a background thread so it can't fill its pipe buffer
            // and deadlock the stdout read below.
            process.ErrorDataReceived += static (_, _) => { };
            if (!process.Start()) return false;
            process.BeginErrorReadLine();

            var stdout = process.StandardOutput.ReadToEnd();

            if (!process.WaitForExit((int)ProbeTimeout.TotalMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return false;
            }

            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout);
        }
        catch
        {
            // docker CLI missing from PATH, etc.
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryStartDockerDesktop(out string error)
    {
        error = string.Empty;

        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Docker", "Docker", "Docker Desktop.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Docker", "Docker", "Docker Desktop.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Docker", "Docker Desktop.exe"),
        ];

        var exe = candidates.FirstOrDefault(File.Exists);
        if (exe is null)
        {
            error = "Docker Desktop.exe not found in the usual install locations";
            return false;
        }

        try
        {
            // UseShellExecute so it launches the GUI app the same way the user would;
            // returns immediately, and readiness is determined by the poll above.
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
