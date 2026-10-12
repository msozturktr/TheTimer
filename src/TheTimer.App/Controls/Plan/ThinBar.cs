using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TheTimer.App.Controls.Plan;

/// <summary>Thin progress bar: faint track with an amber to coral fill for <see cref="Fraction"/> (0..1).</summary>
public sealed class ThinBar : Control
{
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<ThinBar, double>(nameof(Fraction));

    static ThinBar()
    {
        AffectsRender<ThinBar>(FractionProperty);
    }

    public ThinBar()
    {
        Height = 3;
        IsHitTestVisible = false;
    }

    public double Fraction
    {
        get => GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)), null, new Rect(0, 0, w, h), h / 2, h / 2);
        var fill = Math.Clamp(Fraction, 0, 1) * w;
        if (fill <= 0.5)
            return;
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops = [new GradientStop(PlanPalette.Amber(this), 0), new GradientStop(PlanPalette.Coral(this), 1)],
        };
        context.DrawRectangle(brush, null, new Rect(0, 0, fill, h), h / 2, h / 2);
    }
}
