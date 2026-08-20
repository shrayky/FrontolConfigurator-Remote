using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class CursorDrawPositionTests
{
    [Fact]
    public void вычитает_hotspot_из_позиции_курсора()
    {
        var point = CursorDrawPosition.FromScreen(cursorX: 100, cursorY: 80, hotspotX: 4, hotspotY: 2);

        Assert.Equal(96, point.X);
        Assert.Equal(78, point.Y);
    }
}
