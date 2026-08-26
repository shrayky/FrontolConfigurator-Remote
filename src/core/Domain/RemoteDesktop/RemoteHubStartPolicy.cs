namespace Domain.RemoteDesktop;

public enum RemoteConnectionPhase
{
    None,
    Disconnected,
    Connecting,
    Connected,
    Reconnecting
}

public static class RemoteHubStartPolicy
{
    /// <summary>
    /// Разрешает новый Start только если хаба нет или он полностью отключён.
    /// Иначе появится второе соединение и сорвутся кадры и мышь.
    /// </summary>
    public static bool CanStart(RemoteConnectionPhase phase) =>
        phase is RemoteConnectionPhase.None or RemoteConnectionPhase.Disconnected;
}
