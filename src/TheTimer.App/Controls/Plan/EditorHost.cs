using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TheTimer.App.Controls.Plan;

/// <summary>
/// Overlay layer over the ribbon: places its single child (the floating editor card) at the right edge, vertically
/// next to the anchor time, and draws the amber connector from the edited block to the card.
/// </summary>
public sealed class EditorHost : Panel
{
    public const double CardWidth = 348;
    public const double CardGap = 18;

    public static readonly StyledProperty<int> RangeStartHourProperty =
        AvaloniaProperty.Register<EditorHost, int>(nameof(RangeStartHour), 8);

    public static readonly StyledProperty<double> AnchorStartMinutesProperty =
        AvaloniaProperty.Register<EditorHost, double>(nameof(AnchorStartMinutes));

    static EditorHost()
    {
        AffectsMeasure<EditorHost>(RangeStartHourProperty, AnchorStartMinutesProperty);
        AffectsArrange<EditorHost>(RangeStartHourProperty, AnchorStartMinutesProperty);
    }

    public int RangeStartHour { get => GetValue(RangeStartHourProperty); set => SetValue(RangeStartHourProperty, value); }

    public double AnchorStartMinutes { get => GetValue(AnchorStartMinutesProperty); set => SetValue(AnchorStartMinutesProperty, value); }

    private double CardTop => Math.Max(PlanTimeline.YFor(AnchorStartMinutes, RangeStartHour) - 8, 0);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width;
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(Math.Min(CardWidth, width), double.PositiveInfinity));
            if (child.IsVisible)
                height = Math.Max(height, CardTop + child.DesiredSize.Height + 16);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var w = Math.Min(CardWidth, finalSize.Width);
            child.Arrange(new Rect(finalSize.Width - w, CardTop, w, child.DesiredSize.Height));
        }

        return finalSize;
    }
}
