using System;
using System.IO;
using System.Text.Json;

namespace snapback_layout;

public class Settings
{
    private static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "settings.json");

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public bool AutoSaveEnabled { get; set; } = true;
    public int AutoSaveIntervalMinutes { get; set; } = 5;
    public int HistoryLimitMinutes { get; set; } = 60;
    public bool DpiCorrectionEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public uint SaveHotkeyModifiers { get; set; } = 3; // MOD_CONTROL (2) | MOD_ALT (1)
    public uint SaveHotkeyKey { get; set; } = 83;      // Keys.S (83)
    public uint RestoreHotkeyModifiers { get; set; } = 3; // MOD_CONTROL (2) | MOD_ALT (1)
    public uint RestoreHotkeyKey { get; set; } = 82;    // Keys.R (82)

    public static Settings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<Settings>(json, _jsonOptions);
                if (settings != null) return settings;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            try
            {
                string backupPath = ConfigPath + ".bak";
                if (File.Exists(ConfigPath))
                {
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                    File.Move(ConfigPath, backupPath);
                    System.Diagnostics.Debug.WriteLine($"Corrupted settings file moved to {backupPath}");
                }
            }
            catch (Exception moveEx)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to backup corrupted settings: {moveEx.Message}");
            }
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(ConfigDir))
            {
                Directory.CreateDirectory(ConfigDir);
            }
            string json = JsonSerializer.Serialize(this, _jsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
}
