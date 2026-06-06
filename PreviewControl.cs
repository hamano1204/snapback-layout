using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace snapback_layout;

public class PreviewControl : UserControl
{
    private readonly Snapshot _snapshot;

    public PreviewControl(Snapshot snapshot)
    {
        _snapshot = snapshot;
        this.BackColor = Color.FromArgb(24, 24, 27); // Dark zinc
        this.DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_snapshot == null || _snapshot.Monitors.Count == 0)
        {
            using var brush = new SolidBrush(Color.Gray);
            e.Graphics.DrawString("No Preview Available", this.Font ?? SystemFonts.DefaultFont, brush, 10, 10);
            return;
        }

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Calculate virtual desktop bounds enclosing all monitors
        int minX = _snapshot.Monitors.Min(m => m.Bounds.X);
        int minY = _snapshot.Monitors.Min(m => m.Bounds.Y);
        int maxX = _snapshot.Monitors.Max(m => m.Bounds.X + m.Bounds.Width);
        int maxY = _snapshot.Monitors.Max(m => m.Bounds.Y + m.Bounds.Height);

        int totalWidth = maxX - minX;
        int totalHeight = maxY - minY;

        if (totalWidth <= 0 || totalHeight <= 0) return;

        // Determine scaling factor to fit preview inside size
        int padding = 15;
        int targetW = this.Width - (padding * 2);
        int targetH = this.Height - (padding * 2);

        double scaleX = (double)targetW / totalWidth;
        double scaleY = (double)targetH / totalHeight;
        double scale = Math.Min(scaleX, scaleY);

        // Center preview
        int offsetX = padding + (int)((targetW - (totalWidth * scale)) / 2);
        int offsetY = padding + (int)((targetH - (totalHeight * scale)) / 2);

        // Draw monitors
        foreach (var monitor in _snapshot.Monitors)
        {
            int mx = offsetX + (int)((monitor.Bounds.X - minX) * scale);
            int my = offsetY + (int)((monitor.Bounds.Y - minY) * scale);
            int mw = (int)(monitor.Bounds.Width * scale);
            int mh = (int)(monitor.Bounds.Height * scale);

            // Draw screen boundary
            using var monitorPen = new Pen(Color.FromArgb(63, 63, 70), 2); // gray boundary
            using var monitorBg = new SolidBrush(Color.FromArgb(9, 9, 11)); // dark screen
            e.Graphics.FillRectangle(monitorBg, mx, my, mw, mh);
            e.Graphics.DrawRectangle(monitorPen, mx, my, mw, mh);

        }

        // Draw Windows
        // Win32.EnumWindows returns windows from top to bottom (Z-order).
        // Therefore, ZIndex = 1 is the topmost, ZIndex = 2 is underneath it, etc.
        // To draw them on the canvas so that topmost windows stack ON TOP of background ones,
        // we must draw in reverse order (bottom-most windows first, topmost windows last).
        var sortedWindows = _snapshot.Windows.OrderByDescending(w => w.ZIndex).ToList();
        foreach (var win in sortedWindows)
        {
            if (win.Bounds.Width <= 0 || win.Bounds.Height <= 0) continue;
            if (win.State == WindowState.Minimized) continue;

            int wx = offsetX + (int)((win.Bounds.X - minX) * scale);
            int wy = offsetY + (int)((win.Bounds.Y - minY) * scale);
            int ww = (int)(win.Bounds.Width * scale);
            int wh = (int)(win.Bounds.Height * scale);

            // Clip boundaries to the virtual canvas
            if (wx + ww < offsetX || wx > offsetX + targetW || wy + wh < offsetY || wy > offsetY + targetH)
                continue;

            // Coloring based on active vs normal
            Color winColor = win.IsForeground 
                ? Color.FromArgb(124, 58, 237) // Brand Purple for active
                : Color.FromArgb(59, 130, 246);  // Blue for others
            
            // Draw window background solid (no alpha transparency) to clearly visualize Z-order stacking
            using var winBg = new SolidBrush(winColor);
            using var winBorder = new Pen(Color.FromArgb(24, 24, 27), 1f); // Dark border to separate overlapping solid windows
            
            e.Graphics.FillRectangle(winBg, wx, wy, ww, wh);
            e.Graphics.DrawRectangle(winBorder, wx, wy, ww, wh);

            // Draw application abbreviation or name if big enough
            if (ww > 32 && wh > 12)
            {
                string processAbbrev = win.ProcessName.Length > 7 ? win.ProcessName.Substring(0, 6) : win.ProcessName;
                using var labelBrush = new SolidBrush(Color.White);
                using var labelFont = new Font("Segoe UI", 6.5F, FontStyle.Regular);
                e.Graphics.DrawString(processAbbrev, labelFont, labelBrush, wx + 1, wy + 1);
            }
        }
    }
}
