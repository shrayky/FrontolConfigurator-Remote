using System.Diagnostics;
using System.Runtime.Versioning;
using Domain.RemoteDesktop;
using Domain.RemoteDesktop.Dto;
using Microsoft.Extensions.Logging;

namespace Capture;

public sealed class Win32FrontolAdminStart(Win32KeyboardInput keyboard, ILogger<Win32FrontolAdminStart> logger)
{
    /// <summary>
    /// Отпускает модификаторы и запускает Frontol Администратор в сессии кассира.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public RemoteActionResult Start(Guid sessionId)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return Fail(sessionId, FrontolAdminLaunch.NotFoundError);

            keyboard.ReleaseModifiers();
            var path = FrontolAdminLaunch.ResolveOnThisMachine();
            if (path == null)
                return Fail(sessionId, FrontolAdminLaunch.NotFoundError);

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });

            if (process == null)
                return Fail(sessionId, FrontolAdminLaunch.NotFoundError);

            return new RemoteActionResult
            {
                SessionId = sessionId,
                Action = RemoteActionResult.FrontolAdmin,
                Success = true
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось запустить Frontol Администратор");
            return Fail(sessionId, ex.Message);
        }
    }

    private static RemoteActionResult Fail(Guid sessionId, string error) =>
        new()
        {
            SessionId = sessionId,
            Action = RemoteActionResult.FrontolAdmin,
            Success = false,
            Error = error
        };
}
