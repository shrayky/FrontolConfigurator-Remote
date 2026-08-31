using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class RemoteHubCancelKindTests
{
    [Fact]
    public void отмена_хоста_это_не_таймаут_handshake()
    {
        Assert.Equal(RemoteHubCancelKind.HostStop, RemoteHubCancelKindPolicy.Classify(cancellationRequested: true));
    }

    [Fact]
    public void operation_canceled_без_cts_это_таймаут_handshake()
    {
        Assert.Equal(RemoteHubCancelKind.HandshakeTimeout, RemoteHubCancelKindPolicy.Classify(cancellationRequested: false));
    }
}
