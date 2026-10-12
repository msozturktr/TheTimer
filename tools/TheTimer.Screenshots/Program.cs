using Avalonia;
using Avalonia.Headless;
using Avalonia.Skia;
using Avalonia.Threading;
using TheTimer.App;
using TheTimer.App.ViewModels;
using TheTimer.App.Views;
using TheTimer.Core;
using TheTimer.Core.Planning;
using TheTimer.Core.Pomodoro;
using TheTimer.Core.Sessions;
using TheTimer.Screenshots;

// Usage: dotnet run --project tools/TheTimer.Screenshots -- <outDir> [scenario...]
// Renders 1200x780 PNGs of the app with deterministic demo data (fake clock 2026-10-12 15:32, Europe/Istanbul).
const string ZoneId = "Europe/Istanbul";
Environment.SetEnvironmentVariable("TZ", ZoneId); // view models read TimeZoneInfo.Local
TimeZoneInfo.ClearCachedData();

var allScenarios = new[] { "timer-idle", "timer-running-pomodoro", "timer-running-stopwatch", "timer-countdown", "plan", "plan-editor", "today", "today-recovery", "timer-recovery", "report" };
if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: TheTimer.Screenshots <outDir> [scenario...]");
    Console.Error.WriteLine("Scenarios: " + string.Join(", ", allScenarios));
    return 2;
}

// Optional "--size WxH" (default 1200x780); the size is appended to the file name when given.
var sizeIndex = Array.IndexOf(args, "--size");
var width = 1200;
var height = 780;
string? sizeSuffix = null;
if (sizeIndex >= 0)
{
    var parts = sizeIndex + 1 < args.Length ? args[sizeIndex + 1].Split('x') : [];
    if (parts.Length != 2 || !int.TryParse(parts[0], out width) || !int.TryParse(parts[1], out height))
    {
        Console.Error.WriteLine("Usage: --size WxH");
        return 2;
    }

    sizeSuffix = $"-{width}x{height}";
    args = args.Where((_, i) => i != sizeIndex && i != sizeIndex + 1).ToArray();
}

var outDir = Path.GetFullPath(args[0]);
var scenarios = args.Length > 1 ? args[1..] : allScenarios;
foreach (var name in scenarios)
{
    if (!allScenarios.Contains(name))
    {
        Console.Error.WriteLine($"Unknown scenario: {name}");
        return 2;
    }
}

Directory.CreateDirectory(outDir);
AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

var zone = TimeZoneInfo.FindSystemTimeZoneById(ZoneId);
foreach (var name in scenarios)
{
    var path = Path.Combine(outDir, name + sizeSuffix + ".png");
    Render(name, path, zone, width, height);
    Console.WriteLine(path);
}

return 0;

static void Render(string scenario, string path, TimeZoneInfo zone, int width, int height)
{
    var start = new DateTimeOffset(DemoData.Today.ToDateTime(DemoData.Now), zone.GetUtcOffset(DemoData.Today.ToDateTime(DemoData.Now)));
    var clock = new FakeTimeProvider(start, zone);
    var sessions = new InMemorySessionRepository();
    var activeStore = new InMemoryActiveSessionStore();
    var plans = new InMemoryPlanRepository();
    DemoData.Seed(sessions, plans, zone);

    if (scenario is "today-recovery" or "timer-recovery")
    {
        var startedAt = start - TimeSpan.FromMinutes(24);
        activeStore.Save(new ActiveSessionRecord(
            new TimerSnapshot("Tez raporu — 3. bölüm", "yazım", startedAt, TimeSpan.Zero, startedAt, TimeSpan.FromMinutes(25)),
            start - TimeSpan.FromMinutes(5), "Pomodoro", false, PomodoroPhase.Work, 2));
    }

    var vm = AppComposition.Create(sessions, activeStore, plans, clock, new NoOpNotifier());
    var window = new MainWindow { DataContext = vm, Width = width, Height = height };
    window.Show();
    Pump();

    switch (scenario)
    {
        case "timer-running-pomodoro":
            vm.Mode = TimerMode.Pomodoro;
            vm.TaskName = "Tez raporu — 3. bölüm";
            vm.Tag = "yazım";
            clock.Advance(TimeSpan.FromSeconds(-(9 * 60 + 31)));
            vm.StartCommand.Execute(null);
            clock.Advance(TimeSpan.FromSeconds(9 * 60 + 31));
            Pump(800);
            break;
        case "timer-running-stopwatch":
            vm.Mode = TimerMode.Stopwatch;
            vm.TaskName = "Kod inceleme";
            vm.Tag = "yazılım";
            clock.Advance(TimeSpan.FromSeconds(-(72 * 60 + 30)));
            vm.StartCommand.Execute(null);
            clock.Advance(TimeSpan.FromSeconds(72 * 60 + 30));
            Pump(800);
            break;
        case "timer-countdown":
            vm.Mode = TimerMode.Countdown;
            vm.TaskName = "Literatür okuma";
            vm.Tag = "araştırma";
            vm.CountdownMinutes = 45;
            clock.Advance(TimeSpan.FromSeconds(-(17 * 60 + 20)));
            vm.StartCommand.Execute(null);
            clock.Advance(TimeSpan.FromSeconds(17 * 60 + 20));
            Pump(800);
            break;
        case "plan":
            vm.SelectedTabIndex = 1;
            break;
        case "plan-editor":
            vm.SelectedTabIndex = 1;
            Pump();
            var recurring = vm.Plan!.Items.First(i => i.IsRecurring);
            recurring.EditCommand.Execute(null);
            break;
        case "today":
        case "today-recovery":
            vm.SelectedTabIndex = 2;
            break;
        case "report":
            vm.SelectedTabIndex = 3;
            Pump();
            vm.Report!.PreviousWeekCommand.Execute(null);
            break;
    }

    Pump();
    using var bitmap = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame captured.");
    bitmap.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    window.Close();
}

static void Pump(int waitMs = 0)
{
    // Dispatcher + render rounds so layout and bindings settle; waitMs runs the real message loop (view-model ticker).
    for (var i = 0; i < 4; i++)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    if (waitMs > 0)
    {
        using var cts = new CancellationTokenSource();
        _ = Task.Delay(waitMs).ContinueWith(_ => cts.Cancel());
        Dispatcher.UIThread.MainLoop(cts.Token);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
