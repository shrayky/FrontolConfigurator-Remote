using System.Drawing;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace Capture;

public sealed class ScreenCaptureService : IScreenCapture
{
    private readonly DxgiScreenCapture _dxgi;
    private readonly GdiScreenCapture _gdi;
    private readonly ILogger<ScreenCaptureService> _logger;
    private bool _useDxgi = true;
    private bool _loggedAlphaHoles;

    public ScreenCaptureService(
        DxgiScreenCapture dxgi,
        GdiScreenCapture gdi,
        ILogger<ScreenCaptureService> logger)
    {
        _dxgi = dxgi;
        _gdi = gdi;
        _logger = logger;
    }

    /// <summary>
    /// Сначала DXGI (не зависает на панели задач). При альфа-дырах MPO кадр берётся из GDI, DXGI не отключается.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public (int Width, int Height, byte[] Jpeg) Capture()
    {
        if (!OperatingSystem.IsWindows())
            return (0, 0, []);

        if (_useDxgi)
        {
            var frame = _dxgi.Capture();
            if (_dxgi.IsReady)
            {
                if (_dxgi.LastFrameHasAlphaHoles)
                {
                    if (!_loggedAlphaHoles)
                    {
                        _logger.LogWarning("DXGI-кадр с альфа-дырами, этот кадр через GDI");
                        _loggedAlphaHoles = true;
                    }

                    return _gdi.Capture();
                }

                return frame;
            }

            _useDxgi = false;
            _logger.LogWarning("DXGI недоступен, захват через GDI");
        }

        return _gdi.Capture();
    }
}
