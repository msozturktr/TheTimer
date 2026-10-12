using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TheTimer.App.Controls;

/// <summary>
/// The Astrolab night sky: a radial gradient (#141A3A centre to #07091A edges) with deterministic, static faint stars.
/// Stars are generated from a fixed seed whenever the size changes; nothing animates.
/// </summary>
public sealed class StarField : Control
{
    public static readonly StyledProperty<int> SeedProperty =
        AvaloniaProperty.Register<StarField, int>(nameof(Seed), 7);

    private static readonly IBrush Sky = new RadialGradientBrush
    {
        Center = new RelativePoint(0.5, 0.42, RelativeUnit.Relative),
        GradientOrigin = new RelativePoint(0.5, 0.42, RelativeUnit.Relative),
        RadiusX = RelativeScalar.Parse("80%"),
        RadiusY = RelativeScalar.Parse("80%"),
        GradientStops =
        [
            new GradientStop(Color.Parse("#141A3A"), 0),
            new GradientStop(Color.Parse("#07091A"), 1),
        ],
    }.ToImmutable();

    private readonly record struct Star(double X, double Y, double Radius, IBrush Brush);

    private Star[] _stars = [];
    private Size _starsFor;

    static StarField()
    {
        AffectsRender<StarField>(SeedProperty);
    }

    public StarField()
    {
        IsHitTestVisible = false;
    }

    public int Seed
    {
        get => GetValue(SeedProperty);
        set => SetValue(SeedProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Sky, bounds);

        if (_starsFor != Bounds.Size)
        {
            _stars = Generate(Bounds.Size, Seed);
            _starsFor = Bounds.Size;
        }

        foreach (var star in _stars)
            context.DrawEllipse(star.Brush, null, new Point(star.X, star.Y), star.Radius, star.Radius);
    }

    private static Star[] Generate(Size size, int seed)
    {
        var count = (int)Math.Clamp(size.Width * size.Height / 5500, 0, 600);
        var random = new Random(seed);
        var stars = new Star[count];
        for (var i = 0; i < count; i++)
        {
            var x = random.NextDouble() * size.Width;
            var y = random.NextDouble() * size.Height;
            var bright = random.NextDouble() < 0.06;
            var radius = bright ? 0.9 + random.NextDouble() * 0.6 : 0.25 + random.NextDouble() * 0.5;
            var opacity = bright ? 0.6 + random.NextDouble() * 0.25 : 0.15 + random.NextDouble() * 0.4;
            // A few stars lean warm or cool, the rest stay near-white.
            var tint = random.NextDouble();
            var color = tint < 0.12 ? Color.FromRgb(0xFF, 0xD9, 0xA8) : tint < 0.24 ? Color.FromRgb(0xB8, 0xD4, 0xFF) : Color.FromRgb(0xEE, 0xF0, 0xFA);
            stars[i] = new Star(x, y, radius, new ImmutableSolidColorBrush(color, opacity));
        }

        return stars;
    }
}
