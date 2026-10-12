using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TheTimer.App.Controls.Today;

/// <summary>A slim rounded track with an amber-to-coral luminous fill covering <see cref="Fraction"/> (0..1) of the width.</summary>
public sealed class ProportionBar : Control
{
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<ProportionBar, double>(nameof(Fraction));

    private static readonly IBrush Track = new SolidColorBrush(Color.Parse("#14FFFFFF"));

    static ProportionBar()
    {
        AffectsRender<ProportionBar>(FractionProperty);
        AffectsMeasure<ProportionBar>(HeightProperty);
    }

    public ProportionBar()
    {
        Height = 4;
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
        var radius = h / 2;
        context.DrawRectangle(Track, null, new Rect(0, 0, w, h), radius, radius);

        var fill = Math.Clamp(Fraction, 0, 1) * w;
        if (fill <= 0)
            return;
        fill = Math.Max(fill, h);
        var rect = new Rect(0, 0, fill, h);
        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops = [new GradientStop(Color.Parse("#FFB547"), 0), new GradientStop(Color.Parse("#FF7A59"), 1)],
        };
        var glow = new LinearGradientBrush
        {
            StartPoint = gradient.StartPoint,
            EndPoint = gradient.EndPoint,
            Opacity = 0.22,
            GradientStops = gradient.GradientStops,
        };
        context.DrawRectangle(glow, null, rect.Inflate(new Thickness(0, 2.5)), h, h);
        context.DrawRectangle(gradient, null, rect, radius, radius);
    }
}
