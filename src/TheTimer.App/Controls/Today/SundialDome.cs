using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TheTimer.App.ViewModels;

namespace TheTimer.App.Controls.Today;

/// <summary>
/// The 24-hour dome: an elliptical half-arc from 00 (left foot) over 12 (apex) to 24 (right foot).
/// Sessions are thick luminous amber-to-coral arcs, planned blocks thin dashed cyan arcs on an inner radius,
/// and "now" is a coral sun disc with a faint light ray down to a time chip. Renders only when <see cref="Data"/> changes.
/// </summary>
public sealed class SundialDome : Control
{
    public static readonly StyledProperty<TodayArcData?> DataProperty =
        AvaloniaProperty.Register<SundialDome, TodayArcData?>(nameof(Data));

    private static readonly Typeface Mono = new("avares://TheTimer/Assets/Fonts#JetBrains Mono");
    private static readonly Typeface MonoBold = new("avares://TheTimer/Assets/Fonts#JetBrains Mono", FontStyle.Normal, FontWeight.Bold);

    private static readonly Color Amber = Color.Parse("#FFB547");
    private static readonly Color Coral = Color.Parse("#FF7A59");
    private static readonly Color Cyan = Color.Parse("#7DD3FC");
    private static readonly Color Label = Color.Parse("#9AA3C7");

    static SundialDome()
    {
        AffectsRender<SundialDome>(DataProperty);
    }

    public SundialDome()
    {
        IsHitTestVisible = false;
    }

    public TodayArcData? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var data = Data ?? TodayArcData.Empty;
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 200 || h < 120)
            return;

        const double top = 34;
        var baseline = h - 50;
        var ry = baseline - top;
        var cx = w / 2;
        var rx = Math.Min(w / 2 - 44, ry * 1.85);
        var geo = new Geo(cx, baseline, rx, ry);

        DrawBody(context, geo);
        DrawTicks(context, geo);
        DrawPlanned(context, geo, data);
        DrawActual(context, geo, data);
        DrawNow(context, geo, data, w);
    }

    // ---- geometry helpers ----------------------------------------------------------------

    private readonly record struct Geo(double Cx, double Baseline, double Rx, double Ry)
    {
        /// <summary>Point on the dome at minute-of-day <paramref name="minute"/>, pushed <paramref name="offset"/> px along the outward normal.</summary>
        public Point At(double minute, double offset = 0)
        {
            var theta = Math.PI * (1 - minute / 1440.0);
            var cos = Math.Cos(theta);
            var sin = Math.Sin(theta);
            var nx = cos / Rx;
            var ny = sin / Ry;
            var len = Math.Sqrt(nx * nx + ny * ny);
            return new Point(Cx + Rx * cos + offset * nx / len, Baseline - Ry * sin - offset * ny / len);
        }
    }

    private static StreamGeometry Arc(Geo g, double from, double to, double offset)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        var steps = Math.Max(2, (int)Math.Ceiling((to - from) / 3));
        ctx.BeginFigure(g.At(from, offset), false);
        for (var i = 1; i <= steps; i++)
            ctx.LineTo(g.At(from + (to - from) * i / steps, offset));
        ctx.EndFigure(false);
        return geometry;
    }

    private static Color Fade(Color color, double opacity) =>
        Color.FromArgb((byte)Math.Round(255 * Math.Clamp(opacity, 0, 1)), color.R, color.G, color.B);

    private static FormattedText Text(string text, double size, Color color, bool bold = false) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, bold ? MonoBold : Mono, size, new SolidColorBrush(color));

    private static void DrawCentered(DrawingContext ctx, FormattedText text, Point at) =>
        ctx.DrawText(text, new Point(at.X - text.Width / 2, at.Y - text.Height / 2));

    // ---- layers --------------------------------------------------------------------------

    private static void DrawBody(DrawingContext ctx, Geo g)
    {
        // Dome interior: a faint cool vault of light fading toward the horizon.
        var fill = new StreamGeometry();
        using (var f = fill.Open())
        {
            f.BeginFigure(new Point(g.Cx - g.Rx, g.Baseline), true);
            const int steps = 240;
            for (var i = 0; i <= steps; i++)
                f.LineTo(g.At(1440.0 * i / steps));
            f.EndFigure(true);
        }

        var vault = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, g.Baseline - g.Ry, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(0, g.Baseline, RelativeUnit.Absolute),
            GradientStops =
            [
                new GradientStop(Fade(Label, 0.13), 0),
                new GradientStop(Fade(Label, 0.045), 0.55),
                new GradientStop(Fade(Label, 0.0), 1),
            ],
        };
        ctx.DrawGeometry(vault, null, fill);

        // Inner concentric rings and the horizon line.
        for (var i = 1; i <= 2; i++)
        {
            var ring = Arc(g, 0, 1440, -(18 + 38 * i));
            ctx.DrawGeometry(null, new Pen(new SolidColorBrush(Fade(Label, 0.07)), 1), ring);
        }

        var hairline = new Pen(new SolidColorBrush(Fade(Label, 0.22)), 1);
        ctx.DrawLine(hairline, new Point(g.Cx - g.Rx - 18, g.Baseline), new Point(g.Cx + g.Rx + 18, g.Baseline));
        ctx.DrawGeometry(null, new Pen(new SolidColorBrush(Fade(Label, 0.42)), 1.2), Arc(g, 0, 1440, 0));
    }

    private static void DrawTicks(DrawingContext ctx, Geo g)
    {
        var minor = new Pen(new SolidColorBrush(Fade(Label, 0.35)), 1);
        var major = new Pen(new SolidColorBrush(Fade(Label, 0.7)), 1.2);
        for (var hour = 0; hour <= 24; hour++)
        {
            var isMajor = hour % 3 == 0;
            ctx.DrawLine(isMajor ? major : minor, g.At(hour * 60, 0), g.At(hour * 60, isMajor ? 11 : 6));
        }

        for (var hour = 0; hour <= 24; hour += 3)
        {
            var apex = hour == 12;
            var text = Text((hour % 24 == 0 && hour > 0 ? 24 : hour).ToString("00", CultureInfo.InvariantCulture),
                apex ? 13 : 11, apex ? Amber : Fade(Label, 0.85), apex);
            // The feet labels sit under the horizon; the others float just outside the arc.
            var at = hour is 0 or 24 ? new Point(g.At(hour * 60).X, g.Baseline + 16) : g.At(hour * 60, 30);
            DrawCentered(ctx, text, at);
        }
    }

    private static void DrawPlanned(DrawingContext ctx, Geo g, TodayArcData data)
    {
        const double offset = -22;
        var cyan = new SolidColorBrush(Fade(Cyan, 0.9));
        var dashed = new Pen(cyan, 2.2, new DashStyle([2.2, 2.4], 0), PenLineCap.Flat);
        var glow = new Pen(new SolidColorBrush(Fade(Cyan, 0.10)), 8, lineCap: PenLineCap.Round);
        foreach (var seg in data.Planned)
        {
            var (from, to) = Span(seg);
            var arc = Arc(g, from, to, offset);
            ctx.DrawGeometry(null, glow, arc);
            ctx.DrawGeometry(null, dashed, arc);
        }
    }

    private static void DrawActual(DrawingContext ctx, Geo g, TodayArcData data)
    {
        LinearGradientBrush Sweep(double opacity) => new()
        {
            StartPoint = new RelativePoint(g.Cx - g.Rx, 0, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(g.Cx + g.Rx, 0, RelativeUnit.Absolute),
            Opacity = opacity,
            GradientStops = [new GradientStop(Amber, 0), new GradientStop(Color.Parse("#FF9A52"), 0.5), new GradientStop(Coral, 1)],
        };

        var glowLayers = new[] { (34.0, 0.035), (28.0, 0.045), (22.0, 0.06), (17.0, 0.08), (12.0, 0.12) }
            .Select(l => new Pen(Sweep(l.Item2), l.Item1, lineCap: PenLineCap.Round)).ToArray();
        var core = new Pen(Sweep(1), 6.5, lineCap: PenLineCap.Round);
        var spark = new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 244, 220)), 1.4, lineCap: PenLineCap.Round);

        var arcs = data.Sessions.Select(s =>
        {
            var (from, to) = Span(s);
            return Arc(g, from, to, 0);
        }).ToList();

        foreach (var pen in glowLayers)
            foreach (var arc in arcs) ctx.DrawGeometry(null, pen, arc);
        foreach (var arc in arcs) ctx.DrawGeometry(null, core, arc);
        foreach (var arc in arcs) ctx.DrawGeometry(null, spark, arc);
    }

    private static (double From, double To) Span(DomeSegment seg)
    {
        var from = Math.Clamp(Math.Min(seg.StartMinute, seg.EndMinute), 0, 1440);
        var to = Math.Clamp(Math.Max(seg.StartMinute, seg.EndMinute), 0, 1440);
        if (to - from < 4)
            to = Math.Min(1440, from + 4);
        return (from, to);
    }

    private static void DrawNow(DrawingContext ctx, Geo g, TodayArcData data, double width)
    {
        var sun = g.At(data.NowMinute);

        // Chip below the horizon, then the faint ray that joins it to the sun.
        var chipText = Text($"{data.NowText} · ŞİMDİ", 11.5, Color.Parse("#FFC9B8"), bold: true);
        var chipW = chipText.Width + 34;
        var chipH = 26.0;
        var chipX = Math.Clamp(sun.X - chipW / 2, 6, width - chipW - 6);
        var chipY = g.Baseline + 22;

        var rayX = sun.X;
        var ray = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, sun.Y, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(0, chipY, RelativeUnit.Absolute),
            GradientStops = [new GradientStop(Fade(Coral, 0.7), 0), new GradientStop(Fade(Coral, 0.18), 0.7), new GradientStop(Fade(Coral, 0.45), 1)],
        };
        var rayGlow = new LinearGradientBrush
        {
            StartPoint = ray.StartPoint,
            EndPoint = ray.EndPoint,
            GradientStops = [new GradientStop(Fade(Coral, 0.16), 0), new GradientStop(Fade(Coral, 0.0), 1)],
        };
        ctx.DrawLine(new Pen(rayGlow, 9), new Point(rayX, sun.Y), new Point(rayX, chipY));
        ctx.DrawLine(new Pen(ray, 1.2), new Point(rayX, sun.Y), new Point(rayX, chipY));

        var chipRect = new Rect(chipX, chipY, chipW, chipH);
        ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#1E1220")), new Pen(new SolidColorBrush(Fade(Coral, 0.6)), 1), chipRect, 13, 13);
        ctx.DrawEllipse(new SolidColorBrush(Coral), null, new Point(chipX + 14, chipY + chipH / 2), 3, 3);
        ctx.DrawText(chipText, new Point(chipX + 24, chipY + (chipH - chipText.Height) / 2));

        // The sun: concentric glow discs, a coral body and a bright centre.
        var aura = new RadialGradientBrush
        {
            GradientStops =
            [
                new GradientStop(Fade(Coral, 0.55), 0),
                new GradientStop(Fade(Coral, 0.22), 0.35),
                new GradientStop(Fade(Coral, 0.06), 0.7),
                new GradientStop(Fade(Coral, 0.0), 1),
            ],
        };
        ctx.DrawEllipse(aura, null, sun, 40, 40);
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#0B0E22")), new Pen(new SolidColorBrush(Fade(Color.Parse("#FFE7DC"), 0.95)), 1.6), sun, 8, 8);
        ctx.DrawEllipse(new RadialGradientBrush
        {
            GradientStops = [new GradientStop(Color.Parse("#FFF1E6"), 0), new GradientStop(Coral, 1)],
        }, null, sun, 6.2, 6.2);
    }
}
