using Domain.RemoteDesktop;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RemoteApp.Services;

namespace RemoteApp.Workers;

internal sealed class DesktopWatchdogWorker(
    DesktopWatchdog watchdog,
    ILogger<DesktopWatchdogWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var exe = Environment.ProcessPath;
        var dir = Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(exe))
        {
            logger.LogError("Не удалось определить путь fc-remote.exe");
            return;
        }

        logger.LogInformation("Сторож рабочего стола запущен. exe={Path}", exe);

        var lastHeartbeat = DateTime.MinValue;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                watchdog.Tick(exe, dir);
                if (DateTime.UtcNow - lastHeartbeat >= RemoteHubConnectPolicy.HeartbeatInterval)
                {
                    logger.LogInformation("Heartbeat сторож");
                    lastHeartbeat = DateTime.UtcNow;
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            watchdog.StopChild();
            logger.LogInformation("Сторож рабочего стола остановлен");
        }
    }
}
