using System.Diagnostics;
using System.Text.Json;

namespace TheTimer.Core.Settings;

/// <summary>Loads and saves <see cref="AppSettings"/>; a missing or corrupt file yields defaults and never throws.</summary>
public sealed class JsonSettingsStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public AppSettings Load()
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to load settings: {ex.Message}");
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}
