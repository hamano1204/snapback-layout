using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace snapback_layout;

public static class WindowEnumerator
{
    public static List<MonitorInfo> EnumerateMonitors()
    {
        var monitors = new List<MonitorInfo>();
        var screens = Screen.AllScreens;

        for (int i = 0; i < screens.Length; i++)
        {
            var screen = screens[i];
            int dpi = GetScreenDpi(screen);

            monitors.Add(new MonitorInfo
            {
                Id = i + 1,
                DeviceName = screen.DeviceName,
                Bounds = new Bounds
                {
                    X = screen.Bounds.X,
                    Y = screen.Bounds.Y,
                    Width = screen.Bounds.Width,
                    Height = screen.Bounds.Height
                },
                Dpi = dpi
            });
        }

        return monitors;
    }

    public static List<WindowInfo> EnumerateWindows(Screen[] screens)
    {
        var windows = new List<WindowInfo>();
        var processNameCache = new Dictionary<uint, string>();
        int zOrder = 1;

        IntPtr foregroundWnd = Win32.GetForegroundWindow();

        // Reuse StringBuilders to avoid GC pressure during enumeration
        var classBuilder = new StringBuilder(256);
        var titleBuilder = new StringBuilder(512);

        Win32.EnumWindows((hWnd, lParam) =>
        {
            if (ShouldIncludeWindow(hWnd, classBuilder, titleBuilder))
            {
                var winInfo = GetWindowInfo(hWnd, zOrder++, screens, processNameCache, classBuilder, titleBuilder, foregroundWnd);
                if (winInfo != null)
                {
                    windows.Add(winInfo);
                }
            }
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    public static int GetScreenDpi(Screen screen)
    {
        try
        {
            var pt = new Point(screen.Bounds.X + screen.Bounds.Width / 2, screen.Bounds.Y + screen.Bounds.Height / 2);
            IntPtr hMonitor = Win32.MonitorFromPoint(pt, 2 /* MONITOR_DEFAULTTONEAREST */);
            if (hMonitor != IntPtr.Zero)
            {
                if (Win32.GetDpiForMonitor(hMonitor, 0, out uint dpiX, out uint dpiY) == 0)
                {
                    return (int)dpiX;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get DPI for monitor: {ex.Message}");
        }
        return 96; // Fallback DPI
    }

    private static bool ShouldIncludeWindow(IntPtr hWnd, StringBuilder classBuilder, StringBuilder titleBuilder)
    {
        int style = Win32.GetWindowLong(hWnd, Win32.GWL_STYLE);
        int exStyle = Win32.GetWindowLong(hWnd, Win32.GWL_EXSTYLE);

        // Skip child windows
        if ((style & (int)Win32.WS_CHILD) != 0) return false;

        // Skip invisible windows
        if (!Win32.IsWindowVisible(hWnd)) return false;

        // Skip tool windows unless they are also app windows
        if ((exStyle & Win32.WS_EX_TOOLWINDOW) != 0 && (exStyle & Win32.WS_EX_APPWINDOW) == 0) return false;

        // Skip shell and typical desktop/tray containers
        IntPtr shellWnd = Win32.GetShellWindow();
        if (hWnd == shellWnd) return false;

        classBuilder.Clear();
        Win32.GetClassName(hWnd, classBuilder, classBuilder.Capacity);
        string cls = classBuilder.ToString();

        if (cls == "Shell_TrayWnd" || cls == "Progman" || cls == "Button" || cls == "WorkerW") return false;

        // Skip windows without titles
        titleBuilder.Clear();
        Win32.GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
        string t = titleBuilder.ToString();

        if (string.IsNullOrWhiteSpace(t)) return false;

        return true;
    }

    private static WindowInfo? GetWindowInfo(IntPtr hWnd, int zIndex, Screen[] screens, Dictionary<uint, string> processNameCache, StringBuilder classBuilder, StringBuilder titleBuilder, IntPtr foregroundWnd)
    {
        try
        {
            titleBuilder.Clear();
            Win32.GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);

            classBuilder.Clear();
            Win32.GetClassName(hWnd, classBuilder, classBuilder.Capacity);

            Win32.GetWindowThreadProcessId(hWnd, out uint pid);
            if (!processNameCache.TryGetValue(pid, out string? processName))
            {
                processName = string.Empty;
                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    processName = proc.ProcessName;
                }
                catch { }
                processNameCache[pid] = processName;
            }

            string title = titleBuilder.ToString();
            string className = classBuilder.ToString();

            var placement = new Win32.WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            if (!Win32.GetWindowPlacement(hWnd, ref placement)) return null;

            WindowState state = WindowState.Normal;
            if (placement.showCmd == Win32.SW_SHOWMAXIMIZED) state = WindowState.Maximized;
            else if (placement.showCmd == Win32.SW_SHOWMINIMIZED) state = WindowState.Minimized;

            var rect = placement.rcNormalPosition;

            // Find monitor index using MonitorFromWindow and matching DeviceName
            int monitorId = 1;
            IntPtr hMonitor = Win32.MonitorFromWindow(hWnd, Win32.MONITOR_DEFAULTTONEAREST);
            if (hMonitor != IntPtr.Zero)
            {
                var monitorInfo = new Win32.MONITORINFOEX();
                monitorInfo.cbSize = Marshal.SizeOf(monitorInfo);
                if (Win32.GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    string deviceName = monitorInfo.szDevice;
                    for (int i = 0; i < screens.Length; i++)
                    {
                        if (screens[i].DeviceName == deviceName)
                        {
                            monitorId = i + 1;
                            break;
                        }
                    }
                }
            }

            return new WindowInfo
            {
                Hwnd = hWnd.ToInt64(),
                ProcessId = pid,
                ProcessName = processName,
                Title = title,
                ClassName = className,
                State = state,
                Bounds = new Bounds
                {
                    X = rect.Left,
                    Y = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height
                },
                ZIndex = zIndex,
                MonitorId = monitorId,
                IsForeground = (hWnd == foregroundWnd)
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get window info: {ex.Message}");
            return null;
        }
    }
}
