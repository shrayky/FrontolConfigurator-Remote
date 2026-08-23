using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace RemoteApp.Services;

internal sealed class WindowsSessionApi(ILogger<WindowsSessionApi> logger) : IWindowsSessionApi
{
    private const uint InvalidSessionId = 0xFFFFFFFF;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint CreateNoWindow = 0x08000000;
    private const int SecurityImpersonation = 2;
    private const int TokenPrimary = 1;
    private const uint TokenAssignPrimary = 0x0001;
    private const uint TokenDuplicate = 0x0002;
    private const uint TokenQuery = 0x0008;
    private const uint TokenAdjustDefault = 0x0080;
    private const uint TokenAdjustSessionId = 0x0100;

    /// <summary>
    /// SessionId текущего процесса.
    /// </summary>
    public uint CurrentProcessSessionId()
    {
        try
        {
            return (uint)Process.GetCurrentProcess().SessionId;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось получить SessionId процесса fc-remote");
            return 0;
        }
    }

    /// <summary>
    /// Возвращает id активной консольной сессии пользователя (не Session 0).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public bool TryGetActiveConsoleSessionId(out uint sessionId)
    {
        sessionId = 0;
        if (!OperatingSystem.IsWindows())
            return false;

        sessionId = NativeMethods.WTSGetActiveConsoleSessionId();
        if (sessionId == InvalidSessionId || sessionId == 0)
        {
            sessionId = 0;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Обычный Process.Start в текущей сессии.
    /// </summary>
    public IStartedProcess? StartSameSession(string fileName, string arguments, string workingDirectory)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = arguments
            });
            return process is null ? null : new SystemStartedProcess(process);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Process.Start не удался для {Path}", fileName);
            return null;
        }
    }

    /// <summary>
    /// CreateProcessAsUser в указанной сессии рабочего стола winsta0\default.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public IStartedProcess? StartInSession(uint sessionId, string fileName, string arguments, string workingDirectory)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var userToken = IntPtr.Zero;
        var primaryToken = IntPtr.Zero;
        var environment = IntPtr.Zero;
        var threadHandle = IntPtr.Zero;
        var processHandle = IntPtr.Zero;

        try
        {
            if (!NativeMethods.WTSQueryUserToken(sessionId, out userToken))
            {
                logger.LogWarning(
                    "WTSQueryUserToken сессии {SessionId} не удался, код {Error}",
                    sessionId,
                    Marshal.GetLastWin32Error());
                return null;
            }

            var tokenAccess = TokenAssignPrimary | TokenDuplicate | TokenQuery | TokenAdjustDefault | TokenAdjustSessionId;
            if (!NativeMethods.DuplicateTokenEx(
                    userToken,
                    tokenAccess,
                    IntPtr.Zero,
                    SecurityImpersonation,
                    TokenPrimary,
                    out primaryToken))
            {
                logger.LogWarning("DuplicateTokenEx не удался, код {Error}", Marshal.GetLastWin32Error());
                return null;
            }

            if (!NativeMethods.CreateEnvironmentBlock(out environment, primaryToken, false))
                environment = IntPtr.Zero;

            var commandLine = new System.Text.StringBuilder($"\"{fileName}\" {arguments}");
            var startupInfo = new NativeMethods.StartupInfo
            {
                cb = Marshal.SizeOf<NativeMethods.StartupInfo>(),
                lpDesktop = @"winsta0\default"
            };

            if (!NativeMethods.CreateProcessAsUser(
                    primaryToken,
                    fileName,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    CreateUnicodeEnvironment | CreateNoWindow,
                    environment,
                    workingDirectory,
                    ref startupInfo,
                    out var processInfo))
            {
                logger.LogWarning(
                    "CreateProcessAsUser {Path} в сессии {SessionId} не удался, код {Error}",
                    fileName,
                    sessionId,
                    Marshal.GetLastWin32Error());
                return null;
            }

            threadHandle = processInfo.hThread;
            processHandle = processInfo.hProcess;
            return new SystemStartedProcess(Process.GetProcessById(processInfo.dwProcessId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка запуска процесса в сессии {SessionId}", sessionId);
            return null;
        }
        finally
        {
            if (threadHandle != IntPtr.Zero)
                NativeMethods.CloseHandle(threadHandle);
            if (processHandle != IntPtr.Zero)
                NativeMethods.CloseHandle(processHandle);
            if (environment != IntPtr.Zero)
                NativeMethods.DestroyEnvironmentBlock(environment);
            if (primaryToken != IntPtr.Zero)
                NativeMethods.CloseHandle(primaryToken);
            if (userToken != IntPtr.Zero)
                NativeMethods.CloseHandle(userToken);
        }
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct StartupInfo
        {
            public int cb;
            public string? lpReserved;
            public string? lpDesktop;
            public string? lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ProcessInformation
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll")]
        internal static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        internal static extern bool WTSQueryUserToken(uint sessionId, out IntPtr phToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool DuplicateTokenEx(
            IntPtr hExistingToken,
            uint dwDesiredAccess,
            IntPtr lpTokenAttributes,
            int impersonationLevel,
            int tokenType,
            out IntPtr phNewToken);

        [DllImport("userenv.dll", SetLastError = true)]
        internal static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

        [DllImport("userenv.dll", SetLastError = true)]
        internal static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string lpApplicationName,
            System.Text.StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref StartupInfo lpStartupInfo,
            out ProcessInformation lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr hObject);
    }
}
