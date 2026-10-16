namespace RemoteApp.Services;

internal interface IWindowsSessionApi
{
    uint CurrentProcessSessionId();
    bool TryGetActiveConsoleSessionId(out uint sessionId);

    /// <summary>
    /// Запущен ли текущий процесс как SYSTEM. Определяет, можно ли брать токен winlogon.
    /// </summary>
    bool IsRunningAsSystem();
    IStartedProcess? StartInSession(uint sessionId, string fileName, string arguments, string workingDirectory);
    IStartedProcess? StartSameSession(string fileName, string arguments, string workingDirectory);
}
