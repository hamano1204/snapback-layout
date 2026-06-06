using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;

namespace snapback_layout;

public class AppContext : ApplicationContext
{
    private readonly ToolStripMenuItem _saveMenuItem;
    private readonly ToolStripMenuItem _restoreMenuItem;
    private readonly System.Windows.Forms.Timer _autoSaveTimer;
    private readonly HotkeyWindow _hotkeyWindow;
    private Settings _settings;
    private IntPtr _hIcon = IntPtr.Zero;
    private Icon? _trayIcon;
    private Image? _starFavoriteImage;
    private Image? _starNormalImage;

    // Cached Fonts to avoid GDI leaks
    private Font? _menuBoldFont;
    private Font? _menuItalicFont;
    private Font? _menuSubFont;
    private Font? _menuGuideFont;

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
        _historyContextMenu.Closed += ParentDropDown_Closed;
        
        _saveMenuItem = new ToolStripMenuItem("Save Layout", null, (s, e) => OnSaveLayout());
        _restoreMenuItem = new ToolStripMenuItem("Restore Layout", null, (s, e) => OnRestoreLayout());
        UpdateMenuTexts();
        
        var settingsItem = new ToolStripMenuItem("Settings...", null, (s, e) => OnShowSettings());
        var exitItem = new ToolStripMenuItem("Exit", null, (s, e) => ExitThread());

        _contextMenu.Items.AddRange(new ToolStripItem[] {
            _saveMenuItem,
            _restoreMenuItem,
            new ToolStripSeparator(),
            settingsItem,
            exitItem
        });

        // 3. Setup Notify Icon
        _trayIcon = CreateTrayIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
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





    private void ShowHistoryOnlyMenu()
    {
        PopulateHistoryMenu();
        // Force the application's message window/thread to the foreground.
        // This is a known Win32 requirement to ensure the context menu automatically dismisses when clicking away.
        Win32.SetForegroundWindow(_hotkeyWindow.Handle);
        _historyContextMenu.Show(Cursor.Position);
    }

    private void PopulateHistoryMenu()
    {
        // Clear existing items to prevent duplicates/leaks
        while (_historyContextMenu.Items.Count > 0)
        {
            var item = _historyContextMenu.Items[0];
            _historyContextMenu.Items.RemoveAt(0);
            item.Dispose();
        }

        // Initialize cached fonts lazily
        _menuBoldFont ??= new Font(SystemFonts.DefaultFont, FontStyle.Bold);
        _menuItalicFont ??= new Font(SystemFonts.DefaultFont, FontStyle.Italic);
        _menuSubFont ??= new Font(SystemFonts.DefaultFont.FontFamily, 7.5F, FontStyle.Regular);
        _menuGuideFont ??= new Font(SystemFonts.DefaultFont.FontFamily, 8.25F, FontStyle.Regular);

        var now = DateTime.Now;

        // Add visual descriptive header
        var headerTitle = new ToolStripMenuItem("Restore Window Layout History") { Enabled = false };
        headerTitle.Font = _menuBoldFont;
        
        var headerSub = new ToolStripMenuItem("  (Hover to preview layout | Click ★ to pin)") { Enabled = false };
        headerSub.Font = _menuSubFont;
        headerSub.ForeColor = Color.Gray;

        _historyContextMenu.Items.Add(headerTitle);
        _historyContextMenu.Items.Add(headerSub);
        _historyContextMenu.Items.Add(new ToolStripSeparator());
        
        var cachedSnapshots = SnapshotManager.GetCachedSnapshots();
        if (cachedSnapshots.Count == 0)
        {
            _historyContextMenu.Items.Add(new ToolStripMenuItem("No snapshots found") { Enabled = false });
            _historyContextMenu.Items.Add(new ToolStripSeparator());
            
            var guideItem = new ToolStripMenuItem("👉 Save your first layout:") { Enabled = false };
            guideItem.Font = _menuItalicFont;
            
            var guideItem2 = new ToolStripMenuItem("   Right-click this icon & select 'Save Layout'") { Enabled = false };
            guideItem2.Font = _menuGuideFont;
            
            _historyContextMenu.Items.Add(guideItem);
            _historyContextMenu.Items.Add(guideItem2);
        }
        else
        {
            var starred = cachedSnapshots.Where(c => c.IsFavorite).ToList();
            if (starred.Count > 0)
            {
                _historyContextMenu.Items.Add(new ToolStripMenuItem("★ Starred Layouts") { Enabled = false, Font = _menuBoldFont });
                foreach (var item in starred)
                {
                    _historyContextMenu.Items.Add(CreateSnapshotMenuItem(item, now));
                }
                _historyContextMenu.Items.Add(new ToolStripSeparator());
            }

            var recents = cachedSnapshots.Where(c => !c.IsFavorite).ToList();
            if (recents.Count > 0)
            {
                if (starred.Count > 0)
                {
                    _historyContextMenu.Items.Add(new ToolStripMenuItem("Recent Layouts") { Enabled = false, Font = _menuItalicFont });
                }
                foreach (var item in recents)
                {
                    _historyContextMenu.Items.Add(CreateSnapshotMenuItem(item, now));
                }
            }
        }
    }

    private static string GetRelativeTime(DateTime creationTime, DateTime now)
    {
        var span = now - creationTime;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }

    private ToolStripDropDown? _activePreviewDropDown;

    private void ParentDropDown_Closed(object? sender, ToolStripDropDownClosedEventArgs e)
    {
        CloseActivePreview();
    }

    private void CloseActivePreview()
    {
        if (_activePreviewDropDown != null)
        {
            _activePreviewDropDown.Close();
            _activePreviewDropDown.Dispose();
            _activePreviewDropDown = null;
        }
    }

    private ToolStripMenuItem CreateSnapshotMenuItem(SnapshotCacheItem item, DateTime now)
    {
        string timeLabel = item.Name;
        if (item.Name.StartsWith("snapshot_") && item.Name.EndsWith(".json"))
        {
            string nameWithoutExt = Path.GetFileNameWithoutExtension(item.Name);
            string datePart = nameWithoutExt["snapshot_".Length..];
            if (DateTime.TryParseExact(datePart, "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.None, out DateTime dt) ||
                DateTime.TryParseExact(datePart, "yyyyMMdd_HHmmss_fff", null, System.Globalization.DateTimeStyles.None, out dt))
            {
                timeLabel = $"{dt.ToString("HH:mm")} ({GetRelativeTime(item.CreationTime, now)})";
            }
        }

        string displayName = timeLabel;
        var snapshot = item.Snapshot;
        if (snapshot != null && snapshot.Windows != null)
        {
            // Collect workspace processes
            var appNames = snapshot.Windows
                .Select(w => w.ProcessName)
                .Where(n => !string.IsNullOrEmpty(n) && !n.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            string workspaceTag = appNames.Count > 0 ? $" [{string.Join(", ", appNames)}]" : "";

            var foregroundWin = snapshot.Windows.FirstOrDefault(w => w.IsForeground);
            if (foregroundWin == null || string.IsNullOrEmpty(foregroundWin.Title))
            {
                foregroundWin = snapshot.Windows.FirstOrDefault(w => !string.IsNullOrEmpty(w.Title));
            }

            if (foregroundWin != null)
            {
                string activeTitle = foregroundWin.Title;
                if (activeTitle.Length > 20) activeTitle = activeTitle.Substring(0, 17) + "...";
                displayName = $"{timeLabel} - Active: {activeTitle}{workspaceTag}";
            }
            else if (workspaceTag.Length > 0)
            {
                displayName = $"{timeLabel} - Apps:{workspaceTag}";
            }
        }

        // Custom drawn item to support the interactive Star icon
        _starFavoriteImage ??= CreateStarIcon(Color.FromArgb(234, 179, 8)); // Golden filled star
        _starNormalImage ??= CreateStarIcon(Color.FromArgb(156, 163, 175)); // Gray outline star

        var snapshotItem = new ToolStripMenuItem(displayName)
        {
            Image = item.IsFavorite ? _starFavoriteImage : _starNormalImage,
            ImageScaling = ToolStripItemImageScaling.None
        };

        // Listen for mouse movement to toggle favorite via icon click
        snapshotItem.MouseDown += (s, ev) =>
        {
            // The image margin is on the left; click inside the left 32px is treated as a Favorite Toggle click
            if (ev.X >= 0 && ev.X <= 32)
            {
                SnapshotManager.ToggleFavorite(item);
                
                // Re-populate menu items in place to avoid closing/opening
                PopulateHistoryMenu();
            }
            else
            {
                RestoreSnapshotAction(item.FullName, timeLabel);
            }
        };

        // Hover popup preview window setup using ToolStripDropDown (ensures no focus issues)
        snapshotItem.MouseEnter += (s, ev) =>
        {
            if (item.Snapshot != null)
            {
                CloseActivePreview();

                var previewControl = new PreviewControl(item.Snapshot)
                {
                    Size = new Size(320, 200)
                };

                var host = new ToolStripControlHost(previewControl)
                {
                    Padding = Padding.Empty,
                    Margin = Padding.Empty,
                    AutoSize = false
                };

                _activePreviewDropDown = new ToolStripDropDown
                {
                    Padding = Padding.Empty,
                    Margin = Padding.Empty,
                    AutoClose = false,
                    DropShadowEnabled = true
                };
                _activePreviewDropDown.Items.Add(host);

                if (snapshotItem.Owner is ToolStripDropDown parentDropDown)
                {
                    var screenPt = parentDropDown.PointToScreen(new Point(snapshotItem.Bounds.Left, snapshotItem.Bounds.Top));
                    
                    int px = screenPt.X - 320 - 5;
                    if (px < 0) px = screenPt.X + snapshotItem.Bounds.Width + 5;

                    int py = screenPt.Y - (200 / 2) + (snapshotItem.Bounds.Height / 2);

                    _activePreviewDropDown.Show(new Point(px, py));
                }
            }
        };

        snapshotItem.MouseLeave += (s, ev) =>
        {
            CloseActivePreview();
        };

        return snapshotItem;
    }

    private static Bitmap CreateStarIcon(Color color)
    {
        var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(color);
            
            // Draw a simple 5-point star polygon
            PointF[] points = {
                new PointF(8, 1),
                new PointF(10.5f, 6),
                new PointF(16, 6.5f),
                new PointF(12, 10.5f),
                new PointF(13, 16),
                new PointF(8, 13.5f),
                new PointF(3, 16),
                new PointF(4, 10.5f),
                new PointF(0, 6.5f),
                new PointF(5.5f, 6)
            };
            g.FillPolygon(brush, points);
        }
        return bmp;
    }

    private void RestoreSnapshotAction(string path, string displayName)
    {
        SnapshotManager.RestoreSnapshot(path, _settings);
        _notifyIcon.ShowBalloonTip(1500, "Layout Restored", $"Restored layout from {displayName}.", ToolTipIcon.Info);
    }

    private Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
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
        }

        _hIcon = bitmap.GetHicon();
        // Use clone to create a managed Icon instance that takes ownership or is independent,
        // and keep track of _hIcon to destroy it on Dispose (which prevents GDI leak of the native HICON).
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
            
            _trayIcon?.Dispose();
            _starFavoriteImage?.Dispose();
            _starNormalImage?.Dispose();

            // Clean up cached fonts to avoid GDI leak
            _menuBoldFont?.Dispose();
            _menuItalicFont?.Dispose();
            _menuSubFont?.Dispose();
            _menuGuideFont?.Dispose();

            // Destroy native Win32 icon handle
            if (_hIcon != IntPtr.Zero)
            {
                Win32.DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }
        }
        base.Dispose(disposing);
    }
}
