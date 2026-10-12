namespace TheTimer.Core;

/// <summary>Resolves application data locations following the XDG base directory spec.</summary>
public sealed class AppPaths
{
    private const string AppDirectoryName = "thetimer";
    private const string DatabaseFileName = "thetimer.db";

    public AppPaths(string dataDirectory)
    {
        DataDirectory = dataDirectory;
    }

    public string DataDirectory { get; }

    public string DatabasePath => Path.Combine(DataDirectory, DatabaseFileName);

    /// <summary>Creates paths from the real process environment.</summary>
    public static AppPaths FromEnvironment() =>
        Create(Environment.GetEnvironmentVariable,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>Creates paths from an environment lookup and a home directory. Touches no filesystem.</summary>
    public static AppPaths Create(Func<string, string?> getEnvironmentVariable, string homeDirectory)
    {
        var xdgDataHome = getEnvironmentVariable("XDG_DATA_HOME");
        var baseDirectory = string.IsNullOrEmpty(xdgDataHome) || !Path.IsPathRooted(xdgDataHome)
            ? Path.Combine(homeDirectory, ".local", "share")
            : xdgDataHome;
        return new AppPaths(Path.Combine(baseDirectory, AppDirectoryName));
    }
}
