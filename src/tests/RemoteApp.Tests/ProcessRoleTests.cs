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
}
