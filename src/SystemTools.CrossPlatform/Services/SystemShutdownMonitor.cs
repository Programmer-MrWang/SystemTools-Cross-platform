using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace SystemTools.CrossPlatform.Services;

public sealed class SystemShutdownMonitor : IDisposable
{
    internal const string WindowCaption = "SystemTools.CrossPlatform.SystemShutdownMonitor";

    private const int WmQueryEndSession = 0x0011;
    private const int WmEndSession = 0x0016;
    private const string WindowClassName = "SystemTools.CrossPlatform.SystemShutdownMonitorWindow";
    private const uint WsPopup = 0x80000000;

    private static int _isSessionEnding;
    private static bool _classRegistered;
    private static readonly WndProcDelegate WndProcHandler = WndProc;

    private int _isStarted;
    private IntPtr _windowHandle = IntPtr.Zero;

    public bool IsSessionEnding => Volatile.Read(ref _isSessionEnding) != 0;

    public void Start()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (Interlocked.Exchange(ref _isStarted, 1) != 0)
        {
            return;
        }

        try
        {
            EnsureWindowClassRegistered();
            var moduleHandle = GetModuleHandleW(null);
            _windowHandle = CreateWindowExW(
                0,
                WindowClassName,
                WindowCaption,
                WsPopup,
                0, 0, 0, 0,
                IntPtr.Zero,
                IntPtr.Zero,
                moduleHandle,
                IntPtr.Zero);
            if (_windowHandle == IntPtr.Zero)
            {
                Volatile.Write(ref _isStarted, 0);
            }
        }
        catch
        {
            Volatile.Write(ref _isStarted, 0);
            _windowHandle = IntPtr.Zero;
        }
    }

    internal void MarkSessionEnding()
    {
        Volatile.Write(ref _isSessionEnding, 1);
    }
    
    internal void MarkIfOsShutdown(object eventArgs)
    {
        try
        {
            var property = eventArgs.GetType().GetProperty(
                "IsOSShutdown",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.PropertyType == typeof(bool) && property.GetValue(eventArgs) is true)
            {
                MarkSessionEnding();
            }
        }
        catch
        {
            // The property is internal in some Avalonia versions; the native window remains the fallback.
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isStarted, 0) == 0)
        {
            return;
        }

        if (_windowHandle != IntPtr.Zero)
        {
            DestroyWindow(_windowHandle);
            _windowHandle = IntPtr.Zero;
        }
    }

    private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WmQueryEndSession:
                Volatile.Write(ref _isSessionEnding, 1);
                break;
            case WmEndSession:
                Volatile.Write(ref _isSessionEnding, wParam != IntPtr.Zero ? 1 : 0);
                break;
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private static void EnsureWindowClassRegistered()
    {
        if (_classRegistered)
        {
            return;
        }

        var wndClass = new WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(WndProcHandler),
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = GetModuleHandleW(null),
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = WindowClassName,
            hIconSm = IntPtr.Zero
        };

        if (RegisterClassExW(ref wndClass) != 0)
        {
            _classRegistered = true;
            return;
        }

        _classRegistered = Marshal.GetLastWin32Error() == 1410;
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WndClassEx wndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        uint dwExStyle,
        [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = false)]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern IntPtr GetModuleHandleW([MarshalAs(UnmanagedType.LPWStr)] string? lpModuleName);
}