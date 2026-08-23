namespace RemoteApp;

internal static class ProcessRole
{
    public const string DesktopChildArguments = "--service --desktop";

    /// <summary>
    /// Сторож Session 0: поднимает копию процесса на столе кассира.
    /// </summary>
    public static bool IsWatchdog(IEnumerable<string> args, int sessionId)
    {
        var list = args as ICollection<string> ?? args.ToArray();
        return sessionId == 0
               && list.Contains("--service", StringComparer.OrdinalIgnoreCase)
               && !list.Contains("--desktop", StringComparer.OrdinalIgnoreCase);
    }
}
