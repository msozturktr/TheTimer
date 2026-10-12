using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TheTimer.App.Controls.Report;

/// <summary>Horizontal plan vs. actual bar: a ghost cyan-lavender outline for the plan, a glowing amber fill for the actual.</summary>
public sealed class CompareBar : Control
{
    public static readonly StyledProperty<double> PlannedProperty =
        AvaloniaProperty.Register<CompareBar, double>(nameof(Planned));

    public static readonly StyledProperty<double> ActualProperty =
        AvaloniaProperty.Register<CompareBar, double>(nameof(Actual));

    private static readonly IBrush Track = new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF));

    private static readonly IBrush ActualFill = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        GradientStops = [new GradientStop(Color.Parse("#FFB547"), 0), new GradientStop(Color.Parse("#FF7A59"), 1)],
    };

    private static readonly IPen GhostPen = new Pen(new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        GradientStops = [new GradientStop(Color.FromArgb(0x99, 0x7D, 0xD3, 0xFC), 0), new GradientStop(Color.FromArgb(0x8C, 0xA7, 0x8B, 0xFA), 1)],
    }, 1);

    private static readonly BoxShadows Glow = BoxShadows.Parse("0 0 8 0 #55FF9650");

    static CompareBar()
    {
        AffectsRender<CompareBar>(PlannedProperty, ActualProperty);
        AffectsMeasure<CompareBar>(HeightProperty);
    }

    public CompareBar()
    {
        Height = 14;
        IsHitTestVisible = false;
    }

    /// <summary>Planned length as a fraction (0..1) of the full bar.</summary>
    public double Planned
    {
        get => GetValue(PlannedProperty);
        set => SetValue(PlannedProperty, value);
    }

    /// <summary>Actual length as a fraction (0..1) of the full bar.</summary>
    public double Actual
    {
        get => GetValue(ActualProperty);
        set => SetValue(ActualProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 8)
            return;

        var midY = h / 2;
        context.DrawRectangle(Track, null, new RoundedRect(new Rect(0, midY - 2, w, 4), 2));

        var plannedW = Math.Clamp(Planned, 0, 1) * w;
        if (plannedW > 0)
        {
            var ghost = new Rect(0.5, midY - 6.5, Math.Max(12, plannedW) - 1, 13);
            context.DrawRectangle(null, GhostPen, new RoundedRect(ghost, 6.5));
        }

        var actualW = Math.Clamp(Actual, 0, 1) * w;
        if (actualW > 0)
        {
            var fill = new Rect(0, midY - 3, Math.Max(6, actualW), 6);
            context.DrawRectangle(ActualFill, null, new RoundedRect(fill, 3), Glow);
        }
    }
}
