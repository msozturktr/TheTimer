using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TheTimer.App.Controls.Plan;

/// <summary>Glowing coral "now" line across the ribbon with a "15:32 · ŞİMDİ" chip at the left.</summary>
public sealed class NowLine : Control
{
    public static readonly StyledProperty<int> RangeStartHourProperty =
        AvaloniaProperty.Register<NowLine, int>(nameof(RangeStartHour), 8);

    /// <summary>Minutes since midnight; negative hides the line.</summary>
    public static readonly StyledProperty<double> NowMinutesProperty =
        AvaloniaProperty.Register<NowLine, double>(nameof(NowMinutes), -1);

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<NowLine, string>(nameof(Text), string.Empty);

    static NowLine()
    {
        AffectsRender<NowLine>(RangeStartHourProperty, NowMinutesProperty, TextProperty);
    }

    public NowLine()
    {
        IsHitTestVisible = false;
    }

    public int RangeStartHour
    {
        get => GetValue(RangeStartHourProperty);
        set => SetValue(RangeStartHourProperty, value);
    }

    public double NowMinutes
    {
        get => GetValue(NowMinutesProperty);
        set => SetValue(NowMinutesProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (NowMinutes < 0)
            return;
        var coral = PlanPalette.Coral(this);
        var y = Math.Round(PlanTimeline.YFor(NowMinutes, RangeStartHour)) + 0.5;
        var x0 = 56.0;
        var x1 = Bounds.Width;

        var fade = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(coral, 0),
                new GradientStop(Color.FromArgb(0xB0, coral.R, coral.G, coral.B), 0.7),
                new GradientStop(Color.FromArgb(0x30, coral.R, coral.G, coral.B), 1),
            ],
        };
        // Glow = a few wide translucent strokes under the crisp line.
        foreach (var (w, a) in new[] { (9.0, 0x14), (5.0, 0x26), (2.8, 0x40) })
            context.DrawLine(
                new Pen(new SolidColorBrush(Color.FromArgb((byte)a, coral.R, coral.G, coral.B)), w, lineCap: PenLineCap.Round),
                new Point(x0, y), new Point(x1, y));
        context.DrawLine(new Pen(fade, 1.6), new Point(x0, y), new Point(x1, y));

        var face = PlanPalette.Mono(this, FontWeight.Medium);
        var text = PlanPalette.Text(Text, face, 11, new SolidColorBrush(Color.FromRgb(0xFF, 0xE2, 0xD8)));
        var chip = new Rect(4, y - 11, text.Width + 34, 22);
        context.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(0xEE, 0x3A, 0x16, 0x1E)), new Pen(new SolidColorBrush(coral), 1), chip, 11, 11);
        context.DrawEllipse(new SolidColorBrush(coral), null, new Point(chip.X + 12, y), 3, 3);
        context.DrawText(text, new Point(chip.X + 22, y - text.Height / 2));
    }
}
