using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TheTimer.App.Controls.Plan;
using TheTimer.App.ViewModels;

namespace TheTimer.App.Views;

public partial class PlanView : UserControl
{
    /// <summary>Below this width the right summary column collapses into a compact row under the header.</summary>
    private const double NarrowWidth = 1000;

    private PlanViewModel? _vm;

    public PlanView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as PlanViewModel);
        AttachedToVisualTree += (_, _) => ScrollToFocus();
        SizeChanged += (_, e) => ApplyLayout(e.NewSize.Width);
        Scroller.PropertyChanged += (_, e) =>
        {
            if (e.Property == ScrollViewer.ViewportProperty)
                EditorCard.MaxHeight = Math.Max(Scroller.Viewport.Height - 24, 240);
        };
        AddHandler(TappedEvent, OnTapped);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
    }

    private void Attach(PlanViewModel? vm)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = vm;
        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PlanViewModel.SelectedDay):
                ScrollToFocus();
                break;
            case nameof(PlanViewModel.IsEditorOpen) when _vm is { IsEditorOpen: true }:
                Dispatcher.UIThread.Post(RevealEditor, DispatcherPriority.Loaded);
                break;
        }
    }

    private void ApplyLayout(double width)
    {
        var narrow = width < NarrowWidth;
        SummaryColumn.IsVisible = !narrow;
        CompactSummary.IsVisible = narrow;
    }

    /// <summary>Scrolls so "now" (or the first block) sits comfortably inside the viewport.</summary>
    private void ScrollToFocus()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_vm is null)
                return;
            var y = PlanTimeline.YFor(_vm.FocusMinutes, _vm.RangeStartHour);
            var target = Math.Max(y - Scroller.Viewport.Height * 0.5, 0);
            Scroller.Offset = new Vector(0, Math.Min(target, Math.Max(Scroller.Extent.Height - Scroller.Viewport.Height, 0)));
        }, DispatcherPriority.Loaded);
    }

    private void RevealEditor()
    {
        var top = EditorCard.TranslatePoint(new Point(0, 0), Ribbon)?.Y ?? 0;
        var bottom = top + EditorCard.Bounds.Height;
        var offset = Scroller.Offset.Y;
        var view = Scroller.Viewport.Height;
        if (top < offset || bottom > offset + view)
            Scroller.Offset = new Vector(0, Math.Max(Math.Min(top - 16, bottom - view + 16), 0));
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Visual source || source.FindAncestorOfType<Button>(true) is not null)
            return;
        if (source.FindAncestorOfType<Border>(true) is not { } _)
            return;
        // Capsules live in the ribbon's items; walk up to the item container to find its view model.
        for (Visual? v = source; v is not null && !ReferenceEquals(v, Ribbon); v = v.GetVisualParent())
        {
            if (v is Control { DataContext: PlanOccurrenceItemViewModel item } && v is Border)
            {
                item.EditCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _vm is not { IsEditorOpen: true })
            return;
        if (_vm.IsConfirmingDelete)
            _vm.CancelDeleteCommand.Execute(null);
        else
            _vm.CancelEditCommand.Execute(null);
        e.Handled = true;
    }
}
