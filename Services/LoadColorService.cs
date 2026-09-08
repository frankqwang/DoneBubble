using System;
using System.Windows.Media;
namespace DoneBubble.Services;
public static class LoadColorService
{
    public static Color ForPoints(int points)
    {
        points = Math.Max(0, points);
        // Keep exact multiples of five red; the next point begins a new cycle.
        int cycle = points == 0 ? 0 : (points - 1) / 5;
        double progress = points == 0 ? 0 : ((points - 1) % 5) / 4.0;
        // Cumulative load approaches a limit so even long days remain legible.
        double load = 1 - Math.Exp(-cycle / 5.0);
        var green = Mix(Color.FromRgb(157, 191, 171), Color.FromRgb(181, 164, 140), load);
        var amber = Mix(Color.FromRgb(203, 188, 148), Color.FromRgb(183, 150, 125), load);
        var red = Mix(Color.FromRgb(207, 159, 157), Color.FromRgb(181, 125, 125), load);
        return progress <= 0.5 ? Mix(green, amber, progress * 2) : Mix(amber, red, (progress - 0.5) * 2);
    }
    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));
}
