namespace Capture;

internal static class BgraDesktopPixels
{
    /// <summary>
    /// Считает пиксели с A=0 (дыры MPO) и ставит A=255, чтобы JPEG не заливал их чёрным.
    /// </summary>
    public static int FlattenRow(byte[] bgra)
    {
        var holes = 0;
        for (var i = 0; i < bgra.Length; i += 4)
        {
            if (bgra[i + 3] == 0)
                holes++;
            bgra[i + 3] = 255;
        }

        return holes;
    }

    /// <summary>
    /// True, если дыр не меньше 5% кадра — слой в duplication не попал, нужен GDI.
    /// </summary>
    public static bool ExceedsHoleThreshold(int holePixels, int totalPixels)
        => totalPixels > 0 && holePixels * 20 >= totalPixels;
}
