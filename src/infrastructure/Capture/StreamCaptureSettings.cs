using Domain.RemoteDesktop;
using Domain.RemoteDesktop.Dto;

namespace Capture;

public sealed class StreamCaptureSettings
{
    public const int DefaultQuality = 40;
    public const int DefaultMaxSide = 1280;
    public const int DefaultIntervalMs = 67;
    public const int FallbackQuality = 25;
    public const int MaxJpegBytes = 1_000_000;

    private int _quality = DefaultQuality;
    private int _maxSide = DefaultMaxSide;
    private int _intervalMs = DefaultIntervalMs;

    public int Quality => Volatile.Read(ref _quality);
    public int MaxSide => Volatile.Read(ref _maxSide);
    public int IntervalMs => Volatile.Read(ref _intervalMs);

    public void Apply(RemoteStreamSettings settings)
    {
        if (settings == null
            || !StreamSettingsGuard.IsAllowed(settings.Quality, settings.MaxSide, settings.IntervalMs))
            return;

        Volatile.Write(ref _quality, settings.Quality);
        Volatile.Write(ref _maxSide, settings.MaxSide);
        Volatile.Write(ref _intervalMs, settings.IntervalMs);
    }
}
