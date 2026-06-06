using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace snapback_layout;

public static class RestoreEngine
{
    public static void RestoreLayout(Snapshot snapshot, Settings settings)
    {
        try
        {
            var processNameCache = new Dictionary<uint, string>();

            // 1. First pass: Restore positions, sizes, and states (maximized/minimized)
            var windowsToRestore = snapshot.Windows.OrderByDescending(w => w.ZIndex).ToList();
            var validWindows = new List<(IntPtr hWnd, WindowInfo info)>();

            foreach (var winInfo in windowsToRestore)
            {
                IntPtr hWnd = FindWindowHandle(winInfo, processNameCache);
                if (hWnd == IntPtr.Zero)
                {
                    Debug.WriteLine($"Skipped restoring window: {winInfo.Title} (Process {winInfo.ProcessName} not running)");
                    continue;
                }

                RestoreWindow(hWnd, winInfo, snapshot, settings);
                validWindows.Add((hWnd, winInfo));
            }

            // 2. Second pass: Establish exact Z-order chain (from top to bottom)
            var orderedWindows = validWindows.OrderBy(w => w.info.ZIndex).ToList();
            IntPtr lastHwnd = IntPtr.Zero;

            foreach (var item in orderedWindows)
            {
                if (lastHwnd == IntPtr.Zero)
                {
                    // Topmost window: Bring to top and activate
                    Win32.SetWindowPos(item.hWnd, (IntPtr)0 /* HWND_TOP */, 0, 0, 0, 0,
                        Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_SHOWWINDOW);
                    Win32.SetForegroundWindow(item.hWnd);
                }
                else
                {
                    // Place directly behind the previously restored window in the chain
                    Win32.SetWindowPos(item.hWnd, lastHwnd, 0, 0, 0, 0,
                        Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
                }
                lastHwnd = item.hWnd;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to restore snapshot: {ex.Message}");
        }
    }

    private static IntPtr FindWindowHandle(WindowInfo winInfo, Dictionary<uint, string> processNameCache)
    {
        // 1. Try by HWND first, check if it exists and matches PID/ProcessName
        IntPtr hWnd = new IntPtr(winInfo.Hwnd);
        if (IsWindowStillValid(hWnd, winInfo.ProcessId, winInfo.ProcessName, processNameCache))
        {
            return hWnd;
        }

        // Reuse StringBuilders to reduce GC allocations during enumeration loops
        var classBuilder = new StringBuilder(256);
        var titleBuilder = new StringBuilder(512);

        // 2. If HWND changed, search for open windows matching Process ID or name and class name
        IntPtr foundWnd = IntPtr.Zero;
        Win32.EnumWindows((h, lParam) =>
        {
            if (!Win32.IsWindowVisible(h)) return true; // Only visible windows
            Win32.GetWindowThreadProcessId(h, out uint pid);
            if (pid == winInfo.ProcessId)
            {
                classBuilder.Clear();
                Win32.GetClassName(h, classBuilder, classBuilder.Capacity);
                if (classBuilder.ToString() == winInfo.ClassName)
                {
                    foundWnd = h;
                    return false; // Stop enumeration
                }
            }
            return true;
        }, IntPtr.Zero);

        if (foundWnd != IntPtr.Zero) return foundWnd;

        // 3. Fallback: search by Process Name and Class Name or Title
        Win32.EnumWindows((h, lParam) =>
        {
            if (!Win32.IsWindowVisible(h)) return true; // Only visible windows
            Win32.GetWindowThreadProcessId(h, out uint pid);
            if (!processNameCache.TryGetValue(pid, out string? pName))
            {
                pName = string.Empty;
                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    pName = proc.ProcessName;
                }
                catch { }
                processNameCache[pid] = pName;
            }

            if (pName == winInfo.ProcessName)
            {
                classBuilder.Clear();
                Win32.GetClassName(h, classBuilder, classBuilder.Capacity);
                titleBuilder.Clear();
                Win32.GetWindowText(h, titleBuilder, titleBuilder.Capacity);

                if (classBuilder.ToString() == winInfo.ClassName || titleBuilder.ToString() == winInfo.Title)
                {
                    foundWnd = h;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);

        return foundWnd;
    }

    private static bool IsWindowStillValid(IntPtr hWnd, int processId, string processName, Dictionary<uint, string> processNameCache)
    {
        try
        {
            if (!Win32.IsWindowVisible(hWnd)) return false;
            Win32.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid != processId) return false;

            if (!processNameCache.TryGetValue(pid, out string? pName))
            {
                pName = string.Empty;
                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    pName = proc.ProcessName;
                }
                catch { }
                processNameCache[pid] = pName;
            }

            return pName == processName;
        }
        catch
        {
            return false;
        }
    }

    private static void RestoreWindow(IntPtr hWnd, WindowInfo winInfo, Snapshot snapshot, Settings settings)
    {
        try
        {
            var placement = new Win32.WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            
            if (!Win32.GetWindowPlacement(hWnd, ref placement))
            {
                Debug.WriteLine($"Failed to get window placement for: {winInfo.Title}");
                return;
            }

            int x = winInfo.Bounds.X;
            int y = winInfo.Bounds.Y;
            int w = winInfo.Bounds.Width;
            int h = winInfo.Bounds.Height;

            // Apply DPI scaling if enabled
            if (settings.DpiCorrectionEnabled)
            {
                var savedMonitor = snapshot.Monitors.FirstOrDefault(m => m.Id == winInfo.MonitorId);
                if (savedMonitor != null && savedMonitor.Dpi > 0)
                {
                    var screens = System.Windows.Forms.Screen.AllScreens;
                    var targetScreen = screens.FirstOrDefault(s => s.DeviceName == savedMonitor.DeviceName);
                    if (targetScreen == null && savedMonitor.Id - 1 >= 0 && savedMonitor.Id - 1 < screens.Length)
                    {
                        targetScreen = screens[savedMonitor.Id - 1];
                    }
                    if (targetScreen == null)
                    {
                        targetScreen = System.Windows.Forms.Screen.PrimaryScreen ?? screens[0];
                    }

                    int currentDpi = WindowEnumerator.GetScreenDpi(targetScreen);
                    if (currentDpi != savedMonitor.Dpi)
                    {
                        double scale = (double)currentDpi / savedMonitor.Dpi;
                        
                        // Scale size
                        w = (int)Math.Round(w * scale);
                        h = (int)Math.Round(h * scale);

                        // Scale relative position from monitor origin
                        int relX = x - savedMonitor.Bounds.X;
                        int relY = y - savedMonitor.Bounds.Y;
                        relX = (int)Math.Round(relX * scale);
                        relY = (int)Math.Round(relY * scale);

                        x = targetScreen.Bounds.X + relX;
                        y = targetScreen.Bounds.Y + relY;
                        
                        Debug.WriteLine($"DPI Scale applied ({savedMonitor.Dpi} -> {currentDpi}, scale={scale:F2}) to window: {winInfo.Title}");
                    }
                }
            }

            // Check if coordinates overlap with any current visible screen area
            var targetRect = new System.Drawing.Rectangle(x, y, w, h);
            bool isVisible = false;

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var intersect = System.Drawing.Rectangle.Intersect(screen.Bounds, targetRect);
                // If it overlaps with at least 50x50 pixels of a screen, we count it as visible
                if (intersect.Width >= 50 && intersect.Height >= 50)
                {
                    isVisible = true;
                    break;
                }
            }

            if (!isVisible)
            {
                // Rescue window: put it on the primary screen's center/working area
                var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen ?? System.Windows.Forms.Screen.AllScreens[0];
                var workArea = primaryScreen.WorkingArea;
                
                // Keep dimensions but scale down if larger than screen
                w = Math.Min(w, workArea.Width - 100);
                h = Math.Min(h, workArea.Height - 100);
                
                // Position in the center of the working area
                x = workArea.X + (workArea.Width - w) / 2;
                y = workArea.Y + (workArea.Height - h) / 2;
                
                Debug.WriteLine($"Rescued off-screen window: {winInfo.Title}. Repositioned to ({x}, {y})");
            }

            placement.rcNormalPosition = new Win32.RECT
            {
                Left = x,
                Top = y,
                Right = x + w,
                Bottom = y + h
            };

            if (winInfo.State == "maximized")
            {
                placement.showCmd = Win32.SW_SHOWMAXIMIZED;
            }
            else if (winInfo.State == "minimized")
            {
                placement.showCmd = Win32.SW_SHOWMINIMIZED;
            }
            else
            {
                placement.showCmd = Win32.SW_SHOWNORMAL;
            }

            Win32.SetWindowPlacement(hWnd, ref placement);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to restore window {winInfo.Title}: {ex.Message}");
        }
    }
}
