using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TheTimer.App.ViewModels;

/// <summary>Deterministic tag colours (Astrolab accent tokens plus two softer extras), keyed by the lower-cased tag.</summary>
public static class TagPalette
{
    private static readonly Color[] Colors =
    [
        Color.Parse("#7DD3FC"), // cyan
        Color.Parse("#A78BFA"), // lavender
        Color.Parse("#5EEAD4"), // mint
        Color.Parse("#FF6B81"), // rose
        Color.Parse("#F9A8D4"), // pink
        Color.Parse("#86EFAC"), // green
    ];

    private static readonly Color None = Color.Parse("#9AA3C7");

    public static Color For(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return None;
        uint hash = 2166136261;
        foreach (var ch in tag.Trim().TrimStart('#').ToLowerInvariant())
            hash = (hash ^ ch) * 16777619;
        return Colors[hash % (uint)Colors.Length];
    }

    /// <summary>Text, faint fill and hairline border brushes for a tag pill.</summary>
    public static TagStyle Style(string? tag) => new(Solid(tag), Tint(tag, 0.07), Tint(tag, 0.45));

    public static IBrush Solid(string? tag) => new ImmutableSolidColorBrush(For(tag));

    /// <summary>The tag colour at a given opacity (0..1), for pill backgrounds and borders.</summary>
    public static IBrush Tint(string? tag, double opacity) => new ImmutableSolidColorBrush(For(tag), opacity);
}

/// <summary>Brushes of one tag pill: coloured text (also used for dots), faint fill and ~45% hairline border.</summary>
public sealed record TagStyle(IBrush Text, IBrush Fill, IBrush Border);
