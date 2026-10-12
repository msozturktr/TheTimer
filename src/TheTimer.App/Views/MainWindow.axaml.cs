using Avalonia.Controls;
using TheTimer.App.ViewModels;

namespace TheTimer.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
