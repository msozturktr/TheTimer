using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using TheTimer.App.Services;
using TheTimer.App.ViewModels;
using TheTimer.App.Views;
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

            var viewModel = new MainWindowViewModel(
                new SessionTimer(TimeProvider.System),
                new InMemorySessionRepository(),
                TimeProvider.System,
                new FreedesktopNotifier());
            var window = new MainWindow { DataContext = viewModel };
            desktop.MainWindow = window;

            // Idle close ends the app; the window cancels the close itself while a session is active.
            window.Closed += (_, _) => desktop.Shutdown();

            SetupTrayIcon(desktop, window, viewModel);
        }

        base.OnFrameworkInitializationCompleted();
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
