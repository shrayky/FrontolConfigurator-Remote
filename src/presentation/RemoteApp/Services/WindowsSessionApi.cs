using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using Domain.RemoteDesktop;
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
    /// MAXIMUM_ALLOWED — как winlogon-токен запрашивает Remotely/ControlR:
    /// не перечислять права вручную, а взять максимум, разрешённый DACL.
    /// </summary>
    private const uint MaximumAllowed = 0x02000000;

    private const string WinlogonProcessName = "winlogon";
    private const string DefaultDesktop = @"winsta0\default";

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
    /// Запускает процесс на столе кассира. Под SYSTEM берётся токен winlogon целевой сессии
    /// (уровень целостности System) — иначе ввод не доходит до повышенных окон. В остальных
    /// случаях используется отфильтрованный пользовательский токен, как раньше.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public IStartedProcess? StartInSession(uint sessionId, string fileName, string arguments, string workingDirectory)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var path = SessionLaunchPolicy.Decide(IsRunningAsSystem());
        logger.LogInformation(
            "Запуск процесса на столе сессии {SessionId}, путь {Path}",
            sessionId,
            path);

        return path == SessionLaunchPath.WinlogonToken
            ? StartInSessionViaWinlogonToken(sessionId, fileName, arguments, workingDirectory)
            : StartInSessionViaUserToken(sessionId, fileName, arguments, workingDirectory);
    }

    /// <summary>
    /// Запущен ли текущий процесс как SYSTEM (нужно для доступа к токену winlogon).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public bool IsRunningAsSystem()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
#pragma warning disable CA1416 // ветка недостижима вне Windows — проверено выше
            using var identity = WindowsIdentity.GetCurrent();
            return identity.IsSystem;
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось определить, запущен ли процесс как SYSTEM");
            return false;
        }
    }

    /// <summary>
    /// Токен winlogon.exe целевой сессии: уровень целостности System, поэтому SendInput
    /// проходит в повышенные окна. Открытие процесса winlogon требует SeDebugPrivilege,
    /// который есть только у SYSTEM.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private IStartedProcess? StartInSessionViaWinlogonToken(
        uint sessionId,
        string fileName,
        string arguments,
        string workingDirectory)
    {
        var winlogonProcess = IntPtr.Zero;
        var sourceToken = IntPtr.Zero;
        var primaryToken = IntPtr.Zero;
        var threadHandle = IntPtr.Zero;
        var processHandle = IntPtr.Zero;

        try
        {
            var winlogonPid = FindWinlogonProcessId(sessionId);
            if (winlogonPid is null)
                return null;

            winlogonProcess = NativeMethods.OpenProcess(MaximumAllowed, false, (int)winlogonPid.Value);
            if (winlogonProcess == IntPtr.Zero)
            {
                logger.LogWarning(
                    "OpenProcess winlogon.exe (PID {Pid}) не удался, код {Error}. " +
                    "Проверьте, что служба запущена как SYSTEM и имеет SeDebugPrivilege",
                    winlogonPid.Value,
                    Marshal.GetLastWin32Error());
                return null;
            }

            if (!NativeMethods.OpenProcessToken(winlogonProcess, TokenDuplicate, out sourceToken))
            {
                logger.LogWarning(
                    "OpenProcessToken winlogon.exe не удался, код {Error}",
                    Marshal.GetLastWin32Error());
                return null;
            }

            if (!NativeMethods.DuplicateTokenEx(
                    sourceToken,
                    MaximumAllowed,
                    IntPtr.Zero,
                    SecurityImpersonation,
                    TokenPrimary,
                    out primaryToken))
            {
                logger.LogWarning("DuplicateTokenEx winlogon-токена не удался, код {Error}", Marshal.GetLastWin32Error());
                return null;
            }

            // Блок окружения не создаём: как в Remotely/ControlR, процесс наследует окружение службы.
            var commandLine = new System.Text.StringBuilder($"\"{fileName}\" {arguments}");
            var startupInfo = new NativeMethods.StartupInfo
            {
                cb = Marshal.SizeOf<NativeMethods.StartupInfo>(),
                lpDesktop = DefaultDesktop
            };

            if (!NativeMethods.CreateProcessAsUser(
                    primaryToken,
                    fileName,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    CreateUnicodeEnvironment | CreateNoWindow,
                    IntPtr.Zero,
                    workingDirectory,
                    ref startupInfo,
                    out var processInfo))
            {
                logger.LogWarning(
                    "CreateProcessAsUser {Path} в сессии {SessionId} через winlogon-токен не удался, код {Error}",
                    fileName,
                    sessionId,
                    Marshal.GetLastWin32Error());
                return null;
            }

            threadHandle = processInfo.hThread;
            processHandle = processInfo.hProcess;
            logger.LogInformation(
                "Запущен {Path} в сессии {SessionId} с токеном winlogon, PID {Pid}",
                fileName,
                sessionId,
                processInfo.dwProcessId);
            return new SystemStartedProcess(Process.GetProcessById(processInfo.dwProcessId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка запуска процесса в сессии {SessionId} через winlogon-токен", sessionId);
            return null;
        }
        finally
        {
            if (threadHandle != IntPtr.Zero)
                NativeMethods.CloseHandle(threadHandle);
            if (processHandle != IntPtr.Zero)
                NativeMethods.CloseHandle(processHandle);
            if (primaryToken != IntPtr.Zero)
                NativeMethods.CloseHandle(primaryToken);
            if (sourceToken != IntPtr.Zero)
                NativeMethods.CloseHandle(sourceToken);
            if (winlogonProcess != IntPtr.Zero)
                NativeMethods.CloseHandle(winlogonProcess);
        }
    }

    /// <summary>
    /// PID winlogon.exe именно в целевой сессии: у каждой сессии свой winlogon.
    /// </summary>
    private uint? FindWinlogonProcessId(uint sessionId)
    {
        try
        {
            foreach (var process in Process.GetProcessesByName(WinlogonProcessName))
            {
                using (process)
                {
                    if ((uint)process.SessionId == sessionId)
                        return (uint)process.Id;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось перечислить процессы winlogon.exe");
            return null;
        }

        logger.LogWarning("winlogon.exe в сессии {SessionId} не найден", sessionId);
        return null;
    }

    /// <summary>
    /// Прежний путь: отфильтрованный пользовательский токен через WTSQueryUserToken.
    /// Уровень целостности Medium — ввод в повышенные окна не проходит.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private IStartedProcess? StartInSessionViaUserToken(
        uint sessionId,
        string fileName,
        string arguments,
        string workingDirectory)
    {
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
                lpDesktop = DefaultDesktop
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

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

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
