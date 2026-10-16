using RemoteApp.Services;
using Xunit;

namespace RemoteApp.Tests;

public class DesktopWatchdogTests
{
    [Fact]
    public void из_session_0_стартует_ребёнка_с_флагом_desktop()
    {
        var api = new FakeWindowsSessionApi
        {
            CurrentSessionId = 0,
            ConsoleSessionId = 2
        };
        var watchdog = new DesktopWatchdog(api);

        watchdog.Tick(@"C:\fc\fc-remote.exe", @"C:\fc");

        Assert.Equal(1, api.InSessionStarts);
        Assert.Equal(2u, api.LastSessionId);
        Assert.Equal("--service --desktop", api.LastArguments);
    }

    [Fact]
    public void без_консоли_не_стартует()
    {
        var api = new FakeWindowsSessionApi
        {
            CurrentSessionId = 0,
            ConsoleSessionId = null
        };
        var watchdog = new DesktopWatchdog(api);

        watchdog.Tick(@"C:\fc\fc-remote.exe", @"C:\fc");

        Assert.Equal(0, api.InSessionStarts);
    }

    [Fact]
    public void повторный_tick_не_плодит_второго_ребёнка()
    {
        var api = new FakeWindowsSessionApi
        {
            CurrentSessionId = 0,
            ConsoleSessionId = 2
        };
        var watchdog = new DesktopWatchdog(api);

        watchdog.Tick(@"C:\fc\fc-remote.exe", @"C:\fc");
        watchdog.Tick(@"C:\fc\fc-remote.exe", @"C:\fc");

        Assert.Equal(1, api.InSessionStarts);
    }

    [Fact]
    public void смена_сессии_перезапускает_ребёнка()
    {
        var api = new FakeWindowsSessionApi
        {
            CurrentSessionId = 0,
            ConsoleSessionId = 2
        };
        var watchdog = new DesktopWatchdog(api);

        watchdog.Tick(@"C:\fc\fc-remote.exe", @"C:\fc");
        api.ConsoleSessionId = 3;
        watchdog.Tick(@"C:\fc\fc-remote.exe", @"C:\fc");

        Assert.Equal(2, api.InSessionStarts);
        Assert.Equal(3u, api.LastSessionId);
        Assert.Equal(1, api.Killed);
    }

    private sealed class FakeWindowsSessionApi : IWindowsSessionApi
    {
        public uint CurrentSessionId { get; set; }
        public uint? ConsoleSessionId { get; set; }
        public bool RunningAsSystem { get; set; } = true;
        public int InSessionStarts { get; private set; }
        public uint LastSessionId { get; private set; }
        public string? LastArguments { get; private set; }
        public int Killed { get; private set; }

        public uint CurrentProcessSessionId() => CurrentSessionId;

        public bool IsRunningAsSystem() => RunningAsSystem;

        public bool TryGetActiveConsoleSessionId(out uint sessionId)
        {
            if (ConsoleSessionId is null)
            {
                sessionId = 0;
                return false;
            }

            sessionId = ConsoleSessionId.Value;
            return true;
        }

        public IStartedProcess? StartInSession(uint sessionId, string fileName, string arguments, string workingDirectory)
        {
            InSessionStarts++;
            LastSessionId = sessionId;
            LastArguments = arguments;
            return new FakeProcess(() => Killed++);
        }

        public IStartedProcess? StartSameSession(string fileName, string arguments, string workingDirectory) =>
            null;
    }

    private sealed class FakeProcess(Action onKill) : IStartedProcess
    {
        public bool HasExited { get; private set; }

        public void Kill()
        {
            HasExited = true;
            onKill();
        }

        public void Dispose()
        {
        }
    }
}
