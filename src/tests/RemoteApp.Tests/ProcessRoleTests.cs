using Xunit;

namespace RemoteApp.Tests;

public class ProcessRoleTests
{
    [Fact]
    public void Service_в_session_0_это_сторож()
    {
        Assert.True(ProcessRole.IsWatchdog(["--service"], sessionId: 0));
    }

    [Fact]
    public void Service_desktop_в_session_0_это_не_сторож()
    {
        Assert.False(ProcessRole.IsWatchdog(["--service", "--desktop"], sessionId: 0));
    }

    [Fact]
    public void Service_вне_session_0_это_не_сторож()
    {
        Assert.False(ProcessRole.IsWatchdog(["--service"], sessionId: 1));
    }

    [Fact]
    public void имя_роли_сторож_в_session_0()
    {
        Assert.Equal("watchdog", ProcessRole.LogName(["--service"], sessionId: 0));
    }

    [Fact]
    public void имя_роли_desktop_на_столе()
    {
        Assert.Equal("desktop", ProcessRole.LogName(["--service", "--desktop"], sessionId: 1));
    }
}
