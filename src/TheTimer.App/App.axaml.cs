using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using TheTimer.App.Services;
using TheTimer.App.ViewModels;
using TheTimer.App.Views;
using TheTimer.Core;
using TheTimer.Core.Notifications;
using TheTimer.Core.Sessions;

namespace TheTimer.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var notifier = new FreedesktopNotifier();
            var (repository, databaseError) = CreateRepository();
            var viewModel = new MainWindowViewModel(
                new SessionTimer(TimeProvider.System),
                repository,
                TimeProvider.System,
                notifier);
            if (databaseError)
                _ = NotifyDatabaseErrorAsync(notifier);
            var window = new MainWindow { DataContext = viewModel };
            desktop.MainWindow = window;

            // Idle close ends the app; the window cancels the close itself while a session is active.
            window.Closed += (_, _) => desktop.Shutdown();

            SetupTrayIcon(desktop, window, viewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static (ISessionRepository Repository, bool Failed) CreateRepository()
    {
        try
        {
            return (new SqliteSessionRepository(AppPaths.FromEnvironment().DatabasePath), false);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to open session database: {ex}");
            return (new InMemorySessionRepository(), true);
        }
    }

    private static async Task NotifyDatabaseErrorAsync(INotifier notifier)
    {
        try
        {
            await notifier.NotifyAsync("Veritabanı açılamadı", "Kayıtlar yalnızca bu oturum boyunca tutulacak")
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Notification failed: {ex}");
        }
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, MainWindow window, MainWindowViewModel vm)
    {
        var showItem = new NativeMenuItem("Göster");
        showItem.Click += (_, _) => window.ShowAndActivate();

        var exitItem = new NativeMenuItem("Çık");
        exitItem.Click += (_, _) =>
        {
            vm.StopActiveSession();
            desktop.Shutdown();
        };

        var menu = new NativeMenu
        {
            showItem,
            new NativeMenuItemSeparator(),
            new NativeMenuItem("Başlat") { Command = vm.StartCommand },
            new NativeMenuItem("Duraklat") { Command = vm.PauseCommand },
            new NativeMenuItem("Devam") { Command = vm.ResumeCommand },
            new NativeMenuItem("Durdur") { Command = vm.StopCommand },
            new NativeMenuItemSeparator(),
            exitItem,
        };

        var tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://TheTimer/Assets/thetimer.png"))),
            ToolTipText = vm.TrayToolTip,
            Menu = menu,
        };
        tray.Clicked += (_, _) =>
        {
            if (window.IsVisible && window.WindowState != WindowState.Minimized)
                window.Hide();
            else
                window.ShowAndActivate();
        };
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.TrayToolTip))
                tray.ToolTipText = vm.TrayToolTip;
        };

        SetValue(TrayIcon.IconsProperty, new TrayIcons { tray });
    }
}
