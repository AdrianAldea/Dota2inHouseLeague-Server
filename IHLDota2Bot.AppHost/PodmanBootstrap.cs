using System.Diagnostics;
using System.Linq;

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
            // Fast path: engine already answering — nothing to do. One quick retry
            // rides out a transient probe blip so we don't needlessly restart a
            // perfectly healthy machine.
            if (IsEngineReady())
            {
                return;
            }
            Thread.Sleep(TimeSpan.FromSeconds(2));
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

            var (machineExists, machineRunning) = GetMachineStatus();

            if (!machineExists)
            {
                Console.WriteLine(
                    "[PodmanBootstrap] No Podman machine found; running one-time `podman machine init` " +
                    "(downloads a VM image, may take a few minutes)...");
                if (!RunStreaming(InitTimeout, "machine", "init"))
                {
                    Console.Error.WriteLine("[PodmanBootstrap] `podman machine init` failed. Run it manually, then retry.");
                    return;
                }
                Console.WriteLine("[PodmanBootstrap] Starting the Podman machine...");
                RunStreaming(StartTimeout, "machine", "start");
            }
            else if (machineRunning)
            {
                // The machine is up but its engine isn't answering — a stale
                // connection (a known Podman-on-Windows hiccup once the VM has
                // been up a while). A plain `podman machine start` here just fails
                // with "already running" without fixing anything, so restart the
                // machine to re-establish the connection.
                Console.WriteLine(
                    "[PodmanBootstrap] Podman machine is running but its engine isn't responding; " +
                    "restarting it to restore the connection...");
                RunStreaming(StartTimeout, "machine", "stop");
                RunStreaming(StartTimeout, "machine", "start");
            }
            else
            {
                Console.WriteLine("[PodmanBootstrap] Starting the Podman machine...");
                RunStreaming(StartTimeout, "machine", "start");
            }

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

    /// <summary>
    /// Reports whether a Podman machine exists and whether it's running. Uses
    /// `machine list` / `machine inspect`, which read the VM's own state and work
    /// even when the engine API socket is unreachable — so we can tell "stopped"
    /// apart from "running but disconnected" and react correctly.
    /// </summary>
    private static (bool Exists, bool Running) GetMachineStatus()
    {
        var (okList, names) = RunCapture(ProbeTimeout, "machine", "list", "--format", "{{.Name}}");
        if (!okList || string.IsNullOrWhiteSpace(names))
        {
            return (false, false);
        }

        var name = names
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(n => n.TrimEnd('*').Trim())
            .FirstOrDefault(n => n.Length > 0);
        if (string.IsNullOrEmpty(name))
        {
            return (false, false);
        }

        var (okState, state) = RunCapture(ProbeTimeout, "machine", "inspect", name, "--format", "{{.State}}");
        var running = okState && state.Trim().Equals("running", StringComparison.OrdinalIgnoreCase);
        return (true, running);
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
