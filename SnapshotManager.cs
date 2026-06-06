using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace snapback_layout;

public static class SnapshotManager
{
    private const int MaxCacheSize = 12;
    private static readonly string SnapshotsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "snapshots");
    private static readonly List<SnapshotCacheItem> _snapshotCache = new();
    private static readonly object _cacheLock = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string GetSnapshotsDirectory() => SnapshotsDir;

    public static void InitializeCache()
    {
        lock (_cacheLock)
        {
            _snapshotCache.Clear();
            if (!Directory.Exists(SnapshotsDir)) return;
            var dir = new DirectoryInfo(SnapshotsDir);
            var files = dir.GetFiles("snapshot_*.json").OrderByDescending(f => f.CreationTime).Take(MaxCacheSize).ToArray();
            foreach (var file in files)
            {
                Snapshot? snapshot = null;
                try
                {
                    string json = File.ReadAllText(file.FullName);
                    snapshot = JsonSerializer.Deserialize<Snapshot>(json, _jsonOptions);
                }
                catch { }

                _snapshotCache.Add(new SnapshotCacheItem
                {
                    FullName = file.FullName,
                    Name = file.Name,
                    CreationTime = file.CreationTime,
                    Snapshot = snapshot
                });
            }
        }
    }

    public static List<SnapshotCacheItem> GetCachedSnapshots()
    {
        lock (_cacheLock)
        {
            return new List<SnapshotCacheItem>(_snapshotCache);
        }
    }

    public static void SaveSnapshot(Settings settings)
    {
        try
        {
            if (!Directory.Exists(SnapshotsDir))
            {
                Directory.CreateDirectory(SnapshotsDir);
            }

            var snapshot = CaptureCurrentLayout();
            string filename = $"snapshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.json";
            string path = Path.Combine(SnapshotsDir, filename);

            string json = JsonSerializer.Serialize(snapshot, _jsonOptions);
            File.WriteAllText(path, json);

            var fileInfo = new FileInfo(path);
            lock (_cacheLock)
            {
                _snapshotCache.Insert(0, new SnapshotCacheItem
                {
                    FullName = fileInfo.FullName,
                    Name = fileInfo.Name,
                    CreationTime = fileInfo.CreationTime,
                    Snapshot = snapshot
                });
                
                if (_snapshotCache.Count > MaxCacheSize)
                {
                    var itemsToRemove = _snapshotCache.GetRange(MaxCacheSize, _snapshotCache.Count - MaxCacheSize);
                    _snapshotCache.RemoveRange(MaxCacheSize, _snapshotCache.Count - MaxCacheSize);
                    
                    foreach (var item in itemsToRemove)
                    {
                        try
                        {
                            if (File.Exists(item.FullName))
                            {
                                File.Delete(item.FullName);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to delete overflow snapshot file: {ex.Message}");
                        }
                    }
                }
            }

            PruneOldSnapshots(settings.HistoryLimitMinutes);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save snapshot: {ex.Message}");
        }
    }

    public static void RestoreLatestSnapshot(Settings settings)
    {
        var latest = GetCachedSnapshots().FirstOrDefault();
        if (latest != null)
        {
            RestoreSnapshot(latest.FullName, settings);
        }
    }

    public static FileInfo[] GetSortedSnapshots()
    {
        if (!Directory.Exists(SnapshotsDir)) return Array.Empty<FileInfo>();
        var dir = new DirectoryInfo(SnapshotsDir);
        return dir.GetFiles("snapshot_*.json").OrderByDescending(f => f.CreationTime).ToArray();
    }

    public static void PruneOldSnapshots(int limitMinutes)
    {
        try
        {
            if (!Directory.Exists(SnapshotsDir)) return;
            var files = GetSortedSnapshots();
            var threshold = DateTime.Now.AddMinutes(-limitMinutes);

            foreach (var file in files)
            {
                if (file.CreationTime < threshold)
                {
                    file.Delete();
                }
            }

            // Sync cache with current files
            lock (_cacheLock)
            {
                _snapshotCache.RemoveAll(item => !File.Exists(item.FullName));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to prune old snapshots: {ex.Message}");
        }
    }

    public static Snapshot CaptureCurrentLayout()
    {
        var snapshot = new Snapshot
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            Monitors = WindowEnumerator.EnumerateMonitors()
        };

        var screens = Screen.AllScreens;
        snapshot.Windows = WindowEnumerator.EnumerateWindows(screens);

        return snapshot;
    }

    public static void RestoreSnapshot(string snapshotPath, Settings settings)
    {
        try
        {
            if (!File.Exists(snapshotPath)) return;

            string json = File.ReadAllText(snapshotPath);
            var snapshot = JsonSerializer.Deserialize<Snapshot>(json, _jsonOptions);
            if (snapshot == null) return;

            RestoreEngine.RestoreLayout(snapshot, settings);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to restore snapshot: {ex.Message}");
        }
    }
}
