namespace Domain.RemoteDesktop;

public static class RemoteHubClosedPolicy
{
    /// <summary>
    /// После Closed нужен новый Start, даже если сервер закрыл сокет без exception.
    /// </summary>
    public static bool ShouldReconnect(bool hostStopping) => !hostStopping;
}
