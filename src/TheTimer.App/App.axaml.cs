using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    new SessionTimer(TimeProvider.System),
                    new InMemorySessionRepository(),
                    TimeProvider.System,
                    new FreedesktopNotifier()),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}