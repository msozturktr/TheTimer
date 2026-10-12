using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TheTimer.App.Controls.Plan;

/// <summary>
/// Drawn layer under the blocks: hour rail (mono labels), hairline hour lines and the amber connector that links the
/// edited block to the floating editor card.
/// </summary>
public sealed class RibbonBackdrop : Control
{
    public static readonly StyledProperty<int> RangeStartHourProperty =
        AvaloniaProperty.Register<RibbonBackdrop, int>(nameof(RangeStartHour), 8);

    public static readonly StyledProperty<int> RangeEndHourProperty =
        AvaloniaProperty.Register<RibbonBackdrop, int>(nameof(RangeEndHour), 20);

    public static readonly StyledProperty<double> ReserveRightProperty =
        AvaloniaProperty.Register<RibbonBackdrop, double>(nameof(ReserveRight));

    public static readonly StyledProperty<bool> HasConnectorProperty =
        AvaloniaProperty.Register<RibbonBackdrop, bool>(nameof(HasConnector));

    public static readonly StyledProperty<double> AnchorStartMinutesProperty =
        AvaloniaProperty.Register<RibbonBackdrop, double>(nameof(AnchorStartMinutes));

    public static readonly StyledProperty<double> AnchorEndMinutesProperty =
        AvaloniaProperty.Register<RibbonBackdrop, double>(nameof(AnchorEndMinutes));

    public static readonly StyledProperty<int> AnchorColumnProperty =
        AvaloniaProperty.Register<RibbonBackdrop, int>(nameof(AnchorColumn));

    public static readonly StyledProperty<int> AnchorColumnCountProperty =
        AvaloniaProperty.Register<RibbonBackdrop, int>(nameof(AnchorColumnCount), 1);

    private readonly Dictionary<int, FormattedText> _labels = [];

    static RibbonBackdrop()
    {
        AffectsMeasure<RibbonBackdrop>(RangeStartHourProperty, RangeEndHourProperty);
        AffectsRender<RibbonBackdrop>(
            RangeStartHourProperty, RangeEndHourProperty, ReserveRightProperty, HasConnectorProperty,
            AnchorStartMinutesProperty, AnchorEndMinutesProperty, AnchorColumnProperty, AnchorColumnCountProperty);
    }

    public RibbonBackdrop()
    {
        IsHitTestVisible = false;
    }

    public int RangeStartHour { get => GetValue(RangeStartHourProperty); set => SetValue(RangeStartHourProperty, value); }

    public int RangeEndHour { get => GetValue(RangeEndHourProperty); set => SetValue(RangeEndHourProperty, value); }

    public double ReserveRight { get => GetValue(ReserveRightProperty); set => SetValue(ReserveRightProperty, value); }

    /// <summary>False for a new block: the card sits near the top without a connector.</summary>
    public bool HasConnector { get => GetValue(HasConnectorProperty); set => SetValue(HasConnectorProperty, value); }

    public double AnchorStartMinutes { get => GetValue(AnchorStartMinutesProperty); set => SetValue(AnchorStartMinutesProperty, value); }

    public double AnchorEndMinutes { get => GetValue(AnchorEndMinutesProperty); set => SetValue(AnchorEndMinutesProperty, value); }

    public int AnchorColumn { get => GetValue(AnchorColumnProperty); set => SetValue(AnchorColumnProperty, value); }

    public int AnchorColumnCount { get => GetValue(AnchorColumnCountProperty); set => SetValue(AnchorColumnCountProperty, value); }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
            PlanTimeline.ContentHeight(RangeStartHour, RangeEndHour));

    public override void Render(DrawingContext context)
    {
        var line = new Pen(new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)), 1);
        var mono = PlanPalette.Mono(this);
        var labelBrush = new SolidColorBrush(PlanPalette.Faint(this));
        var width = Bounds.Width;
        for (var hour = RangeStartHour; hour <= RangeEndHour; hour++)
        {
            var y = Math.Round(PlanTimeline.YFor(hour * 60, RangeStartHour)) + 0.5;
            context.DrawLine(line, new Point(PlanTimeline.LeftInset - 10, y), new Point(width, y));
            if (!_labels.TryGetValue(hour, out var text))
                _labels[hour] = text = PlanPalette.Text($"{hour:00}:00", mono, 11, labelBrush);
            context.DrawText(text, new Point(PlanTimeline.LeftInset - 22 - text.Width, y - text.Height / 2));
        }

        if (HasConnector)
            DrawConnector(context, width);
    }

    private void DrawConnector(DrawingContext context, double width)
    {
        var amber = PlanPalette.Amber(this);
        var cardX = width - Math.Min(EditorHost.CardWidth, width);
        var (x, w) = PlanTimeline.ColumnSpan(width, ReserveRight, AnchorColumn, AnchorColumnCount);
        var right = x + w;
        var y = Math.Round(PlanTimeline.YFor((AnchorStartMinutes + AnchorEndMinutes) / 2, RangeStartHour)) + 0.5;
        if (ReserveRight <= 0 || right + 10 >= cardX)
            return;
        var fade = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops = [new GradientStop(amber, 0), new GradientStop(Color.FromArgb(0x66, amber.R, amber.G, amber.B), 1)],
        };
        context.DrawLine(new Pen(fade, 1.4), new Point(right + 6, y), new Point(cardX, y));
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x40, amber.R, amber.G, amber.B)), null, new Point(right + 6, y), 7, 7);
        context.DrawEllipse(new SolidColorBrush(amber), null, new Point(right + 6, y), 3.5, 3.5);
    }
}
