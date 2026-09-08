namespace Domain.RemoteDesktop;

public static class RemoteHubHeartbeatPolicy
{
    public static bool ShouldRefreshRegistration(RemoteConnectionPhase phase) =>
        phase == RemoteConnectionPhase.Connected;
}
