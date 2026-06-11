using System.Diagnostics;

namespace IHLDota2Bot.AppHost;

/// <summary>
/// Makes sure the Podman engine is up before Aspire provisions its containers
/// (the persistent SQL Server). Podman is the container runtime for this project
/// — free of Docker Desktop's licensing — so the AppHost shouldn't require the
/// engine to be started by hand.
///
/// On Windows/macOS Podman runs its engine inside a "machine" (a lightweight VM;
/// WSL on Windows). This checks whether the engine responds and, if not, creates
/// the machine on first run (one-time) and starts it, then blocks until the
/// engine is reachable. On Linux Podman runs natively, so this only probes.
///
/// Best-effort: a no-op when the engine is already up. Any failure is logged and
/// swallowed so it can't stop the AppHost from starting — Aspire then surfaces
/// its own "is the container runtime running?" error as before.
/// </summary>
public static class PodmanBootstrap
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan InitTimeout = TimeSpan.FromMinutes(15);

    public static void EnsureRunning()
    {
        try
        {
            if (IsEngineReady())
            {
                return;
            }

            // Linux runs Podman natively (rootless socket / system service); the
            // machine model is Windows/macOS only. There we can't do much beyond
            // telling the developer the engine isn't reachable.
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
            {
                Console.Error.WriteLine(
                    "[PodmanBootstrap] Podman engine not reachable. Ensure the podman socket/service is running " +
                    "(e.g. `systemctl --user start podman.socket`).");
                return;
            }

            if (!MachineExists())
            {
                Console.WriteLine(
                    "[PodmanBootstrap] No Podman machine found; running one-time `podman machine init` " +
                    "(downloads a VM image, may take a few minutes)...");
                if (!RunStreaming(InitTimeout, "machine", "init"))
                {
                    Console.Error.WriteLine("[PodmanBootstrap] `podman machine init` failed. Run it manually, then retry.");
                    return;
                }
            }

            Console.WriteLine("[PodmanBootstrap] Starting the Podman machine...");
            // Ignore the exit code: "already running" returns non-zero, and the
            // readiness poll below is the real source of truth.
            RunStreaming(StartTimeout, "machine", "start");

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < StartupTimeout)
            {
                if (IsEngineReady())
                {
                    Console.WriteLine($"[PodmanBootstrap] Podman engine ready after {sw.Elapsed.TotalSeconds:F0}s.");
                    return;
                }
                Thread.Sleep(PollInterval);
            }

            Console.Error.WriteLine(
                $"[PodmanBootstrap] Podman engine still not ready after {StartupTimeout.TotalMinutes:F0} min; " +
                "Aspire may fail to start its containers.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[PodmanBootstrap] Could not ensure Podman is running: {ex.Message}");
        }
    }

    /// <summary>
    /// True when the Podman daemon answers. <c>podman version --format {{.Server.Version}}</c>
    /// only exits 0 once the server (engine) — not just the CLI — is reachable.
    /// </summary>
    private static bool IsEngineReady()
    {
        var (ok, _) = RunCapture(ProbeTimeout, "version", "--format", "{{.Server.Version}}");
        return ok;
    }

    private static bool MachineExists()
    {
        var (ok, output) = RunCapture(ProbeTimeout, "machine", "list", "--format", "{{.Name}}");
        return ok && !string.IsNullOrWhiteSpace(output);
    }

    /// <summary>Runs podman with output captured (for small, parseable results).</summary>
    private static (bool Ok, string Output) RunCapture(TimeSpan timeout, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("podman")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var process = Process.Start(psi);
            if (process is null) return (false, string.Empty);

            // Outputs here are tiny, so reading stdout to completion before waiting
            // can't deadlock on a full pipe buffer.
            var stdout = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();

            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return (false, stdout);
            }

            return (process.ExitCode == 0, stdout);
        }
        catch
        {
            // podman CLI missing from PATH, etc.
            return (false, string.Empty);
        }
    }

    /// <summary>
    /// Runs podman letting its output flow to the console — for the long, chatty
    /// `machine init` / `machine start` so the developer sees progress (and the
    /// inherited streams can't deadlock on a buffer).
    /// </summary>
    private static bool RunStreaming(TimeSpan timeout, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("podman") { UseShellExecute = false };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var process = Process.Start(psi);
            if (process is null) return false;

            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
