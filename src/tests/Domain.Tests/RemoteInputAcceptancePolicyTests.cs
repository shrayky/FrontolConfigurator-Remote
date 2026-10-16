using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class RemoteInputAcceptancePolicyTests
{
    private static readonly Guid Session = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Other = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void ввод_своей_сессии_принимается()
    {
        var apply = RemoteInputAcceptancePolicy.ShouldApply(
            streamRunning: true,
            activeSessionId: Session,
            incomingSessionId: Session);

        Assert.True(apply);
    }

    [Fact]
    public void ввод_чужой_сессии_отбрасывается()
    {
        var apply = RemoteInputAcceptancePolicy.ShouldApply(
            streamRunning: true,
            activeSessionId: Session,
            incomingSessionId: Other);

        Assert.False(apply);
    }

    [Fact]
    public void без_активного_стрима_ввод_отбрасывается()
    {
        var apply = RemoteInputAcceptancePolicy.ShouldApply(
            streamRunning: false,
            activeSessionId: Guid.Empty,
            incomingSessionId: Session);

        Assert.False(apply);
    }

    [Fact]
    public void событие_без_sessionId_принимается_при_живом_стриме()
    {
        var apply = RemoteInputAcceptancePolicy.ShouldApply(
            streamRunning: true,
            activeSessionId: Session,
            incomingSessionId: Guid.Empty);

        Assert.True(apply);
    }

    [Fact]
    public void событие_без_sessionId_отбрасывается_без_стрима()
    {
        var apply = RemoteInputAcceptancePolicy.ShouldApply(
            streamRunning: false,
            activeSessionId: Session,
            incomingSessionId: Guid.Empty);

        Assert.False(apply);
    }
}
