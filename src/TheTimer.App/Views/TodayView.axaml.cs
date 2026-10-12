using Avalonia.Controls;

namespace TheTimer.App.Views;

public partial class TodayView : UserControl
{
    // Below this width the two lower panels stack instead of sitting side by side.
    private const double StackBelow = 1000;

    public TodayView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Arrange(e.NewSize.Width < StackBelow);
    }

    private void Arrange(bool stacked)
    {
        Dome.Height = stacked ? 250 : 300;
        Lists.ColumnDefinitions = stacked ? new ColumnDefinitions("*") : new ColumnDefinitions("*,*");
        Lists.RowDefinitions = stacked ? new RowDefinitions("Auto,Auto") : new RowDefinitions();
        Grid.SetColumn(ChainPanel, stacked ? 0 : 1);
        Grid.SetRow(ChainPanel, stacked ? 1 : 0);
    }
}
