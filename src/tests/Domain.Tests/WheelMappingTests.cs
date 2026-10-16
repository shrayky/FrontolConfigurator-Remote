using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class WheelMappingTests
{
    [Fact]
    public void щелчок_вниз_даёт_минус_120()
    {
        var (mouseData, remainder) = WheelMapping.Consume(0, 100);

        Assert.Equal(-120, mouseData);
        Assert.Equal(-20, remainder);
    }

    [Fact]
    public void щелчок_вверх_даёт_плюс_120()
    {
        var (mouseData, remainder) = WheelMapping.Consume(0, -100);

        Assert.Equal(120, mouseData);
        Assert.Equal(20, remainder);
    }

    [Fact]
    public void мелкие_шаги_копятся_до_щелчка()
    {
        var (first, remainder) = WheelMapping.Consume(0, 40);
        var (second, next) = WheelMapping.Consume(remainder, 40);
        var (third, last) = WheelMapping.Consume(next, 40);

        Assert.Equal(0, first);
        Assert.Equal(0, second);
        Assert.Equal(-120, third);
        Assert.Equal(0, last);
    }

    [Fact]
    public void большой_шаг_даёт_несколько_щелчков()
    {
        var (mouseData, remainder) = WheelMapping.Consume(0, 250);

        Assert.Equal(-240, mouseData);
        Assert.Equal(10, remainder);
    }

    [Fact]
    public void ноль_не_тратит_остаток()
    {
        var (mouseData, remainder) = WheelMapping.Consume(30, 0);

        Assert.Equal(0, mouseData);
        Assert.Equal(30, remainder);
    }
}
