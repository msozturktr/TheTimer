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

    /// <summary>
    /// Initial scroll: the first block near the top (24px margin) when it and "now" both fit; otherwise
    /// "now" centred (today) or the first block (other days). Never leaves a block cut at the top edge.
    /// </summary>
    private void ScrollToFocus()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_vm is null)
                return;
            const double margin = 24;
            var view = Scroller.Viewport.Height;
            var max = Math.Max(Scroller.Extent.Height - view, 0);
            var start = _vm.RangeStartHour;
            var blocks = _vm.Items
                .Select(i => (Top: PlanTimeline.YFor(i.StartMinutes, start), Bottom: PlanTimeline.YFor(i.EndMinutes, start)))
                .OrderBy(b => b.Top)
                .ToList();
            var hasNow = _vm.NowMinutes >= 0;
            var nowY = hasNow ? PlanTimeline.YFor(_vm.NowMinutes, start) : 0;

            double target;
            if (blocks.Count == 0)
                target = hasNow ? nowY - view * 0.5 : 0;
            else
            {
                var firstTop = Math.Max(blocks[0].Top - margin, 0);
                if (!hasNow || nowY <= firstTop + view - margin)
                    target = firstTop;
                else
                {
                    target = nowY - view * 0.5;
                    // Don't slice a block at the top edge: snap up to its top if "now" stays visible, else past its end.
                    foreach (var b in blocks)
                    {
                        if (b.Top < target && b.Bottom > target)
                        {
                            target = nowY <= b.Top - 8 + view - margin ? b.Top - 8 : b.Bottom + 8;
                            break;
                        }
                    }
                }
            }
            Scroller.Offset = new Vector(0, Math.Clamp(target, 0, max));
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
