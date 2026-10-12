using TheTimer.Core;

namespace TheTimer.Core.Tests;

public class AppPathsTests
{
    private const string Home = "/home/user";

    private static Func<string, string?> Env(string? xdgDataHome) =>
        name => name == "XDG_DATA_HOME" ? xdgDataHome : null;

    [Fact]
    public void UsesXdgDataHomeWhenSet()
    {
        var paths = AppPaths.Create(Env("/custom/data"), Home);

        Assert.Equal("/custom/data/thetimer", paths.DataDirectory);
        Assert.Equal("/custom/data/thetimer/thetimer.db", paths.DatabasePath);
    }

    [Fact]
    public void FallsBackToHomeWhenXdgUnset()
    {
        var paths = AppPaths.Create(Env(null), Home);

        Assert.Equal("/home/user/.local/share/thetimer", paths.DataDirectory);
        Assert.Equal("/home/user/.local/share/thetimer/thetimer.db", paths.DatabasePath);
    }

    [Fact]
    public void FallsBackToHomeWhenXdgEmpty()
    {
        var paths = AppPaths.Create(Env(""), Home);

        Assert.Equal("/home/user/.local/share/thetimer", paths.DataDirectory);
    }

    [Fact]
    public void FallsBackToHomeWhenXdgRelative()
    {
        var paths = AppPaths.Create(Env("relative/path"), Home);

        Assert.Equal("/home/user/.local/share/thetimer", paths.DataDirectory);
    }
}
