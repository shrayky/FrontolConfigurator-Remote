namespace Domain.RemoteDesktop;

/// <summary>
/// Таймауты и паузы SignalR: короткий реконнект, быстрый обрыв TCP, запас по ping при JPEG.
/// </summary>
public static class RemoteHubConnectPolicy
{
    public static readonly TimeSpan[] ReconnectDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(3)
    ];

    public static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan ServerTimeout = TimeSpan.FromSeconds(90);
    public static readonly TimeSpan ClosedRetryDelay = TimeSpan.FromSeconds(2);
}
