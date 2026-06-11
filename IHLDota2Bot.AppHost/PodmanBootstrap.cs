using System.Diagnostics;
using System.IO;
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

    // On `machine start`, Podman's win-sshproxy/gvproxy helpers drop a stray "NUL"
    // file (the Windows null device opened as a relative path) into the working
    // directory. Process.Start inherits the AppHost's working directory (the
    // project folder), so without this the junk file lands in the repo. Running
    // podman from the temp dir keeps it out of the source tree.
    private static readonly string PodmanWorkingDir = Path.GetTempPath();

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
                if (!RunQuiet(InitTimeout, "machine", "init"))
                {
                    Console.Error.WriteLine("[PodmanBootstrap] `podman machine init` failed. Run it manually, then retry.");
                    return;
                }
                Console.WriteLine("[PodmanBootstrap] Starting the Podman machine...");
                RunQuiet(StartTimeout, "machine", "start");
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
                RunQuiet(StartTimeout, "machine", "stop");
                RunQuiet(StartTimeout, "machine", "start");
            }
            else
            {
                Console.WriteLine("[PodmanBootstrap] Starting the Podman machine...");
                RunQuiet(StartTimeout, "machine", "start");
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
                CreateNoWindow = true,
                WorkingDirectory = PodmanWorkingDir
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var process = new Process { StartInfo = psi };
            // Drain stderr on a background thread so a chatty error stream can't
            // fill its pipe buffer and deadlock the synchronous stdout read below.
            process.ErrorDataReceived += static (_, _) => { };
            if (!process.Start()) return (false, string.Empty);
            process.BeginErrorReadLine();

            var stdout = process.StandardOutput.ReadToEnd();

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
    /// Runs a podman command (the longer `machine init` / `start` / `stop`),
    /// returning true on a clean exit.
    ///
    /// Output is redirected and silently drained — deliberately not re-emitted to
    /// the console — for two reasons:
    /// 1. `machine start` spawns long-lived helpers (win-sshproxy/gvproxy) that
    ///    inherit the podman process's handles. Redirecting hands them our pipes
    ///    instead of the AppHost's real console handles, which the helpers would
    ///    otherwise hold open for their whole lifetime.
    /// 2. Writing podman's output to the console from the background reader thread
    ///    can contend with the AppHost's own console writes under a Visual Studio
    ///    debug console and freeze startup right after "machine started".
    /// We still surface progress through the bootstrap's own status lines, which
    /// are written from the main thread.
    /// </summary>
    private static bool RunQuiet(TimeSpan timeout, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("podman")
            {
                UseShellExecute = false,
                WorkingDirectory = PodmanWorkingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var process = new Process { StartInfo = psi };
            // Empty handlers still drain the pipes (so podman can't block on a full
            // buffer) without touching the console.
            process.OutputDataReceived += static (_, _) => { };
            process.ErrorDataReceived += static (_, _) => { };

            if (!process.Start()) return false;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait only for the process — NOT the streams. The spawned helpers keep
            // the redirected pipes open for their lifetime, so waiting for stream
            // EOF (the parameterless WaitForExit) would hang. The exit code is
            // available as soon as podman itself exits.
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
