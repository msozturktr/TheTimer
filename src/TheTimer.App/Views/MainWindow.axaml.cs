using Avalonia.Controls;
using TheTimer.App.ViewModels;

namespace TheTimer.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private WindowState _stateBeforeFullScreen = WindowState.Normal;

    /// <summary>F11 toggles full screen and restores the previous window state.</summary>
    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.F11)
        {
            if (WindowState == WindowState.FullScreen)
                WindowState = _stateBeforeFullScreen;
            else
            {
                _stateBeforeFullScreen = WindowState;
                WindowState = WindowState.FullScreen;
            }
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!e.IsProgrammatic && DataContext is MainWindowViewModel vm && vm.ShouldHideOnClose())
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    /// <summary>Shows the window, restores it from minimized state and brings it to front.</summary>
    public void ShowAndActivate()
    {
        if (!IsVisible)
            Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }
}
