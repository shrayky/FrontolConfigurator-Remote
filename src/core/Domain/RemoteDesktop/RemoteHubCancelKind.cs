namespace Domain.RemoteDesktop;

public enum RemoteHubCancelKind
{
    HostStop,
    HandshakeTimeout
}

public static class RemoteHubCancelKindPolicy
{
    /// <summary>
    /// HandshakeTimeout SignalR бросает OperationCanceledException, наш CTS при этом не отменён.
    /// </summary>
    public static RemoteHubCancelKind Classify(bool cancellationRequested) =>
        cancellationRequested ? RemoteHubCancelKind.HostStop : RemoteHubCancelKind.HandshakeTimeout;
}
