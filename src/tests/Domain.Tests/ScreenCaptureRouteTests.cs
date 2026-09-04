using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class ScreenCaptureRouteTests
{
    [Fact]
    public void dxgi_готов_берём_dxgi()
    {
        var source = ScreenCaptureRoute.Decide(
            dxgiInitFailed: false, alphaHoles: false, dxgiReady: true, everReady: true);

        Assert.Equal(ScreenCaptureSource.Dxgi, source);
    }

    [Fact]
    public void access_lost_этот_кадр_через_gdi()
    {
        var source = ScreenCaptureRoute.Decide(
            dxgiInitFailed: false, alphaHoles: false, dxgiReady: false, everReady: true);

        Assert.Equal(ScreenCaptureSource.GdiOneFrame, source);
    }

    [Fact]
    public void dxgi_ещё_не_поднимался_ждём_dxgi()
    {
        var source = ScreenCaptureRoute.Decide(
            dxgiInitFailed: false, alphaHoles: false, dxgiReady: false, everReady: false);

        Assert.Equal(ScreenCaptureSource.Dxgi, source);
    }

    [Fact]
    public void живой_gdi_кадр_важнее_прошлого_dxgi()
    {
        var live = (Width: 2, Height: 2, Jpeg: new byte[] { 1, 2 });
        var last = (Width: 1, Height: 1, Jpeg: new byte[] { 9 });

        var picked = ScreenCaptureRoute.PreferLiveThenLast(live, last);

        Assert.Equal(live, picked);
    }

    [Fact]
    public void пустой_gdi_отдаём_прошлый_dxgi()
    {
        var live = (Width: 0, Height: 0, Jpeg: Array.Empty<byte>());
        var last = (Width: 800, Height: 600, Jpeg: new byte[] { 0xFF, 0xD8 });

        var picked = ScreenCaptureRoute.PreferLiveThenLast(live, last);

        Assert.Equal(last, picked);
    }

    [Fact]
    public void dxgi_не_поднялся_берём_gdi_навсегда()
    {
        var source = ScreenCaptureRoute.Decide(dxgiInitFailed: true, alphaHoles: false);

        Assert.Equal(ScreenCaptureSource.GdiPermanent, source);
    }

    [Fact]
    public void альфа_дыры_только_этот_кадр_через_gdi()
    {
        var source = ScreenCaptureRoute.Decide(dxgiInitFailed: false, alphaHoles: true);

        Assert.Equal(ScreenCaptureSource.GdiOneFrame, source);
    }

    [Fact]
    public void постоянный_отказ_важнее_альфа_дыр()
    {
        var source = ScreenCaptureRoute.Decide(dxgiInitFailed: true, alphaHoles: true);

        Assert.Equal(ScreenCaptureSource.GdiPermanent, source);
    }
}
