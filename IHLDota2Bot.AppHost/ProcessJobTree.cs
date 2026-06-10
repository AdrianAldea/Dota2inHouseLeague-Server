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

    /// <summary>
    /// Belt-and-suspenders for the one case the kill-on-close job can't catch on
    /// its own: someone kills the intermediate <c>dotnet run</c> launcher but
    /// leaves <c>IHLDota2Bot.AppHost.exe</c> itself alive. Watches the parent
    /// process and, if it exits, exits this process too — which closes the job
    /// handle and reaps every child. Windows-only and best-effort.
    /// </summary>
    public static void ExitWhenParentExits()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            WatchParentWindows();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ProcessJobTree] Could not set up parent-process watch: {ex.Message}");
        }
    }

    [SupportedOSPlatform("windows")]
    private static void WatchParentWindows()
    {
        var parentPid = GetParentProcessId();
        if (parentPid <= 0)
        {
            return;
        }

        var parentHandle = OpenProcess(SYNCHRONIZE, false, (uint)parentPid);
        if (parentHandle == IntPtr.Zero)
        {
            // Parent already gone or not openable — nothing to watch.
            return;
        }

        // A dedicated background thread blocked on the parent's handle is the
        // simplest correct primitive: it's a pure kernel wait (no CPU, no poll),
        // and being a started thread it stays rooted until it returns.
        var watcher = new Thread(() =>
        {
            WaitForSingleObject(parentHandle, INFINITE);
            Console.Error.WriteLine("[ProcessJobTree] Parent process exited; shutting down AppHost so its children are reaped.");
            Environment.Exit(0);
        })
        {
            IsBackground = true,
            Name = "ParentProcessWatch"
        };
        watcher.Start();
        Console.WriteLine($"[ProcessJobTree] Watching parent process {parentPid}; AppHost will exit if it does.");
    }

    [SupportedOSPlatform("windows")]
    private static int GetParentProcessId()
    {
        var pbi = new PROCESS_BASIC_INFORMATION();
        var status = NtQueryInformationProcess(GetCurrentProcess(), ProcessBasicInformation, ref pbi, Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(), out _);
        return status == 0 ? pbi.InheritedFromUniqueProcessId.ToInt32() : -1;
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
    private const int ProcessBasicInformation = 0;
    private const uint SYNCHRONIZE = 0x00100000;
    private const uint INFINITE = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, out int returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr ExitStatus;
        public IntPtr PebBaseAddress;
        public IntPtr AffinityMask;
        public IntPtr BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

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
