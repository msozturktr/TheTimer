using TheTimer.Core.Settings;

namespace TheTimer.Core.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "thetimer-settings-" + Guid.NewGuid());

    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults() =>
        Assert.Equal(new AppSettings(), new JsonSettingsStore(FilePath).Load());

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var settings = new AppSettings { SoundEnabled = false, Sound = "gong" };
        new JsonSettingsStore(FilePath).Save(settings);
        Assert.Equal(settings, new JsonSettingsStore(FilePath).Load());
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");
        Assert.Equal(new AppSettings(), new JsonSettingsStore(FilePath).Load());
    }
}
