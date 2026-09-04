using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class DxgiDuplicationRecoverPolicyTests
{
    [Fact]
    public void после_успеха_сбои_не_делают_отказ_постоянным()
    {
        Assert.False(DxgiDuplicationRecoverPolicy.IsPermanentCreateFailure(everReady: true, consecutiveFailures: 100));
    }

    [Fact]
    public void до_лимита_первый_старт_ещё_не_отказ()
    {
        Assert.False(DxgiDuplicationRecoverPolicy.IsPermanentCreateFailure(
            everReady: false,
            consecutiveFailures: DxgiDuplicationRecoverPolicy.MaxInitialCreateFailures - 1));
    }

    [Fact]
    public void лимит_неудач_на_первом_старте_это_постоянный_отказ()
    {
        Assert.True(DxgiDuplicationRecoverPolicy.IsPermanentCreateFailure(
            everReady: false,
            consecutiveFailures: DxgiDuplicationRecoverPolicy.MaxInitialCreateFailures));
    }

    [Fact]
    public void таймаут_захвата_как_у_gdi()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(300), DxgiDuplicationRecoverPolicy.CaptureTimeout);
    }
}
