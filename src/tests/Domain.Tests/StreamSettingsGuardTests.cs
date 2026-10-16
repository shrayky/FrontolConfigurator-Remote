using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class StreamSettingsGuardTests
{
    [Fact]
    public void IsAllowed_true_для_среднего_качества_и_обычной_скорости()
    {
        Assert.True(StreamSettingsGuard.IsAllowed(40, 1280, 67));
    }

    [Fact]
    public void IsAllowed_true_для_всех_пресетов()
    {
        Assert.True(StreamSettingsGuard.IsAllowed(25, 960, 200));
        Assert.True(StreamSettingsGuard.IsAllowed(60, 1600, 40));
    }

    [Fact]
    public void IsAllowed_false_для_чисел_вне_пресетов()
    {
        Assert.False(StreamSettingsGuard.IsAllowed(80, 1280, 67));
        Assert.False(StreamSettingsGuard.IsAllowed(40, 1920, 67));
        Assert.False(StreamSettingsGuard.IsAllowed(40, 1280, 16));
    }
}
