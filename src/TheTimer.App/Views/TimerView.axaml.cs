using Avalonia.Controls;

namespace TheTimer.App.Views;

public partial class TimerView : UserControl
{
    // Window width at which the "Bugün" side panel gives way to the dial (the nav rail takes 88 px).
    private const double SidePanelMinWidth = 1100 - 88;

    public TimerView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => SidePanel.IsVisible = e.NewSize.Width >= SidePanelMinWidth;
    }
}
