using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TheTimer.App.Controls;

/// <summary>
/// Outline icon drawn from a 24x24 geometry (see <c>Themes/Icons.axaml</c>, keys <c>Icon.*</c>).
/// Stroked with the inherited foreground so it follows button/tab states; <see cref="IsFilled"/> also fills the shape.
/// The glyphs are original simple strokes in the style of Material Symbols (outlined, rounded caps).
/// </summary>
public sealed class Icon : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<Icon, Geometry?>(nameof(Data));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        Avalonia.Controls.Documents.TextElement.ForegroundProperty.AddOwner<Icon>();

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<Icon, double>(nameof(StrokeThickness), 1.6);

    public static readonly StyledProperty<bool> IsFilledProperty =
        AvaloniaProperty.Register<Icon, bool>(nameof(IsFilled));

    static Icon()
    {
        AffectsRender<Icon>(DataProperty, ForegroundProperty, StrokeThicknessProperty, IsFilledProperty);
        AffectsMeasure<Icon>(WidthProperty, HeightProperty);
    }

    public Icon()
    {
        Width = 20;
        Height = 20;
        IsHitTestVisible = false;
    }

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public bool IsFilled
    {
        get => GetValue(IsFilledProperty);
        set => SetValue(IsFilledProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Width, Height);

    public override void Render(DrawingContext context)
    {
        if (Data is not { } geometry || Foreground is not { } brush)
            return;

        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24.0;
        var offset = new Vector((Bounds.Width - 24 * scale) / 2, (Bounds.Height - 24 * scale) / 2);
        var pen = new Pen(brush, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset.X, offset.Y)))
            context.DrawGeometry(IsFilled ? brush : null, pen, geometry);
    }
}
