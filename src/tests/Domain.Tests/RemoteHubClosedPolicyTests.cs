using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class RemoteHubClosedPolicyTests
{
    [Fact]
    public void после_закрытия_хаба_переподключаемся()
    {
        Assert.True(RemoteHubClosedPolicy.ShouldReconnect(hostStopping: false));
    }

    [Fact]
    public void при_остановке_хоста_не_переподключаемся()
    {
        Assert.False(RemoteHubClosedPolicy.ShouldReconnect(hostStopping: true));
    }
}
