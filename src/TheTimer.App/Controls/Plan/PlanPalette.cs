using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TheTimer.App.Controls.Plan;

/// <summary>Resolves Astrolab colour tokens for custom-drawn controls, with the token values as fallback.</summary>
internal static class PlanPalette
{
    public static Color Get(Control control, string key, string fallback) =>
        control.TryFindResource(key, control.ActualThemeVariant, out var value) && value is Color color
            ? color
            : Color.Parse(fallback);

    public static Color Cyan(Control c) => Get(c, "Color.Cyan", "#7DD3FC");

    public static Color Amber(Control c) => Get(c, "Color.Amber", "#FFB547");

    public static Color Coral(Control c) => Get(c, "Color.Coral", "#FF7A59");

    public static Color Faint(Control c) => Get(c, "Color.TextFaint", "#5B638A");

    public static Typeface Mono(Control c, FontWeight weight = FontWeight.Normal) =>
        new(c.TryFindResource("Font.Mono", c.ActualThemeVariant, out var f) && f is FontFamily family
            ? family
            : FontFamily.Default, FontStyle.Normal, weight);

    public static FormattedText Text(string text, Typeface face, double size, IBrush brush) =>
        new(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, brush);
}
