using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DevLiteServer.Core;

/// <summary>
/// Wraps Win32 Job Object with JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE flag.
/// Ensures all assigned child processes (Nginx, PHP, MySQL, etc.) terminate
/// automatically when LiteServer exits or terminates unexpectedly.
/// </summary>
public sealed class JobObject : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    public JobObject()
    {
        _handle = CreateJobObject(IntPtr.Zero, null);
        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create Win32 Job Object.");
        }

        var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        };

        var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = info
        };

        int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
        IntPtr pExtendedInfo = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(extendedInfo, pExtendedInfo, false);
            if (!SetInformationJobObject(_handle, JobObjectExtendedLimitInformation, pExtendedInfo, (uint)length))
            {
                throw new InvalidOperationException("Failed to set Job Object information.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pExtendedInfo);
        }
    }

    public bool AssignProcess(Process process)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (process == null || process.HasExited)
        {
            return false;
        }

        return AssignProcessToJobObject(_handle, process.Handle);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_handle != IntPtr.Zero)
            {
                CloseHandle(_handle);
                _handle = IntPtr.Zero;
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    ~JobObject()
    {
        Dispose();
    }

    #region Win32 P/Invoke

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
    private const int JobObjectExtendedLimitInformation = 9;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(
        IntPtr hJob,
        int JobObjectInfoClass,
        IntPtr lpJobObjectInfo,
        uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

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
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryLimit;
        public UIntPtr PeakJobMemoryLimit;
    }

    #endregion
}
