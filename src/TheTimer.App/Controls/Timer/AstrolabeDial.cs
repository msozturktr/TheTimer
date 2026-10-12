using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TheTimer.App.Controls.Timer;

public enum DialKind
{
    Stopwatch,
    Countdown,
    Pomodoro,
}

/// <summary>
/// The hero instrument of the timer tab: a ring of 60 hairline ticks, engraved inner rings, a faint constellation
/// ring and the progress arc (amber to coral conic gradient, layered glow, white-hot comet head). Pomodoro mode adds
/// four "moons" near the top-right. Draws square, centred, scaling with <c>min(Width, Height)</c>; the centre is left
/// empty for an XAML overlay. Nothing here animates by itself: the pulse of the current moon is sampled at render time
/// and renders only happen when the bound values change (the view model's 250 ms tick while running).
/// </summary>
public sealed class AstrolabeDial : Control
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<AstrolabeDial, double>(nameof(Progress));

    public static readonly StyledProperty<DialKind> KindProperty =
        AvaloniaProperty.Register<AstrolabeDial, DialKind>(nameof(Kind));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<AstrolabeDial, bool>(nameof(IsActive));

    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<AstrolabeDial, bool>(nameof(IsRunning));

    public static readonly StyledProperty<double> TargetMinutesProperty =
        AvaloniaProperty.Register<AstrolabeDial, double>(nameof(TargetMinutes));

    public static readonly StyledProperty<int> MoonCountProperty =
        AvaloniaProperty.Register<AstrolabeDial, int>(nameof(MoonCount));

    public static readonly StyledProperty<int> MoonsCompletedProperty =
        AvaloniaProperty.Register<AstrolabeDial, int>(nameof(MoonsCompleted));

    public static readonly StyledProperty<bool> MoonCurrentProperty =
        AvaloniaProperty.Register<AstrolabeDial, bool>(nameof(MoonCurrent));

    private static readonly Color Amber = Color.Parse("#FFB547");
    private static readonly Color Orange = Color.Parse("#FF9650");
    private static readonly Color Coral = Color.Parse("#FF7A59");

    // Constellation dots: (turn from top, radius jitter, size)
    private static readonly (double Turn, double Jitter, double Size)[] Stars =
    [
        (0.06, 0.000, 1.6), (0.19, 0.012, 1.1), (0.31, -0.008, 1.9), (0.43, 0.006, 1.2),
        (0.58, -0.010, 1.7), (0.71, 0.010, 1.1), (0.84, -0.004, 1.5), (0.93, 0.008, 1.2),
    ];

    static AstrolabeDial()
    {
        AffectsRender<AstrolabeDial>(ProgressProperty, KindProperty, IsActiveProperty, IsRunningProperty,
            TargetMinutesProperty, MoonCountProperty, MoonsCompletedProperty, MoonCurrentProperty);
    }

    public AstrolabeDial()
    {
        IsHitTestVisible = false;
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public DialKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>True while a session exists (running or paused); false draws only the calm idle instrument.</summary>
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public bool IsRunning
    {
        get => GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    /// <summary>Target length in minutes (countdown/pomodoro), used for the quarter labels.</summary>
    public double TargetMinutes
    {
        get => GetValue(TargetMinutesProperty);
        set => SetValue(TargetMinutesProperty, value);
    }

    public int MoonCount
    {
        get => GetValue(MoonCountProperty);
        set => SetValue(MoonCountProperty, value);
    }

    public int MoonsCompleted
    {
        get => GetValue(MoonsCompletedProperty);
        set => SetValue(MoonsCompletedProperty, value);
    }

    public bool MoonCurrent
    {
        get => GetValue(MoonCurrentProperty);
        set => SetValue(MoonCurrentProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size < 40)
            return;

        var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var half = size / 2 - 1;
        var active = IsActive;
        var dim = active ? 1.0 : 0.6;

        DrawConstellation(context, c, half, dim);
        DrawTicks(context, c, half, active, dim);
        DrawLabels(context, c, half, dim);
        DrawInnerRings(context, c, half, dim, active);

        var arcR = half * 0.74;
        var thick = Math.Max(5, half * 0.036);
        DrawTrack(context, c, arcR, thick, dim);
        if (active && Progress > 0.002)
            DrawProgress(context, c, arcR, thick, Math.Min(Progress, 0.9995));

        if (MoonCount > 0 && Kind == DialKind.Pomodoro)
            DrawMoons(context, c, half);
    }

    private static Point P(Point c, double turn, double r)
    {
        var a = turn * 2 * Math.PI;
        return new Point(c.X + r * Math.Sin(a), c.Y - r * Math.Cos(a));
    }

    private static IBrush White(double alpha) =>
        new SolidColorBrush(Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), 255, 255, 255));

    private static void DrawConstellation(DrawingContext context, Point c, double half, double dim)
    {
        var ringR = half * 0.975;
        context.DrawEllipse(null, new Pen(White(0.05 * dim), 1), c, ringR, ringR);
        foreach (var (turn, jitter, starSize) in Stars)
        {
            var p = P(c, turn, ringR + half * jitter);
            context.DrawEllipse(White(0.07 * dim), null, p, starSize * 2.6, starSize * 2.6);
            context.DrawEllipse(White(0.5 * dim), null, p, starSize * 0.75, starSize * 0.75);
        }
    }

    private void DrawTicks(DrawingContext context, Point c, double half, bool active, double dim)
    {
        var passed = Math.Min(Progress, 1);
        var outer = half * 0.855;
        var shortPen = new Pen(White(0.22 * dim), 1);
        var longPen = new Pen(White(0.5 * dim), 1.4);
        var litShort = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 181, 71)), 1);
        var litLong = new Pen(new SolidColorBrush(Color.FromArgb(230, 255, 181, 71)), 1.5);
        for (var i = 0; i < 60; i++)
        {
            var turn = i / 60.0;
            var major = i % 5 == 0;
            var len = half * (major ? 0.052 : 0.024);
            var lit = active && turn <= passed && i > 0 ? major ? litLong : litShort : null;
            var pen = lit ?? (major ? longPen : shortPen);
            context.DrawLine(pen, P(c, turn, outer), P(c, turn, outer - len));
        }
    }

    private void DrawLabels(DrawingContext context, Point c, double half, double dim)
    {
        string[] labels = Kind == DialKind.Stopwatch || TargetMinutes <= 0
            ? ["0", "15", "30", "45"]
            : ["0", "25%", "50%", "75%"];
        var brush = new SolidColorBrush(Color.FromArgb((byte)(255 * (0.85 * dim)), 0x5B, 0x63, 0x8A));
        var typeface = new Typeface(MonoFont());
        for (var i = 0; i < 4; i++)
        {
            var text = new FormattedText(labels[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface,
                Math.Max(8, half * 0.042), brush);
            var p = P(c, i / 4.0, half * 0.915);
            context.DrawText(text, new Point(p.X - text.Width / 2, p.Y - text.Height / 2));
        }
    }

    private string Quarter(double fraction) =>
        Math.Round(TargetMinutes * fraction).ToString(CultureInfo.InvariantCulture);

    private static void DrawInnerRings(DrawingContext context, Point c, double half, double dim, bool active)
    {
        var r = half * 0.655;
        var fill = new RadialGradientBrush
        {
            Center = new RelativePoint(c, RelativeUnit.Absolute),
            GradientOrigin = new RelativePoint(c, RelativeUnit.Absolute),
            RadiusX = RelativeScalar.Parse(r.ToString("0.##", CultureInfo.InvariantCulture)),
            RadiusY = RelativeScalar.Parse(r.ToString("0.##", CultureInfo.InvariantCulture)),
            GradientStops =
            [
                new GradientStop(Color.FromArgb(18, 255, 255, 255), 0),
                new GradientStop(Color.FromArgb(0, 255, 255, 255), 1),
            ],
        };
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(70 * dim), 6, 8, 22)), null, c, r, r);
        context.DrawEllipse(fill, null, c, r, r);
        if (active)
        {
            var gr = half * 0.5;
            var glow = new RadialGradientBrush
            {
                Center = new RelativePoint(c.WithY(c.Y - half * 0.1), RelativeUnit.Absolute),
                GradientOrigin = new RelativePoint(c.WithY(c.Y - half * 0.1), RelativeUnit.Absolute),
                RadiusX = RelativeScalar.Parse(gr.ToString("0.##", CultureInfo.InvariantCulture)),
                RadiusY = RelativeScalar.Parse(gr.ToString("0.##", CultureInfo.InvariantCulture)),
                GradientStops =
                [
                    new GradientStop(Color.FromArgb(34, 255, 181, 71), 0),
                    new GradientStop(Color.FromArgb(0, 255, 181, 71), 1),
                ],
            };
            context.DrawEllipse(glow, null, c.WithY(c.Y - half * 0.1), gr, gr);
        }

        // Engraved double line: a dark groove and a light lip.
        context.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), 1.2), c, r, r);
        context.DrawEllipse(null, new Pen(White(0.075 * dim), 1), c, r + 1.4, r + 1.4);
        context.DrawEllipse(null, new Pen(White(0.045 * dim), 1), c, r - 3, r - 3);
        var dash = new Pen(White(0.07 * dim), 1, new DashStyle([1, 5], 0));
        context.DrawEllipse(null, dash, c, half * 0.555, half * 0.555);
    }

    private static void DrawTrack(DrawingContext context, Point c, double r, double thick, double dim)
    {
        context.DrawEllipse(null, new Pen(White(0.045 * dim), 1.2), c, r, r);
        context.DrawEllipse(null, new Pen(White(0.02 * dim), thick), c, r, r);
    }

    private static StreamGeometry Arc(Point c, double r, double fromTurn, double toTurn)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(P(c, fromTurn, r), false);
            g.ArcTo(P(c, toTurn, r), new Size(r, r), 0, toTurn - fromTurn > 0.5, SweepDirection.Clockwise);
            g.EndFigure(false);
        }

        return geometry;
    }

    private static ConicGradientBrush Conic(Point c, double p, double opacity, Color a, Color b, Color head)
    {
        var brush = new ConicGradientBrush
        {
            Center = new RelativePoint(c, RelativeUnit.Absolute),
            Angle = 0,
            Opacity = opacity,
            GradientStops =
            [
                new GradientStop(a, 0),
                new GradientStop(b, p * 0.55),
                new GradientStop(head, p),
            ],
        };
        // The round start cap reaches just before offset 0 (= offset 1): keep that sliver amber, not the head colour.
        var after = Math.Min(p + 0.012, 0.995);
        if (after < 1)
        {
            brush.GradientStops.Add(new GradientStop(head, after));
            brush.GradientStops.Add(new GradientStop(a, 1));
        }

        return brush;
    }

    private void DrawProgress(DrawingContext context, Point c, double r, double thick, double p)
    {
        var arc = Arc(c, r, 0, p);
        Color Lerp(Color x, Color y, double t) => Color.FromRgb(
            (byte)(x.R + (y.R - x.R) * t), (byte)(x.G + (y.G - x.G) * t), (byte)(x.B + (y.B - x.B) * t));

        // Layered glow, widest and faintest first.
        (double Width, double Opacity)[] layers = [(6.0, 0.016), (5.2, 0.02), (4.5, 0.026), (3.8, 0.032), (3.1, 0.04), (2.5, 0.05), (2.0, 0.065), (1.6, 0.09)];
        foreach (var (width, opacity) in layers)
            context.DrawGeometry(null,
                new Pen(Conic(c, p, opacity, Amber, Orange, Coral), thick * width, lineCap: PenLineCap.Round), arc);

        // Core stroke.
        context.DrawGeometry(null,
            new Pen(Conic(c, p, 1, Amber, Orange, Coral), thick, lineCap: PenLineCap.Round), arc);
        // Inner light line for luminosity.
        context.DrawGeometry(null,
            new Pen(Conic(c, p, 0.55, Lerp(Amber, Colors.White, 0.55), Lerp(Orange, Colors.White, 0.5), Lerp(Coral, Colors.White, 0.5)),
                thick * 0.28, lineCap: PenLineCap.Round), arc);

        // Comet: short fading tail, soft halo and white-hot head.
        var tailTurn = Math.Min(0.045, p);
        var from = P(c, p - tailTurn, r);
        var head = P(c, p, r);
        var tailBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(from, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(head, RelativeUnit.Absolute),
            GradientStops =
            [
                new GradientStop(Color.FromArgb(0, 255, 240, 214), 0),
                new GradientStop(Color.FromArgb(235, 255, 244, 224), 1),
            ],
        };
        context.DrawGeometry(null, new Pen(tailBrush, thick * 0.6, lineCap: PenLineCap.Round), Arc(c, r, p - tailTurn, p));

        var haloR = thick * 3.6;
        var halo = new RadialGradientBrush
        {
            Center = new RelativePoint(head, RelativeUnit.Absolute),
            GradientOrigin = new RelativePoint(head, RelativeUnit.Absolute),
            RadiusX = RelativeScalar.Parse(haloR.ToString("0.##", CultureInfo.InvariantCulture)),
            RadiusY = RelativeScalar.Parse(haloR.ToString("0.##", CultureInfo.InvariantCulture)),
            GradientStops =
            [
                new GradientStop(Color.FromArgb(235, 255, 250, 238), 0),
                new GradientStop(Color.FromArgb(170, 255, 214, 150), 0.22),
                new GradientStop(Color.FromArgb(70, 255, 150, 80), 0.5),
                new GradientStop(Color.FromArgb(0, 255, 120, 70), 1),
            ],
        };
        context.DrawEllipse(halo, null, head, haloR, haloR);
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#FFFFFBF2")), null, head, thick * 0.62, thick * 0.62);
    }

    private void DrawMoons(DrawingContext context, Point c, double half)
    {
        var moonR = Math.Max(3.2, half * 0.03);
        var ringR = half * 0.935;
        var step = 0.032;
        var first = 0.09;
        var pulse = (1 - Math.Cos(Environment.TickCount64 % 2200 / 2200.0 * 2 * Math.PI)) / 2;
        for (var i = 0; i < MoonCount; i++)
        {
            var p = P(c, first + i * step, ringR);
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#E607091A")), null, p, moonR + 2.5, moonR + 2.5);
            if (i < MoonsCompleted)
            {
                context.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, 255, 181, 71)), null, p, moonR * 2, moonR * 2);
                context.DrawEllipse(new SolidColorBrush(Amber), null, p, moonR, moonR);
            }
            else if (i == MoonsCompleted && MoonCurrent)
            {
                if (IsRunning)
                {
                    var haloR = moonR * (1.7 + 1.3 * pulse);
                    context.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(90 * (1 - pulse) + 25), 255, 181, 71)), null, p, haloR, haloR);
                }

                context.DrawEllipse(new SolidColorBrush(Color.FromArgb(110, 255, 181, 71)), new Pen(new SolidColorBrush(Amber), 1.4), p, moonR, moonR);
            }
            else
            {
                context.DrawEllipse(null, new Pen(White(0.38), 1.2), p, moonR, moonR);
            }
        }
    }

    private FontFamily MonoFont() =>
        this.TryFindResource("Font.Mono", out var v) && v is FontFamily f ? f : FontFamily.Default;
}
