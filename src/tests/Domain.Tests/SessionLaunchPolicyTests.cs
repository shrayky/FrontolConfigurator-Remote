using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class SessionLaunchPolicyTests
{
    [Fact]
    public void под_SYSTEM_берём_токен_winlogon()
    {
        var path = SessionLaunchPolicy.Decide(runningAsSystem: true);

        Assert.Equal(SessionLaunchPath.WinlogonToken, path);
    }

    [Fact]
    public void не_SYSTEM_работаем_как_раньше_через_токен_пользователя()
    {
        var path = SessionLaunchPolicy.Decide(runningAsSystem: false);

        Assert.Equal(SessionLaunchPath.UserToken, path);
    }

    [Fact]
    public void политика_покрывает_оба_варианта_без_исключений()
    {
        foreach (var runningAsSystem in new[] { true, false })
        {
            var path = SessionLaunchPolicy.Decide(runningAsSystem);

            Assert.True(Enum.IsDefined(path));
        }
    }
}
