namespace Domain.RemoteDesktop;

public enum ScreenCaptureSource
{
    Dxgi,
    GdiOneFrame,
    GdiPermanent
}

public static class ScreenCaptureRoute
{
    /// <summary>
    /// DXGI после ACCESS_LOST на этот кадр берём GDI; навсегда GDI только если DXGI так и не поднялся.
    /// </summary>
    public static ScreenCaptureSource Decide(
        bool dxgiInitFailed,
        bool alphaHoles,
        bool dxgiReady = true,
        bool everReady = false)
    {
        if (dxgiInitFailed)
            return ScreenCaptureSource.GdiPermanent;
        if (alphaHoles)
            return ScreenCaptureSource.GdiOneFrame;
        if (everReady && !dxgiReady)
            return ScreenCaptureSource.GdiOneFrame;
        return ScreenCaptureSource.Dxgi;
    }

    /// <summary>
    /// Живой JPEG, иначе прошлый кадр — чтобы пустой GDI при зависании CopyFromScreen не обрывал стрим.
    /// </summary>
    public static (int Width, int Height, byte[] Jpeg) PreferLiveThenLast(
        (int Width, int Height, byte[] Jpeg) live,
        (int Width, int Height, byte[] Jpeg) last)
    {
        if (live.Jpeg is { Length: > 0 })
            return live;
        return last;
    }
}
