using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class RemoteHubHeartbeatPolicyTests
{
    [Fact]
    public void обновляет_регистрацию_только_когда_хаб_подключён()
    {
        Assert.True(RemoteHubHeartbeatPolicy.ShouldRefreshRegistration(RemoteConnectionPhase.Connected));
    }

    [Theory]
    [InlineData(RemoteConnectionPhase.None)]
    [InlineData(RemoteConnectionPhase.Disconnected)]
    [InlineData(RemoteConnectionPhase.Connecting)]
    [InlineData(RemoteConnectionPhase.Reconnecting)]
    public void не_обновляет_регистрацию_если_нет_живого_сокета(RemoteConnectionPhase phase)
    {
        Assert.False(RemoteHubHeartbeatPolicy.ShouldRefreshRegistration(phase));
    }
}
