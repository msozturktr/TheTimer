using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TheTimer.App.Controls.Timer;

/// <summary>A span of the day in minutes since local midnight.</summary>
public readonly record struct DaySegment(double StartMinute, double EndMinute);

/// <summary>
/// "Günün akışı": a slim track from 08:00 to 20:00 (widened when sessions fall outside) with today's sessions as
/// amber capsules, a coral tick for the current time and a hairline hour scale below.
/// </summary>
public sealed class DayRibbon : Control
{
    public static readonly StyledProperty<IReadOnlyList<DaySegment>?> SegmentsProperty =
        AvaloniaProperty.Register<DayRibbon, IReadOnlyList<DaySegment>?>(nameof(Segments));

    public static readonly StyledProperty<double> NowMinuteProperty =
        AvaloniaProperty.Register<DayRibbon, double>(nameof(NowMinute), -1);

    private const double TrackHeight = 22;
    private static readonly Color Amber = Color.Parse("#FFB547");
    private static readonly Color Coral = Color.Parse("#FF7A59");

    static DayRibbon()
    {
        AffectsRender<DayRibbon>(SegmentsProperty, NowMinuteProperty);
    }

    public DayRibbon()
    {
        Height = 50;
        IsHitTestVisible = false;
    }

    public IReadOnlyList<DaySegment>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public double NowMinute
    {
        get => GetValue(NowMinuteProperty);
        set => SetValue(NowMinuteProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        if (w < 20)
            return;

        var segments = Segments ?? [];
        var start = 8 * 60.0;
        var end = 20 * 60.0;
        foreach (var s in segments)
        {
            start = Math.Min(start, Math.Floor(s.StartMinute / 60) * 60);
            end = Math.Max(end, Math.Ceiling(s.EndMinute / 60) * 60);
        }

        if (NowMinute >= 0)
        {
            start = Math.Min(start, Math.Floor(NowMinute / 60) * 60);
            end = Math.Max(end, Math.Ceiling(NowMinute / 60) * 60);
        }

        var span = end - start;
        double X(double minute) => (minute - start) / span * w;

        // Track.
        var track = new RoundedRect(new Rect(0, 4, w, TrackHeight), TrackHeight / 2);
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#B30A0D20")), new Pen(new SolidColorBrush(Color.Parse("#14FFFFFF")), 1), track);

        // Session capsules (min width so very short sessions stay visible).
        var capsuleBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops = [new GradientStop(Amber, 0), new GradientStop(Color.Parse("#FF9650"), 1)],
        };
        var glow = new SolidColorBrush(Color.FromArgb(0x2E, 0xFF, 0x96, 0x50));
        foreach (var s in segments)
        {
            var x0 = X(s.StartMinute);
            var x1 = Math.Max(X(s.EndMinute), x0 + 8);
            var rect = new Rect(x0, 7, x1 - x0, TrackHeight - 6);
            context.DrawRectangle(glow, null, new RoundedRect(rect.Inflate(2), 99));
            context.DrawRectangle(capsuleBrush, null, new RoundedRect(rect, rect.Height / 2));
        }

        // Hour scale.
        var typeface = new Typeface(MonoFont());
        var faint = new SolidColorBrush(Color.Parse("#5B638A"));
        var nowX = NowMinute >= 0 ? X(NowMinute) : double.NaN;
        var step = span > 14 * 60 ? 4 * 60 : 3 * 60;
        for (var m = start; m <= end + 0.1; m += step)
        {
            var x = X(m);
            var text = Format($"{(int)(m / 60):00}", 9.5, faint, typeface);
            if (!double.IsNaN(nowX) && Math.Abs(x - nowX) < 26)
                continue;
            var tx = Math.Clamp(x - text.Width / 2, 0, w - text.Width);
            context.DrawText(text, new Point(tx, TrackHeight + 12));
        }

        // Now tick.
        if (!double.IsNaN(nowX))
        {
            var coral = new SolidColorBrush(Coral);
            context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0x7A, 0x59)), 5), new Point(nowX, 1), new Point(nowX, TrackHeight + 7));
            context.DrawLine(new Pen(coral, 1.6, lineCap: PenLineCap.Round), new Point(nowX, 1), new Point(nowX, TrackHeight + 7));
            var label = Format($"{(int)(NowMinute / 60):00}:{(int)(NowMinute % 60):00}", 9.5, coral, typeface);
            var lx = Math.Clamp(nowX - label.Width / 2, 0, w - label.Width);
            context.DrawText(label, new Point(lx, TrackHeight + 12));
        }
    }

    private FontFamily MonoFont() =>
        this.TryFindResource("Font.Mono", out var v) && v is FontFamily f ? f : FontFamily.Default;

    private static FormattedText Format(string text, double size, IBrush brush, Typeface typeface) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, brush);
}
