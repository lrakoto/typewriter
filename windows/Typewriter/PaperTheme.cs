using System.Windows.Media;

namespace Typewriter.Windows;

internal sealed record PaperTheme(Color Paper, Color Ink, Color Accent)
{
    public static PaperTheme For(string theme) => theme switch {
        "parchment" => new(Color.FromRgb(217, 204, 179), Color.FromRgb(56, 60, 56), Color.FromRgb(100, 102, 79)),
        "midnight" => new(Color.FromRgb(36, 41, 41), Color.FromRgb(214, 209, 189), Color.FromRgb(189, 173, 122)),
        _ => new(Color.FromRgb(224, 223, 214), Color.FromRgb(56, 60, 56), Color.FromRgb(100, 102, 79))
    };
    public SolidColorBrush InkBrush(double opacity = 1) => new(Ink) { Opacity = opacity };
    public SolidColorBrush PaperBrush => new(Paper);
}
