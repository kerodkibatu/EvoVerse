
using Raylib_cs;

namespace EvoVerse;

public static class Utils
{
    // Random Color
    public static Color RandomColor()
    {
        return new Color(
            Random.Shared.Next(256),
            Random.Shared.Next(256),
            Random.Shared.Next(256),
            255
        );
    }

    // Get a random color in the HSV color space
    public static Color SampleHSV(float intensity = 0.5f)
    {
        // Generate random hue in HSV color space
        float hue = (float)Random.Shared.NextDouble() * 360f;

        // Convert HSV to RGB
        return Raylib.ColorFromHSV(hue, intensity, intensity);
    }
}
