using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows.Forms;

namespace snapback_layout;

public class AppContext : ApplicationContext
{
    private readonly ToolStripMenuItem _historyMenuItem;
    private readonly ToolStripMenuItem _saveMenuItem;
    private readonly ToolStripMenuItem _restoreMenuItem;
    private readonly System.Windows.Forms.Timer _autoSaveTimer;
    private readonly HotkeyWindow _hotkeyWindow;
    private Settings _settings;
    private IntPtr _hIcon = IntPtr.Zero;

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ContextMenuStrip _historyContextMenu;

    public AppContext()
    {
        SnapshotManager.InitializeCache();
        _settings = Settings.Load();
        ApplyStartupSetting(_settings.StartWithWindows);

        // 1. Setup Hotkeys via hidden window
        _hotkeyWindow = new HotkeyWindow(OnSaveLayout, OnRestoreLayout, _settings);

        // 2. Setup Context Menu
        _contextMenu = new ContextMenuStrip();
        _historyContextMenu = new ContextMenuStrip();
        
        _saveMenuItem = new ToolStripMenuItem("Save Layout", null, (s, e) => OnSaveLayout());
        _restoreMenuItem = new ToolStripMenuItem("Restore Layout", null, (s, e) => OnRestoreLayout());
        UpdateMenuTexts();
        
        _historyMenuItem = new ToolStripMenuItem("History");
        _contextMenu.Opening += ContextMenu_Opening;

        var settingsItem = new ToolStripMenuItem("Settings...", null, (s, e) => OnShowSettings());
        var exitItem = new ToolStripMenuItem("Exit", null, (s, e) => ExitThread());

        _contextMenu.Items.AddRange(new ToolStripItem[] {
            _saveMenuItem,
            _restoreMenuItem,
            _historyMenuItem,
            new ToolStripSeparator(),
            settingsItem,
            exitItem
        });

        // 3. Setup Notify Icon
        _notifyIcon = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            ContextMenuStrip = _contextMenu,
            Text = "snapback-layout",
            Visible = true
        };

        // Double click tray icon restores the latest layout
        _notifyIcon.DoubleClick += (s, e) => OnRestoreLayout();
        _notifyIcon.MouseUp += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowHistoryOnlyMenu();
            }
        };

        // 4. Setup Auto-Save Timer
        _autoSaveTimer = new System.Windows.Forms.Timer();
        _autoSaveTimer.Tick += AutoSaveTimer_Tick;
        UpdateTimerSettings();

        // Check if registration succeeded on launch
        if (!_hotkeyWindow.IsRegisteredSuccessfully)
        {
            _notifyIcon.ShowBalloonTip(3000, "Hotkey Conflict", 
                "Failed to register hotkeys. They might be in use by another app. Please change them in Settings.", 
                ToolTipIcon.Warning);
        }
        else
        {
            _notifyIcon.ShowBalloonTip(2000, "snapback-layout is running", 
                "Access settings or restore layouts from the system tray icon.", 
                ToolTipIcon.Info);
        }

        // Perform initial save on start
        OnSaveLayout(silent: true);
    }

    private void UpdateTimerSettings()
    {
        _autoSaveTimer.Stop();
        if (_settings.AutoSaveEnabled && _settings.AutoSaveIntervalMinutes > 0)
        {
            _autoSaveTimer.Interval = _settings.AutoSaveIntervalMinutes * 60 * 1000;
            _autoSaveTimer.Start();
        }
    }

    private void AutoSaveTimer_Tick(object? sender, EventArgs e)
    {
        OnSaveLayout(silent: true);
    }

    private void OnSaveLayout()
    {
        OnSaveLayout(silent: false);
    }

    private void OnSaveLayout(bool silent)
    {
        SnapshotManager.SaveSnapshot(_settings);
        if (!silent)
        {
            _notifyIcon.ShowBalloonTip(1500, "Layout Saved", "Current window layout has been captured.", ToolTipIcon.Info);
        }
    }

    private void OnRestoreLayout()
    {
        SnapshotManager.RestoreLatestSnapshot(_settings);
        _notifyIcon.ShowBalloonTip(1500, "Layout Restored", "Latest layout configuration has been applied.", ToolTipIcon.Info);
    }

    private void OnShowSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            bool startupChanged = _settings.StartWithWindows != form.UpdatedSettings.StartWithWindows;
            bool hotkeysChanged = _settings.SaveHotkeyModifiers != form.UpdatedSettings.SaveHotkeyModifiers ||
                                  _settings.SaveHotkeyKey != form.UpdatedSettings.SaveHotkeyKey ||
                                  _settings.RestoreHotkeyModifiers != form.UpdatedSettings.RestoreHotkeyModifiers ||
                                  _settings.RestoreHotkeyKey != form.UpdatedSettings.RestoreHotkeyKey;

            _settings = form.UpdatedSettings;
            _settings.Save();
            
            if (startupChanged)
            {
                ApplyStartupSetting(_settings.StartWithWindows);
            }

            if (hotkeysChanged)
            {
                bool ok = _hotkeyWindow.UpdateHotkeys(_settings.SaveHotkeyModifiers, _settings.SaveHotkeyKey, _settings.RestoreHotkeyModifiers, _settings.RestoreHotkeyKey);
                UpdateMenuTexts();
                if (!ok)
                {
                    MessageBox.Show("One or both hotkeys could not be registered. They might be in use by another application.", 
                        "Hotkey Conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            
            UpdateTimerSettings();
        }
    }

    private void UpdateMenuTexts()
    {
        _saveMenuItem.Text = $"Save Layout ({HotkeyHelper.Format(_settings.SaveHotkeyModifiers, (Keys)_settings.SaveHotkeyKey)})";
        _restoreMenuItem.Text = $"Restore Layout ({HotkeyHelper.Format(_settings.RestoreHotkeyModifiers, (Keys)_settings.RestoreHotkeyKey)})";
    }

    private void ApplyStartupSetting(bool enable)
    {
        try
        {
            string appName = "snapback-layout";
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                if (enable)
                {
                    key.SetValue(appName, $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue(appName, false);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set startup registry: {ex.Message}");
        }
    }

    private void ContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        // Dispose existing items to prevent GDI resource leaks
        while (_historyMenuItem.DropDownItems.Count > 0)
        {
            var item = _historyMenuItem.DropDownItems[0];
            _historyMenuItem.DropDownItems.RemoveAt(0);
            item.Dispose();
        }

        var cachedSnapshots = SnapshotManager.GetCachedSnapshots();
        if (cachedSnapshots.Count == 0)
        {
            _historyMenuItem.DropDownItems.Add(new ToolStripMenuItem("No snapshots found") { Enabled = false });
        }
        else
        {
            foreach (var item in cachedSnapshots)
            {
                _historyMenuItem.DropDownItems.Add(CreateSnapshotMenuItem(item));
            }
        }
    }

    private void ShowHistoryOnlyMenu()
    {
        // Clear existing items to prevent duplicates/leaks
        while (_historyContextMenu.Items.Count > 0)
        {
            var item = _historyContextMenu.Items[0];
            _historyContextMenu.Items.RemoveAt(0);
            item.Dispose();
        }
        
        var cachedSnapshots = SnapshotManager.GetCachedSnapshots();
        if (cachedSnapshots.Count == 0)
        {
            _historyContextMenu.Items.Add(new ToolStripMenuItem("No snapshots found") { Enabled = false });
        }
        else
        {
            foreach (var item in cachedSnapshots)
            {
                _historyContextMenu.Items.Add(CreateSnapshotMenuItem(item));
            }
        }

        _historyContextMenu.Show(Cursor.Position);
    }

    private ToolStripMenuItem CreateSnapshotMenuItem(SnapshotCacheItem item)
    {
        string displayLabel = item.Name;
        if (item.Name.StartsWith("snapshot_") && item.Name.EndsWith(".json") && item.Name.Length >= 24)
        {
            string datePart = item.Name.Substring(9, item.Name.Length - 9 - 5);
            if (DateTime.TryParseExact(datePart, "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.None, out DateTime dt) ||
                DateTime.TryParseExact(datePart, "yyyyMMdd_HHmmss_fff", null, System.Globalization.DateTimeStyles.None, out dt))
            {
                displayLabel = dt.ToString("HH:mm");
            }
        }

        string displayName = displayLabel;
        var snapshot = item.Snapshot;
        if (snapshot != null && snapshot.Windows != null)
        {
            var foregroundWin = snapshot.Windows.FirstOrDefault(w => w.IsForeground);
            if (foregroundWin == null || string.IsNullOrEmpty(foregroundWin.Title))
            {
                foregroundWin = snapshot.Windows.FirstOrDefault(w => !string.IsNullOrEmpty(w.Title));
            }

            int otherWindowsCount = snapshot.Windows.Count(w => !string.IsNullOrEmpty(w.Title)) - 1;
            if (otherWindowsCount < 0) otherWindowsCount = 0;

            if (foregroundWin != null)
            {
                string processTag = string.IsNullOrEmpty(foregroundWin.ProcessName) ? "" : $"[{foregroundWin.ProcessName}] ";
                string activeTitle = foregroundWin.Title;
                if (activeTitle.Length > 25) activeTitle = activeTitle.Substring(0, 22) + "...";
                
                string countTag = otherWindowsCount > 0 ? $" (+{otherWindowsCount})" : "";
                displayName = $"{displayLabel} - Active: {processTag}{activeTitle}{countTag}";
            }
            else
            {
                var appNames = snapshot.Windows
                    .Select(w => w.ProcessName)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .ToList();

                if (appNames.Count > 0)
                {
                    displayName = $"{displayLabel} ({string.Join(", ", appNames)})";
                }
            }
        }

        var snapshotItem = new ToolStripMenuItem(displayName);
        snapshotItem.Click += (s, ev) => RestoreSnapshotAction(item.FullName, displayLabel);
        return snapshotItem;
    }

    private void RestoreSnapshotAction(string path, string displayName)
    {
        SnapshotManager.RestoreSnapshot(path, _settings);
        _notifyIcon.ShowBalloonTip(1500, "Layout Restored", $"Restored layout from {displayName}.", ToolTipIcon.Info);
    }

    private Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Draw a premium purple/blue gradient circle representing snapback
        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
            new Rectangle(0, 0, 16, 16),
            Color.FromArgb(124, 58, 237), // Violet
            Color.FromArgb(59, 130, 246),  // Blue
            45f);
        g.FillEllipse(brush, 1, 1, 14, 14);

        using var pen = new Pen(Color.White, 1.5f);
        // Draw circular arrow representation
        g.DrawArc(pen, 3, 3, 10, 10, 45, 270);
        g.FillPolygon(Brushes.White, new PointF[] {
            new PointF(10, 3),
            new PointF(13, 5),
            new PointF(10, 8)
        });

        _hIcon = bitmap.GetHicon();
        return Icon.FromHandle(_hIcon);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Dispose();
            _autoSaveTimer.Dispose();
            _hotkeyWindow.Dispose();
            _contextMenu.Dispose();
            _historyContextMenu.Dispose();
            
            if (_hIcon != IntPtr.Zero)
            {
                Win32.DestroyIcon(_hIcon);
            }
        }
        base.Dispose(disposing);
    }

    private class HotkeyWindow : NativeWindow, IDisposable
    {
        private readonly Action _onSave;
        private readonly Action _onRestore;

        public bool IsRegisteredSuccessfully { get; private set; } = true;

        public HotkeyWindow(Action onSave, Action onRestore, Settings settings)
        {
            _onSave = onSave;
            _onRestore = onRestore;
            
            // Create handle for the window to receive messages
            var cp = new CreateParams();
            this.CreateHandle(cp);

            UpdateHotkeys(settings.SaveHotkeyModifiers, settings.SaveHotkeyKey, settings.RestoreHotkeyModifiers, settings.RestoreHotkeyKey);
        }

        public bool UpdateHotkeys(uint saveModifiers, uint saveKey, uint restoreModifiers, uint restoreKey)
        {
            Win32.UnregisterHotKey(this.Handle, 1);
            Win32.UnregisterHotKey(this.Handle, 2);

            bool saveOk = true;
            bool restoreOk = true;

            if (saveKey != 0)
            {
                saveOk = Win32.RegisterHotKey(this.Handle, 1, saveModifiers, saveKey);
            }
            if (restoreKey != 0)
            {
                restoreOk = Win32.RegisterHotKey(this.Handle, 2, restoreModifiers, restoreKey);
            }

            IsRegisteredSuccessfully = saveOk && restoreOk;
            return IsRegisteredSuccessfully;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312) // WM_HOTKEY
            {
                int id = m.WParam.ToInt32();
                if (id == 1) _onSave();
                else if (id == 2) _onRestore();
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            Win32.UnregisterHotKey(this.Handle, 1);
            Win32.UnregisterHotKey(this.Handle, 2);
            this.DestroyHandle();
            GC.SuppressFinalize(this);
        }
    }
}
