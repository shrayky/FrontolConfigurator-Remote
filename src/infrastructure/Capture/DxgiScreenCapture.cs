using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Domain.RemoteDesktop;
using Microsoft.Extensions.Logging;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Capture;

public sealed class DxgiScreenCapture : IDisposable
{
    private readonly GdiScreenCapture _encoder;
    private readonly ILogger<DxgiScreenCapture> _logger;

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGIOutputDuplication? _duplication;
    private ID3D11Texture2D? _staging;
    private int _stagingWidth;
    private int _stagingHeight;
    private (int Width, int Height, byte[] Jpeg) _last = (0, 0, []);
    private Task<(int Width, int Height, byte[] Jpeg)>? _inFlight;
    private bool _initFailed;
    private bool _everReady;
    private int _createFailures;
    private bool _lastFrameHasAlphaHoles;

    public DxgiScreenCapture(GdiScreenCapture encoder, ILogger<DxgiScreenCapture> logger)
    {
        _encoder = encoder;
        _logger = logger;
    }

    /// <summary>
    /// True, если Desktop Duplication создана и можно захватывать без GDI.
    /// </summary>
    public bool IsReady => _duplication != null;

    /// <summary>
    /// True, если DXGI на этой машине недоступен — дальше только GDI.
    /// </summary>
    public bool InitFailed => _initFailed;

    /// <summary>
    /// True, если duplication хотя бы раз поднималась — после ACCESS_LOST можно взять кадр из GDI.
    /// </summary>
    public bool EverReady => _everReady;

    /// <summary>
    /// True, если в последнем DXGI-кадре много пикселей с A=0 — слой MPO не попал в duplication.
    /// </summary>
    public bool LastFrameHasAlphaHoles => _lastFrameHasAlphaHoles;

    /// <summary>
    /// Захватывает рабочий стол через DXGI. ACCESS_LOST и зависание DuplicateOutput/Map не блокируют стример.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public (int Width, int Height, byte[] Jpeg) Capture()
    {
        if (!OperatingSystem.IsWindows() || _initFailed)
            return (0, 0, []);

        if (_inFlight is { IsCompleted: false })
            return _last;

        _inFlight = Task.Run(CaptureCore);
        if (!_inFlight.Wait(DxgiDuplicationRecoverPolicy.CaptureTimeout))
        {
            _logger.LogWarning("DXGI-захват дольше 300мс, отдаём предыдущий кадр");
            return _last;
        }

        _last = _inFlight.Result;
        return _last;
    }

    [SupportedOSPlatform("windows")]
    private (int Width, int Height, byte[] Jpeg) CaptureCore()
    {
        if (!EnsureDuplication())
            return _last;

        try
        {
            var result = _duplication!.AcquireNextFrame(80, out _, out var resource);
            if (result.Failure)
            {
                if (result.Code == Vortice.DXGI.ResultCode.WaitTimeout.Code)
                    return _last;

                Reset();
                if (result.Code != Vortice.DXGI.ResultCode.AccessLost.Code)
                    _logger.LogWarning("AcquireNextFrame HRESULT {Code}", result.Code);
                return _last;
            }

            try
            {
                using var texture = resource.QueryInterface<ID3D11Texture2D>();
                var desc = texture.Description;
                if (!Is32Bpp(desc.Format))
                {
                    _logger.LogWarning("DXGI-формат {Format} не 32bpp, кадр пропущен", desc.Format);
                    return _last;
                }

                EnsureStaging((int)desc.Width, (int)desc.Height, desc.Format);
                _context!.CopyResource(_staging, texture);

                var mapped = _context.Map(_staging!, 0, MapMode.Read);
                try
                {
                    using var bitmap = ToBitmap(mapped, (int)desc.Width, (int)desc.Height, desc.Format);
                    if (_lastFrameHasAlphaHoles)
                        return _last;

                    _last = _encoder.EncodeDesktopBitmap(bitmap);
                    return _last;
                }
                finally
                {
                    _context.Unmap(_staging, 0);
                }
            }
            finally
            {
                resource.Dispose();
                _duplication.ReleaseFrame();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка DXGI-захвата, сброс duplication");
            Reset();
            return _last;
        }
    }

    public void Dispose() => Reset();

    [SupportedOSPlatform("windows")]
    private bool EnsureDuplication()
    {
        if (_duplication != null)
            return true;

        try
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            var target = DxgiDuplicationPicker.Pick(ListOutputs(factory));
            if (target is null)
            {
                _initFailed = true;
                _logger.LogWarning("Нет аппаратного DXGI-выхода на рабочем столе");
                return false;
            }

            factory.EnumAdapters1(target.Value.AdapterIndex, out var adapter);
            using (adapter)
            {
                D3D11.D3D11CreateDevice(
                    adapter.NativePointer,
                    DriverType.Unknown,
                    DeviceCreationFlags.BgraSupport,
                    [FeatureLevel.Level_11_0, FeatureLevel.Level_10_0],
                    out _device,
                    out _context);

                adapter.EnumOutputs(target.Value.OutputIndex, out var output);
                using (output)
                using (var output1 = output.QueryInterface<IDXGIOutput1>())
                {
                    _duplication = output1.DuplicateOutput(_device);
                }
            }

            _createFailures = 0;
            _everReady = true;
            _logger.LogInformation(
                "DXGI Desktop Duplication включён: {Adapter} {Output}",
                target.Value.AdapterName,
                target.Value.OutputName);
            return true;
        }
        catch (Exception ex)
        {
            _createFailures++;
            _initFailed = DxgiDuplicationRecoverPolicy.IsPermanentCreateFailure(_everReady, _createFailures);
            _logger.LogWarning(ex, "Не удалось создать DXGI Desktop Duplication");
            Reset();
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private void EnsureStaging(int width, int height, Format format)
    {
        if (_staging != null && _stagingWidth == width && _stagingHeight == height)
            return;

        _staging?.Dispose();
        _staging = _device!.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = format,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            CPUAccessFlags = CpuAccessFlags.Read,
            BindFlags = BindFlags.None
        });
        _stagingWidth = width;
        _stagingHeight = height;
    }

    private static bool Is32Bpp(Format format) =>
        format is Format.B8G8R8A8_UNorm or Format.B8G8R8A8_UNorm_SRgb or Format.R8G8B8A8_UNorm or Format.R8G8B8A8_UNorm_SRgb;

    /// <summary>
    /// Собирает выходы всех адаптеров, чтобы выбрать не WARP и не отключённый монитор.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static List<DxgiOutputCandidate> ListOutputs(IDXGIFactory1 factory)
    {
        var candidates = new List<DxgiOutputCandidate>();
        for (uint adapterIndex = 0; factory.EnumAdapters1(adapterIndex, out var adapter).Success; adapterIndex++)
        {
            using (adapter)
            {
                var adapterDesc = adapter.Description1;
                var software = adapterDesc.Flags.HasFlag(AdapterFlags.Software);
                for (uint outputIndex = 0; adapter.EnumOutputs(outputIndex, out var output).Success; outputIndex++)
                {
                    using (output)
                    {
                        var outputDesc = output.Description;
                        var rect = outputDesc.DesktopCoordinates;
                        candidates.Add(new DxgiOutputCandidate(
                            adapterIndex,
                            outputIndex,
                            software,
                            outputDesc.AttachedToDesktop,
                            rect.Left,
                            rect.Top,
                            adapterDesc.Description ?? "",
                            outputDesc.DeviceName ?? ""));
                    }
                }
            }
        }

        return candidates;
    }

    /// <summary>
    /// Копирует BGRA в Bitmap, выставляет A=255 и запоминает, есть ли дыры MPO.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private Bitmap ToBitmap(MappedSubresource mapped, int width, int height, Format format)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var bits = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);

        var rowBytes = width * 4;
        var row = new byte[rowBytes];
        var swapRb = format is Format.R8G8B8A8_UNorm or Format.R8G8B8A8_UNorm_SRgb;
        var holes = 0;
        for (var y = 0; y < height; y++)
        {
            Marshal.Copy(IntPtr.Add(mapped.DataPointer, y * (int)mapped.RowPitch), row, 0, rowBytes);
            if (swapRb)
            {
                for (var i = 0; i < rowBytes; i += 4)
                    (row[i], row[i + 2]) = (row[i + 2], row[i]);
            }

            holes += BgraDesktopPixels.FlattenRow(row);
            Marshal.Copy(row, 0, IntPtr.Add(bits.Scan0, y * bits.Stride), rowBytes);
        }

        bitmap.UnlockBits(bits);
        _lastFrameHasAlphaHoles = BgraDesktopPixels.ExceedsHoleThreshold(holes, width * height);
        return bitmap;
    }

    private void Reset()
    {
        _duplication?.Dispose();
        _duplication = null;
        _staging?.Dispose();
        _staging = null;
        _context?.Dispose();
        _context = null;
        _device?.Dispose();
        _device = null;
        _lastFrameHasAlphaHoles = false;
    }
}
