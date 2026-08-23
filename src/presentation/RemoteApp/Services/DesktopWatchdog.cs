using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RemoteApp.Services;

internal sealed class DesktopWatchdog(IWindowsSessionApi sessionApi, ILogger<DesktopWatchdog>? logger = null)
{
    private readonly ILogger<DesktopWatchdog> _logger = logger ?? NullLogger<DesktopWatchdog>.Instance;
    private IStartedProcess? _child;
    private uint _childSessionId;

    /// <summary>
    /// Запускает или перезапускает процесс на интерактивном столе.
    /// </summary>
    public void Tick(string executablePath, string workingDirectory)
    {
        try
        {
            EnsureDesktopChild(executablePath, workingDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка цикла сторожа рабочего стола");
        }
    }

    private void EnsureDesktopChild(string executablePath, string workingDirectory)
    {
        if (_child is { HasExited: false })
        {
            if (sessionApi.TryGetActiveConsoleSessionId(out var current) && current == _childSessionId)
                return;

            StopChild();
        }
        else if (_child is not null)
        {
            _child.Dispose();
            _child = null;
        }

        if (!sessionApi.TryGetActiveConsoleSessionId(out var sessionId))
            return;

        var process = sessionApi.StartInSession(
            sessionId,
            executablePath,
            ProcessRole.DesktopChildArguments,
            workingDirectory);

        if (process is null)
        {
            _logger.LogWarning("Не удалось запустить fc-remote на столе сессии {SessionId}", sessionId);
            return;
        }

        _child = process;
        _childSessionId = sessionId;
        _logger.LogInformation("Запущен fc-remote на столе сессии {SessionId}", sessionId);
    }

    /// <summary>
    /// Останавливает процесс на столе.
    /// </summary>
    public void StopChild()
    {
        if (_child is null)
            return;

        try
        {
            if (!_child.HasExited)
                _child.Kill();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка остановки процесса на столе");
        }
        finally
        {
            _child.Dispose();
            _child = null;
            _childSessionId = 0;
        }
    }
}
