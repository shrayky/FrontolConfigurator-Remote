using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class TaskManagerLaunchTests
{
    /// <summary>
    /// На кассе запускаем стандартный taskmgr, не SAS.
    /// </summary>
    [Fact]
    public void FileName_taskmgr()
    {
        Assert.Equal("taskmgr.exe", TaskManagerLaunch.FileName);
    }
}
