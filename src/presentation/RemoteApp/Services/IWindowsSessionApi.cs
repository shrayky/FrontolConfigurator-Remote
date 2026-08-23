namespace RemoteApp.Services;

internal interface IWindowsSessionApi
{
    uint CurrentProcessSessionId();
    bool TryGetActiveConsoleSessionId(out uint sessionId);
    IStartedProcess? StartInSession(uint sessionId, string fileName, string arguments, string workingDirectory);
    IStartedProcess? StartSameSession(string fileName, string arguments, string workingDirectory);
}
