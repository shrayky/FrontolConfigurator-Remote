namespace Domain.RemoteDesktop;

public static class StreamSettingsGuard
{
    public static bool IsAllowed(int quality, int maxSide, int intervalMs) =>
        quality is 25 or 40 or 60
        && maxSide is 960 or 1280 or 1600
        && intervalMs is 40 or 67 or 200;
}
