using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TheTimer.App.Controls.Plan;

/// <summary>Dual ring gauge: cyan track = planned, amber to coral arc = actual / planned, label in the centre.</summary>
public sealed class RatioRing : Control
{
    public static readonly StyledProperty<double> RatioProperty =
        AvaloniaProperty.Register<RatioRing, double>(nameof(Ratio));

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<RatioRing, string>(nameof(Text), string.Empty);

    static RatioRing()
    {
        AffectsRender<RatioRing>(RatioProperty, TextProperty);
    }

    public RatioRing()
    {
        Width = 76;
        Height = 76;
        IsHitTestVisible = false;
    }

    /// <summary>Actual / planned; the arc is capped at a full turn.</summary>
    public double Ratio
    {
        get => GetValue(RatioProperty);
        set => SetValue(RatioProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        const double stroke = 5;
        var radius = size / 2 - stroke / 2 - 2;
        var cyan = PlanPalette.Cyan(this);

        var track = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.FromArgb(0xB0, cyan.R, cyan.G, cyan.B), 0),
                new GradientStop(Color.FromArgb(0x70, 0xA7, 0x8B, 0xFA), 1),
            ],
        };
        context.DrawEllipse(null, new Pen(track, stroke), centre, radius, radius);

        var ratio = Math.Clamp(Ratio, 0, 1);
        if (ratio > 0.003)
        {
            var inner = radius - stroke - 1;
            var sweep = ratio * 2 * Math.PI;
            var start = new Point(centre.X, centre.Y - inner);
            var end = new Point(centre.X + inner * Math.Sin(sweep), centre.Y - inner * Math.Cos(sweep));
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(start, false);
                if (ratio >= 0.999)
                {
                    g.ArcTo(new Point(centre.X, centre.Y + inner), new Size(inner, inner), 0, false, SweepDirection.Clockwise);
                    g.ArcTo(start, new Size(inner, inner), 0, false, SweepDirection.Clockwise);
                }
                else
                {
                    g.ArcTo(end, new Size(inner, inner), 0, sweep > Math.PI, SweepDirection.Clockwise);
                }

                g.EndFigure(false);
            }

            var amber = PlanPalette.Amber(this);
            var coral = PlanPalette.Coral(this);
            var arc = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = [new GradientStop(amber, 0), new GradientStop(coral, 1)],
            };
            context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(0x30, amber.R, amber.G, amber.B)), stroke + 5, lineCap: PenLineCap.Round), geometry);
            context.DrawGeometry(null, new Pen(arc, stroke - 1, lineCap: PenLineCap.Round), geometry);
        }

        var text = PlanPalette.Text(Text, PlanPalette.Mono(this, FontWeight.Medium), 12, new SolidColorBrush(PlanPalette.Amber(this)));
        context.DrawText(text, new Point(centre.X - text.Width / 2, centre.Y - text.Height / 2));
    }
}
