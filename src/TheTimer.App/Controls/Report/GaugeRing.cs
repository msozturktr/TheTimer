using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TheTimer.App.Controls.Report;

/// <summary>Circular gauge: amber-to-coral arc (with a soft glow) on a hairline ring and a small star at the centre.</summary>
public sealed class GaugeRing : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<GaugeRing, double>(nameof(Value));

    private const double Thickness = 6;

    private static readonly IPen TrackPen = new Pen(new SolidColorBrush(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF)), 2);
    private static readonly IBrush Star = new SolidColorBrush(Color.Parse("#FFB547"));

    static GaugeRing()
    {
        AffectsRender<GaugeRing>(ValueProperty);
    }

    public GaugeRing()
    {
        Width = 80;
        Height = 80;
        IsHitTestVisible = false;
    }

    /// <summary>Fraction of the ring that is filled (0..1; larger values draw a full ring).</summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = size / 2 - Thickness - 2;
        if (radius <= 4)
            return;

        context.DrawEllipse(null, TrackPen, center, radius, radius);

        var value = Math.Clamp(Value, 0, 1);
        if (value > 0.003)
        {
            var arc = new ConicGradientBrush
            {
                Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                Angle = -90 + 0,
                GradientStops =
                [
                    new GradientStop(Color.Parse("#FFB547"), 0),
                    new GradientStop(Color.Parse("#FF7A59"), Math.Max(0.02, value)),
                    new GradientStop(Color.Parse("#FF7A59"), 1),
                ],
            };
            var glow = new Pen(new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0x96, 0x50)), Thickness + 8, lineCap: PenLineCap.Round);
            var pen = new Pen(arc, Thickness, lineCap: PenLineCap.Round);

            if (value >= 0.997)
            {
                context.DrawEllipse(null, glow, center, radius, radius);
                context.DrawEllipse(null, pen, center, radius, radius);
            }
            else
            {
                var start = new Point(center.X, center.Y - radius);
                var angle = value * 2 * Math.PI - Math.PI / 2;
                var end = new Point(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
                var geometry = new StreamGeometry();
                using (var g = geometry.Open())
                {
                    g.BeginFigure(start, false);
                    g.ArcTo(end, new Size(radius, radius), 0, value > 0.5, SweepDirection.Clockwise);
                    g.EndFigure(false);
                }

                context.DrawGeometry(null, glow, geometry);
                context.DrawGeometry(null, pen, geometry);
            }
        }

        // Four-point star in the middle.
        var s = radius * 0.30;
        var k = s * 0.28;
        var star = new StreamGeometry();
        using (var g = star.Open())
        {
            g.BeginFigure(new Point(center.X, center.Y - s), true);
            g.LineTo(new Point(center.X + k, center.Y - k));
            g.LineTo(new Point(center.X + s, center.Y));
            g.LineTo(new Point(center.X + k, center.Y + k));
            g.LineTo(new Point(center.X, center.Y + s));
            g.LineTo(new Point(center.X - k, center.Y + k));
            g.LineTo(new Point(center.X - s, center.Y));
            g.LineTo(new Point(center.X - k, center.Y - k));
            g.EndFigure(true);
        }

        context.DrawGeometry(Star, null, star);
    }
}
