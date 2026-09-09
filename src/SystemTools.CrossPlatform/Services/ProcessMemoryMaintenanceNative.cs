using System;
using System.Runtime.InteropServices;

namespace SystemTools.CrossPlatform.Services;

internal static class ProcessMemoryMaintenanceNative
{
    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    internal static bool TryTrimWorkingSet(IntPtr processHandle)
    {
        if (!OperatingSystem.IsWindows() || processHandle == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            return EmptyWorkingSet(processHandle);
        }
        catch
        {
            return false;
        }
    }
}
