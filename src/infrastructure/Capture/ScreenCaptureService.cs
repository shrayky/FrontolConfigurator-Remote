using System.Runtime.Versioning;
using Domain.RemoteDesktop;
using Microsoft.Extensions.Logging;

namespace Capture;

public sealed class ScreenCaptureService : IScreenCapture
{
    private readonly DxgiScreenCapture _dxgi;
    private readonly GdiScreenCapture _gdi;
    private readonly ILogger<ScreenCaptureService> _logger;
    private bool _loggedAlphaHoles;
    private bool _loggedDxgiDown;

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
    /// Сначала DXGI. После ACCESS_LOST этот кадр из GDI; если GDI пустой — прошлый DXGI JPEG.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public (int Width, int Height, byte[] Jpeg) Capture()
    {
        if (!OperatingSystem.IsWindows())
            return (0, 0, []);

        if (_dxgi.InitFailed)
            _dxgi.TryScheduleReProbe();

        if (!_dxgi.InitFailed)
        {
            var frame = _dxgi.Capture();
            var source = ScreenCaptureRoute.Decide(
                _dxgi.InitFailed,
                _dxgi.LastFrameHasAlphaHoles,
                _dxgi.IsReady,
                _dxgi.EverReady);
            if (source == ScreenCaptureSource.Dxgi)
            {
                _loggedDxgiDown = false;
                return frame;
            }

            if (source == ScreenCaptureSource.GdiOneFrame)
            {
                if (!_loggedAlphaHoles)
                {
                    _logger.LogWarning("DXGI-кадр с альфа-дырами или ACCESS_LOST, этот кадр через GDI");
                    _loggedAlphaHoles = true;
                }

                return ScreenCaptureRoute.PreferLiveThenLast(_gdi.Capture(), frame);
            }
        }

        if (!_loggedDxgiDown)
        {
            _loggedDxgiDown = true;
            _logger.LogWarning("DXGI недоступен, захват через GDI");
        }

        return _gdi.Capture();
    }
}
