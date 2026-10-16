namespace Domain.RemoteDesktop;

/// <summary>
/// Каким токеном запускать процесс на столе кассира.
/// </summary>
public enum SessionLaunchPath
{
    /// <summary>
    /// Токен winlogon.exe целевой сессии. Уровень целостности System, поэтому SendInput
    /// проходит в повышенные окна (taskmgr, MMC) — UIPI блокирует ввод только снизу вверх.
    /// </summary>
    WinlogonToken,

    /// <summary>
    /// Отфильтрованный токен пользователя через WTSQueryUserToken. Уровень Medium:
    /// ввод в повышенные окна Windows молча отбрасывает.
    /// </summary>
    UserToken
}

public static class SessionLaunchPolicy
{
    /// <summary>
    /// Токен winlogon доступен только службе, запущенной как SYSTEM: открытие процесса
    /// winlogon требует SeDebugPrivilege. Во всех остальных случаях работаем как раньше.
    /// </summary>
    public static SessionLaunchPath Decide(bool runningAsSystem) =>
        runningAsSystem ? SessionLaunchPath.WinlogonToken : SessionLaunchPath.UserToken;
}
