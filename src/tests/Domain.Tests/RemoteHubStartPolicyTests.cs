using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class RemoteHubStartPolicyTests
{
    [Fact]
    public void стартует_если_хаба_нет()
    {
        Assert.True(RemoteHubStartPolicy.CanStart(RemoteConnectionPhase.None));
    }

    [Fact]
    public void стартует_если_хаб_отключён()
    {
        Assert.True(RemoteHubStartPolicy.CanStart(RemoteConnectionPhase.Disconnected));
    }

    [Theory]
    [InlineData(RemoteConnectionPhase.Connecting)]
    [InlineData(RemoteConnectionPhase.Connected)]
    [InlineData(RemoteConnectionPhase.Reconnecting)]
    public void не_стартует_пока_есть_живое_или_реконнектящееся_соединение(RemoteConnectionPhase phase)
    {
        Assert.False(RemoteHubStartPolicy.CanStart(phase));
    }
}
