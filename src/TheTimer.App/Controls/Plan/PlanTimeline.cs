using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;

namespace TheTimer.App.Controls.Plan;

/// <summary>
/// Items panel of the day ribbon: draws the hour rail and hairlines and places each block (data context implements
/// <see cref="ITimelineItem"/>) absolutely by time; overlapping blocks sit side by side in their columns.
/// </summary>
public sealed class PlanTimeline : Panel
{
    public const double HourHeight = 68;
    public const double LeftInset = 72;
    public const double TopPad = 18;
    public const double BottomPad = 18;
    public const double ColumnGap = 8;
    public const double MinBlockWidth = 300;
    private const double MinBlockHeight = 34;

    public static readonly StyledProperty<int> RangeStartHourProperty =
        AvaloniaProperty.Register<PlanTimeline, int>(nameof(RangeStartHour), 8);

    public static readonly StyledProperty<int> RangeEndHourProperty =
        AvaloniaProperty.Register<PlanTimeline, int>(nameof(RangeEndHour), 20);

    /// <summary>Width kept free on the right (for the floating editor card).</summary>
    public static readonly StyledProperty<double> ReserveRightProperty =
        AvaloniaProperty.Register<PlanTimeline, double>(nameof(ReserveRight));

    static PlanTimeline()
    {
        AffectsMeasure<PlanTimeline>(RangeStartHourProperty, RangeEndHourProperty);
        AffectsArrange<PlanTimeline>(ReserveRightProperty);
    }

    public int RangeStartHour
    {
        get => GetValue(RangeStartHourProperty);
        set => SetValue(RangeStartHourProperty, value);
    }

    public int RangeEndHour
    {
        get => GetValue(RangeEndHourProperty);
        set => SetValue(RangeEndHourProperty, value);
    }

    public double ReserveRight
    {
        get => GetValue(ReserveRightProperty);
        set => SetValue(ReserveRightProperty, value);
    }

    public static double YFor(double minutes, int rangeStartHour) =>
        TopPad + (minutes / 60.0 - rangeStartHour) * HourHeight;

    public static double ContentHeight(int rangeStartHour, int rangeEndHour) =>
        TopPad + (rangeEndHour - rangeStartHour) * HourHeight + BottomPad;

    /// <summary>Horizontal span of a block column inside a panel of the given width.</summary>
    public static (double X, double Width) ColumnSpan(double totalWidth, double reserve, int column, int columns)
    {
        var area = Math.Min(Math.Max(totalWidth - LeftInset - reserve, MinBlockWidth), Math.Max(totalWidth - LeftInset, 0));
        columns = Math.Max(columns, 1);
        var width = (area - ColumnGap * (columns - 1)) / columns;
        return (LeftInset + column * (width + ColumnGap), Math.Max(width, 0));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width;
        foreach (var child in Children)
        {
            var (_, w) = Span(child, width);
            child.Measure(new Size(w, BlockHeight(child)));
        }

        return new Size(width, ContentHeight(RangeStartHour, RangeEndHour));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var (x, w) = Span(child, finalSize.Width);
            var y = ItemOf(child) is { } item ? YFor(item.StartMinutes, RangeStartHour) + 1 : 0;
            child.Arrange(new Rect(x, y, w, BlockHeight(child)));
        }

        return finalSize;
    }

    private (double X, double Width) Span(Control child, double width) =>
        ItemOf(child) is { } item
            ? ColumnSpan(width, ReserveRight, item.Column, item.ColumnCount)
            : (LeftInset, Math.Max(width - LeftInset, 0));

    private static ITimelineItem? ItemOf(Control child) =>
        (child as ContentPresenter)?.Content as ITimelineItem ?? child.DataContext as ITimelineItem;

    private static double BlockHeight(Control child) =>
        ItemOf(child) is { } item
            ? Math.Max((item.EndMinutes - item.StartMinutes) / 60.0 * HourHeight - 4, MinBlockHeight)
            : MinBlockHeight;
}
