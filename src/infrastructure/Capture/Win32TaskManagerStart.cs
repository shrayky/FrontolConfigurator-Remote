using System.Diagnostics;
using System.Runtime.Versioning;
using Domain.RemoteDesktop;
using Microsoft.Extensions.Logging;

namespace Capture;

public sealed class Win32TaskManagerStart(Win32KeyboardInput keyboard, ILogger<Win32TaskManagerStart> logger)
{
    /// <summary>
    /// Отпускает модификаторы и запускает диспетчер задач в сессии кассира.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public void Start()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            keyboard.ReleaseModifiers();
            Process.Start(new ProcessStartInfo
            {
                FileName = TaskManagerLaunch.FileName,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось запустить {File}", TaskManagerLaunch.FileName);
        }
    }
}
