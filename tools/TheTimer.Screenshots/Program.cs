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

var allScenarios = new[] { "timer-idle", "timer-running-pomodoro", "plan", "plan-editor", "today", "today-recovery", "timer-recovery", "report", "report-empty", "today-empty", "today-running" };
if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: TheTimer.Screenshots <outDir> [scenario...]");
    Console.Error.WriteLine("Scenarios: " + string.Join(", ", allScenarios));
    return 2;
}

// Optional "--size WxH" (default 1200x780) may appear anywhere after the output directory.
var width = 1200;
var height = 780;
var rest = new List<string>(args[1..]);
var sizeAt = rest.IndexOf("--size");
if (sizeAt >= 0 && sizeAt + 1 < rest.Count)
{
    var parts = rest[sizeAt + 1].Split('x');
    if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
        (width, height) = (w, h);
    rest.RemoveRange(sizeAt, 2);
}

var outDir = Path.GetFullPath(args[0]);
var scenarios = rest.Count > 0 ? rest.ToArray() : allScenarios;
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
    var path = Path.Combine(outDir, name + ".png");
    Render(name, path, zone, width, height);
    Console.WriteLine(path);
}

return 0;

static void Render(string scenario, string path, TimeZoneInfo zone, int width, int height)
{
    var start = new DateTimeOffset(DemoData.Today.ToDateTime(DemoData.Now), zone.GetUtcOffset(DemoData.Today.ToDateTime(DemoData.Now)));
    if (scenario is "report" or "report-empty")
        start = DemoData.ReportNow(zone); // a Friday inside the seeded week so "today" sits mid-week
    var clock = new FakeTimeProvider(start, zone);
    var sessions = new InMemorySessionRepository();
    var activeStore = new InMemoryActiveSessionStore();
    var plans = new InMemoryPlanRepository();
    if (scenario != "today-empty")
        DemoData.Seed(sessions, plans, zone);
    if (scenario is "report" or "report-empty")
        DemoData.SeedReportWeek(sessions, plans, zone);

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
        case "plan":
            vm.SelectedTabIndex = 1;
            break;
        case "plan-editor":
            vm.SelectedTabIndex = 1;
            Pump();
            var recurring = vm.Plan!.Items.First(i => i.IsRecurring);
            recurring.EditCommand.Execute(null);
            break;
        case "today-running":
            vm.SelectedTabIndex = 2;
            vm.TaskName = "Tez raporu — 3. bölüm";
            vm.Tag = "yazım";
            clock.Advance(TimeSpan.FromMinutes(-25));
            vm.StartCommand.Execute(null);
            clock.Advance(TimeSpan.FromMinutes(25));
            Pump(1500);
            break;
        case "today":
        case "today-empty":
        case "today-recovery":
            vm.SelectedTabIndex = 2;
            break;
        case "report":
            vm.SelectedTabIndex = 3;
            Pump();
            break;
        case "report-empty":
            vm.SelectedTabIndex = 3;
            Pump();
            for (var i = 0; i < 4; i++) // before the recurring plan series starts
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
