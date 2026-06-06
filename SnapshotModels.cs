using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace snapback_layout;

public class Snapshot
{
    public string Timestamp { get; set; } = string.Empty;
    public List<MonitorInfo> Monitors { get; set; } = new();
    public List<WindowInfo> Windows { get; set; } = new();
}

public class MonitorInfo
{
    public int Id { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public Bounds Bounds { get; set; } = new();
    public int Dpi { get; set; }
}

public class WindowInfo
{
    public long Hwnd { get; set; }
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string State { get; set; } = "normal"; // normal, maximized, minimized
    public Bounds Bounds { get; set; } = new();
    public int ZIndex { get; set; }
    public int MonitorId { get; set; }
}

public class Bounds
{
    public int X { get; set; }
    public int Y { get; set; }
    
    [JsonPropertyName("w")]
    public int Width { get; set; }
    
    [JsonPropertyName("h")]
    public int Height { get; set; }
}

public class SnapshotCacheItem
{
    public string FullName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreationTime { get; set; }
    public Snapshot? Snapshot { get; set; }
}
