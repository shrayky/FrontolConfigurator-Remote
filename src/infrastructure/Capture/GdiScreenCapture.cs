using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Domain.RemoteDesktop;
using Microsoft.Extensions.Logging;

namespace Capture;

public sealed class GdiScreenCapture
{
    private const int MaxDimension = 1280;
    private const long JpegQuality = 40;

    private readonly ILogger<GdiScreenCapture> _logger;

    public GdiScreenCapture(ILogger<GdiScreenCapture> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Захватывает основной экран, масштабирует так, чтобы max(ширина, высота) ≤ 1280, и кодирует JPEG качеством 40.
    /// При ошибке CopyFromScreen (Session 0) возвращает пустой кадр и не падает.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public (int Width, int Height, byte[] Jpeg) Capture()
    {
        if (!OperatingSystem.IsWindows())
            return (0, 0, []);

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

            DrawCursor(source);

            var (width, height) = ScaleToFit(srcWidth, srcHeight, MaxDimension);
            if (width == srcWidth && height == srcHeight)
                return (width, height, EncodeJpeg(source));

            using var scaled = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(scaled))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(source, 0, 0, width, height);
            }

            return (width, height, EncodeJpeg(scaled));
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
    private static byte[] EncodeJpeg(Bitmap bitmap)
    {
        var codec = ImageCodecInfo.GetImageEncoders()
            .First(item => item.FormatID == ImageFormat.Jpeg.Guid);

        using var encoderParameters = new EncoderParameters(1);
        encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, JpegQuality);

        using var stream = new MemoryStream();
        bitmap.Save(stream, codec, encoderParameters);
        return stream.ToArray();
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
