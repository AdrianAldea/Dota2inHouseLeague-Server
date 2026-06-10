using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace IHLDota2Bot.AppHost;

/// <summary>
/// Ties every child process the AppHost spawns to the AppHost's own lifetime via
/// a Windows Job Object created with <c>JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE</c>.
///
/// Aspire launches the Game Manager, the Discord bot, and its DCP orchestrator as
/// child processes. When the AppHost is closed from a terminal or the IDE without
/// a graceful Ctrl+C — or is force-killed — those children would normally orphan
/// and pile up in Task Manager (each wrapped in its own conhost.exe). Assigning
/// the AppHost process to a kill-on-close job means the OS terminates the whole
/// job the instant the last handle to it closes (i.e. when the AppHost dies, by
/// any means), so the entire process tree goes with it.
///
/// Best-effort and Windows-only: any failure is logged and swallowed so it can
/// never stop the AppHost from starting. On non-Windows hosts this is a no-op
/// (process-group / cgroup semantics already clean children up there).
/// </summary>
public static class ProcessJobTree
{
    // The job handle must stay open for the whole process lifetime: kill-on-close
    // fires when the *last* handle closes. Holding it in a static keeps it alive
    // (and uncollected) until the process exits, at which point the OS closes it
    // and reaps the job.
    private static IntPtr _jobHandle = IntPtr.Zero;

    public static void KillChildrenOnExit()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            AttachWindows();
        }
        catch (Exception ex)
        {
            // Never let process-management plumbing break app startup.
            Console.Error.WriteLine($"[ProcessJobTree] Could not attach kill-on-close job: {ex.Message}");
        }
    }

    [SupportedOSPlatform("windows")]
    private static void AttachWindows()
    {
        var job = CreateJobObject(IntPtr.Zero, null);
        if (job == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CreateJobObject failed (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            }
        };

        var length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        var infoPtr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(info, infoPtr, fDeleteOld: false);

            if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, infoPtr, (uint)length))
            {
                throw new InvalidOperationException($"SetInformationJobObject failed (Win32 error {Marshal.GetLastWin32Error()}).");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(infoPtr);
        }

        if (!AssignProcessToJobObject(job, GetCurrentProcess()))
        {
            throw new InvalidOperationException($"AssignProcessToJobObject failed (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        // Keep the handle alive for the process lifetime. Deliberately never closed.
        _jobHandle = job;
        Console.WriteLine("[ProcessJobTree] Child processes are tied to the AppHost lifetime (kill-on-close job).");
    }

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
    private const int JobObjectExtendedLimitInformation = 9;

    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int jobObjectInformationClass, IntPtr lpJobObjectInformation, uint cbJobObjectInformationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
