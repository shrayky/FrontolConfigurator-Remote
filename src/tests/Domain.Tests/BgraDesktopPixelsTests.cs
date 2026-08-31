using Capture;
using Xunit;

namespace Domain.Tests;

public class BgraDesktopPixelsTests
{
    [Fact]
    public void нулевая_альфа_считается_дырой_и_становится_непрозрачной()
    {
        var bgra = new byte[] { 10, 20, 30, 0, 40, 50, 60, 255 };

        var holes = BgraDesktopPixels.FlattenRow(bgra);

        Assert.Equal(1, holes);
        Assert.Equal(255, bgra[3]);
        Assert.Equal(255, bgra[7]);
    }

    [Fact]
    public void непрозрачный_ряд_без_дыр()
    {
        var bgra = new byte[] { 1, 2, 3, 255, 4, 5, 6, 255 };

        var holes = BgraDesktopPixels.FlattenRow(bgra);

        Assert.Equal(0, holes);
    }

    [Fact]
    public void пять_процентов_дыр_это_порог()
    {
        Assert.False(BgraDesktopPixels.ExceedsHoleThreshold(4, 100));
        Assert.True(BgraDesktopPixels.ExceedsHoleThreshold(5, 100));
    }
}
