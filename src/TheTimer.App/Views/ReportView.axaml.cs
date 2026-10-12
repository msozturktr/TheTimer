using Avalonia.Controls;
using Avalonia;
using TheTimer.App.ViewModels;

namespace TheTimer.App.Views;

public partial class ReportView : UserControl
{
    public ReportView()
    {
        InitializeComponent();
    }

    // The tab content is attached whenever the Rapor tab is selected, so plan edits made elsewhere show up.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        (DataContext as ReportViewModel)?.Refresh();
    }
}
