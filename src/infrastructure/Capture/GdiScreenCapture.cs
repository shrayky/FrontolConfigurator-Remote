using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Domain.RemoteDesktop;
using Microsoft.Extensions.Logging;

namespace Capture;

public sealed class GdiScreenCapture : IScreenCapture
{
    private static readonly TimeSpan GdiTimeout = TimeSpan.FromMilliseconds(300);

    private readonly StreamCaptureSettings _settings;
    private readonly ILogger<GdiScreenCapture> _logger;
    private Task<(int Width, int Height, byte[] Jpeg)>? _inFlight;
    private (int Width, int Height, byte[] Jpeg) _last = (0, 0, []);

    public GdiScreenCapture(StreamCaptureSettings settings, ILogger<GdiScreenCapture> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Захватывает основной экран через GDI. Если CopyFromScreen зависает дольше 300мс, отдаёт предыдущий кадр.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public (int Width, int Height, byte[] Jpeg) Capture()
    {
        if (!OperatingSystem.IsWindows())
            return (0, 0, []);

        if (_inFlight is { IsCompleted: false })
            return _last;

        _inFlight = Task.Run(CaptureCore);
        if (!_inFlight.Wait(GdiTimeout))
        {
            _logger.LogWarning("GDI-захват дольше 300мс, отдаём предыдущий кадр");
            return _last;
        }

        _last = _inFlight.Result;
        return _last;
    }

    /// <summary>
    /// Рисует курсор, масштабирует и кодирует уже полученный bitmap рабочего стола.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public (int Width, int Height, byte[] Jpeg) EncodeDesktopBitmap(Bitmap source)
    {
        DrawCursor(source);

        var (width, height) = ScaleToFit(source.Width, source.Height, _settings.MaxSide);
        if (width == source.Width && height == source.Height)
            return (width, height, EncodeWithinLimit(source));

        using var scaled = new Bitmap(width, height);
        using (var graphics = Graphics.FromImage(scaled))
        {
            graphics.InterpolationMode = InterpolationMode.Bilinear;
            graphics.DrawImage(source, 0, 0, width, height);
        }

        return (width, height, EncodeWithinLimit(scaled));
    }

    [SupportedOSPlatform("windows")]
    private (int Width, int Height, byte[] Jpeg) CaptureCore()
    {
        try
        {
            var srcWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            var srcHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
            if (srcWidth <= 0 || srcHeight <= 0)
                return (0, 0, []);

            using var source = new Bitmap(srcWidth, srcHeight);
            using (var graphics = Graphics.FromImage(source))
            {
                graphics.CopyFromScreen(0, 0, 0, 0, new Size(srcWidth, srcHeight));
            }

            return EncodeDesktopBitmap(source);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось захватить экран (возможно Session 0)");
            return (0, 0, []);
        }
    }

    private static (int Width, int Height) ScaleToFit(int width, int height, int maxDimension)
    {
        var largest = Math.Max(width, height);
        if (largest <= maxDimension)
            return (width, height);

        var scale = maxDimension / (double)largest;
        return ((int)Math.Round(width * scale), (int)Math.Round(height * scale));
    }

    [SupportedOSPlatform("windows")]
    private static byte[] EncodeJpeg(Bitmap bitmap, long quality)
    {
        var codec = ImageCodecInfo.GetImageEncoders()
            .First(item => item.FormatID == ImageFormat.Jpeg.Guid);

        using var encoderParameters = new EncoderParameters(1);
        encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);

        using var stream = new MemoryStream();
        bitmap.Save(stream, codec, encoderParameters);
        return stream.ToArray();
    }

    /// <summary>
    /// Кодирует кадр текущим качеством. Если JPEG длиннее лимита хаба, один раз повторяет с качеством 25.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private byte[] EncodeWithinLimit(Bitmap bitmap)
    {
        var jpeg = EncodeJpeg(bitmap, _settings.Quality);
        if (jpeg.Length <= StreamCaptureSettings.MaxJpegBytes)
            return jpeg;

        _logger.LogWarning(
            "JPEG {Bytes} байт длиннее {MaxBytes}, повтор с качеством {Quality}",
            jpeg.Length,
            StreamCaptureSettings.MaxJpegBytes,
            StreamCaptureSettings.FallbackQuality);

        jpeg = EncodeJpeg(bitmap, StreamCaptureSettings.FallbackQuality);
        if (jpeg.Length <= StreamCaptureSettings.MaxJpegBytes)
            return jpeg;

        _logger.LogWarning(
            "JPEG {Bytes} байт всё ещё длиннее {MaxBytes}, кадр не отправляем",
            jpeg.Length,
            StreamCaptureSettings.MaxJpegBytes);
        return [];
    }

    /// <summary>
    /// Рисует системный курсор на кадре: CopyFromScreen его не захватывает.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private void DrawCursor(Bitmap bitmap)
    {
        var iconInfo = new NativeMethods.IconInfo();
        var gotIcon = false;
        Graphics? graphics = null;
        var hdc = IntPtr.Zero;

        try
        {
            var cursorInfo = new NativeMethods.CursorInfo
            {
                cbSize = Marshal.SizeOf<NativeMethods.CursorInfo>()
            };

            if (!NativeMethods.GetCursorInfo(ref cursorInfo))
                return;

            if ((cursorInfo.flags & NativeMethods.CURSOR_SHOWING) == 0 || cursorInfo.hCursor == IntPtr.Zero)
                return;

            if (!NativeMethods.GetIconInfo(cursorInfo.hCursor, out iconInfo))
                return;

            gotIcon = true;

            var (x, y) = CursorDrawPosition.FromScreen(
                cursorInfo.ptScreenPos.X,
                cursorInfo.ptScreenPos.Y,
                iconInfo.xHotspot,
                iconInfo.yHotspot);

            graphics = Graphics.FromImage(bitmap);
            hdc = graphics.GetHdc();
            NativeMethods.DrawIconEx(
                hdc, x, y, cursorInfo.hCursor, 0, 0, 0, IntPtr.Zero, NativeMethods.DI_NORMAL);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось нарисовать курсор на кадре");
        }
        finally
        {
            if (hdc != IntPtr.Zero && graphics != null)
                graphics.ReleaseHdc(hdc);

            graphics?.Dispose();

            if (gotIcon)
            {
                if (iconInfo.hbmMask != IntPtr.Zero)
                    NativeMethods.DeleteObject(iconInfo.hbmMask);
                if (iconInfo.hbmColor != IntPtr.Zero)
                    NativeMethods.DeleteObject(iconInfo.hbmColor);
            }
        }
    }
}
