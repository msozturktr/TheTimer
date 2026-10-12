using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TheTimer.App.Controls.Report;

/// <summary>
/// Weekly focus chart: seven columns, each a ghost capsule for the planned time with a filled, glowing amber-to-coral
/// capsule for the actual time rising from the baseline. A star dot tops every actual capsule; a faint polyline joins them.
/// </summary>
public sealed class ConstellationChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<ConstellationDay>?> DaysProperty =
        AvaloniaProperty.Register<ConstellationChart, IReadOnlyList<ConstellationDay>?>(nameof(Days));

    private static readonly FontFamily Mono = new("avares://TheTimer/Assets/Fonts#JetBrains Mono");
    private static readonly Typeface MonoFace = new(Mono);
    private static readonly Typeface MonoBold = new(Mono, FontStyle.Normal, FontWeight.Medium);

    private static readonly Color Amber = Color.Parse("#FFB547");
    private static readonly Color Coral = Color.Parse("#FF7A59");
    private static readonly Color Cyan = Color.Parse("#7DD3FC");
    private static readonly Color Lavender = Color.Parse("#A78BFA");
    private static readonly Color Faint = Color.Parse("#5B638A");
    private static readonly Color Secondary = Color.Parse("#9AA3C7");

    private static readonly IBrush GridBrush = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush FaintBrush = new SolidColorBrush(Faint);
    private static readonly IBrush SecondaryBrush = new SolidColorBrush(Secondary);
    private static readonly IBrush AmberBrush = new SolidColorBrush(Amber);
    private static readonly IBrush StarBrush = new SolidColorBrush(Color.Parse("#FFF4D6"));

    private static readonly IBrush ActualFill = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
        GradientStops = [new GradientStop(Amber, 0), new GradientStop(Coral, 1)],
    };

    private static readonly IBrush GhostFill = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
        GradientStops = [new GradientStop(Color.FromArgb(0x14, 0x7D, 0xD3, 0xFC), 0), new GradientStop(Color.FromArgb(0x08, 0xA7, 0x8B, 0xFA), 1)],
    };

    private static readonly IPen GhostPen = new Pen(new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
        GradientStops = [new GradientStop(Color.FromArgb(0x8C, 0x7D, 0xD3, 0xFC), 0), new GradientStop(Color.FromArgb(0x80, 0xA7, 0x8B, 0xFA), 1)],
    }, 1.2);

    private static readonly IPen LinePen = new Pen(new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xB5, 0x47)), 1.2, lineJoin: PenLineJoin.Round);
    private static readonly IPen TodayPillPen = new Pen(new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xB5, 0x47)), 1);
    private static readonly IBrush TodayPillFill = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xB5, 0x47));

    private static readonly IBrush BeamBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
        GradientStops = [new GradientStop(Color.FromArgb(0x24, 0xFF, 0xB5, 0x47), 0), new GradientStop(Color.FromArgb(0x06, 0xFF, 0xB5, 0x47), 1)],
    };

    private static readonly IBrush StarGlow = new RadialGradientBrush
    {
        GradientStops = [new GradientStop(Color.FromArgb(0xB0, 0xFF, 0xC5, 0x6B), 0), new GradientStop(Color.FromArgb(0x00, 0xFF, 0xB5, 0x47), 1)],
    };

    private static readonly BoxShadows GlowNormal = BoxShadows.Parse("0 0 20 0 #66FF9650");
    private static readonly BoxShadows GlowStrong = BoxShadows.Parse("0 0 28 3 #99FF9650");

    private const double LeftGutter = 46;
    private const double TopBand = 34;
    private const double BottomBand = 38;

    static ConstellationChart()
    {
        AffectsRender<ConstellationChart>(DaysProperty);
    }

    public IReadOnlyList<ConstellationDay>? Days
    {
        get => GetValue(DaysProperty);
        set => SetValue(DaysProperty, value);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var index = ColumnAt(e.GetPosition(this).X);
        ToolTip.SetTip(this, index is { } i && Days is { } d ? d[i].ToolTip : null);
    }

    private int? ColumnAt(double x)
    {
        if (Days is not { Count: > 0 } days)
            return null;
        var colW = (Bounds.Width - LeftGutter) / days.Count;
        var i = (int)Math.Floor((x - LeftGutter) / colW);
        return i >= 0 && i < days.Count ? i : null;
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h)); // hit-test surface for the tooltips
        if (w < 120 || h < 120)
            return;

        var days = Days ?? [];
        var count = Math.Max(days.Count, 1);
        var peak = days.Count == 0 ? 0 : days.Max(d => Math.Max(d.ActualHours, d.PlannedHours));
        var step = peak <= 8 ? 2 : 4;
        var max = Math.Max(4, Math.Ceiling(peak / step) * step);
        var baseY = h - BottomBand;
        var plotH = baseY - TopBand;
        var colW = (w - LeftGutter) / count;
        double YOf(double hours) => baseY - hours / max * plotH;

        // Horizontal grid with hour labels.
        for (var hour = 0.0; hour <= max + 0.001; hour += step)
        {
            var y = YOf(hour);
            context.DrawLine(new Pen(GridBrush, 1), new Point(LeftGutter - 6, y), new Point(w, y));
            var label = Text(hour == 0 ? "0" : $"{hour:0} sa", 10, FaintBrush);
            context.DrawText(label, new Point(LeftGutter - 14 - label.Width, y - label.Height / 2));
        }

        var capW = Math.Clamp(colW * 0.34, 18, 38);
        var actW = Math.Max(10, capW * 0.62);
        var stars = new List<Point>(days.Count);

        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            var cx = LeftGutter + colW * (i + 0.5);

            if (day.IsToday)
            {
                var beam = new Rect(cx - colW * 0.42, 0, colW * 0.84, baseY + 8);
                context.DrawRectangle(BeamBrush, null, new RoundedRect(beam, new CornerRadius(14, 14, 0, 0)));
            }

            // Value row above the plot (omitted for a day without any time).
            if (day.ActualHours > 0 || day.PlannedHours > 0)
            {
                var actualLabel = Text(day.ActualText, 12, day.ActualHours > 0 ? AmberBrush : FaintBrush, bold: day.ActualHours > 0);
                var plannedLabel = Text(" / " + day.PlannedText, 12, FaintBrush);
                var lx = cx - (actualLabel.Width + plannedLabel.Width) / 2;
                context.DrawText(actualLabel, new Point(lx, 6));
                context.DrawText(plannedLabel, new Point(lx + actualLabel.Width, 6));
            }

            // Ghost planned capsule.
            if (day.PlannedHours > 0)
            {
                var ph = Math.Max(capW, day.PlannedHours / max * plotH);
                var rect = new Rect(cx - capW / 2, baseY - ph, capW, ph);
                context.DrawRectangle(GhostFill, GhostPen, new RoundedRect(rect, capW / 2));
            }

            // Filled actual capsule.
            if (day.ActualHours > 0)
            {
                var ah = Math.Max(actW, day.ActualHours / max * plotH);
                var rect = new Rect(cx - actW / 2, baseY - ah, actW, ah);
                var over = day.ActualHours > day.PlannedHours && day.PlannedHours > 0;
                context.DrawRectangle(ActualFill, null, new RoundedRect(rect, actW / 2), over ? GlowStrong : GlowNormal);
                stars.Add(new Point(cx, rect.Top + actW / 2));
            }
            else
            {
                stars.Add(new Point(cx, baseY));
            }

            // Day label.
            var dayLabel = Text(day.Label, 11, day.IsToday ? AmberBrush : SecondaryBrush, bold: day.IsToday);
            var dy = baseY + 12;
            if (day.IsToday)
            {
                var pill = new Rect(cx - dayLabel.Width / 2 - 10, dy - 3, dayLabel.Width + 20, dayLabel.Height + 6);
                context.DrawRectangle(TodayPillFill, TodayPillPen, new RoundedRect(pill, 999));
            }

            context.DrawText(dayLabel, new Point(cx - dayLabel.Width / 2, dy));
        }

        // Constellation: faint polyline, then the stars.
        if (stars.Count > 1 && days.Any(d => d.ActualHours > 0))
        {
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(stars[0], false);
                for (var i = 1; i < stars.Count; i++)
                    g.LineTo(stars[i]);
                g.EndFigure(false);
            }

            context.DrawGeometry(null, LinePen, geometry);
        }

        for (var i = 0; i < days.Count; i++)
        {
            if (days[i].ActualHours <= 0)
                continue;
            var p = stars[i];
            context.DrawEllipse(StarGlow, null, p, 11, 11);
            context.DrawEllipse(StarBrush, null, p, 3.6, 3.6);
        }
    }

    private static FormattedText Text(string text, double size, IBrush brush, bool bold = false) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, bold ? MonoBold : MonoFace, size, brush);
}
