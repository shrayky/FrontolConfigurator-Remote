using System.Diagnostics;
using System.Runtime.Versioning;
using Domain.RemoteDesktop;
using Domain.RemoteDesktop.Dto;
using Microsoft.Extensions.Logging;

namespace Capture;

public sealed class Win32FrontolDbFolderOpen(Win32KeyboardInput keyboard, ILogger<Win32FrontolDbFolderOpen> logger)
{
    /// <summary>
    /// Отпускает модификаторы и открывает каталог базы Frontol в проводнике.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public RemoteActionResult Open(Guid sessionId)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return Fail(sessionId, FrontolDbFolderLaunch.NotFoundError);

            keyboard.ReleaseModifiers();
            var resolved = FrontolDbFolderLaunch.ResolveOnThisMachine();
            if (!resolved.Success || resolved.Folder == null)
                return Fail(sessionId, resolved.Error, resolved.Tried);

            var explorer = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "explorer.exe");

            Process.Start(new ProcessStartInfo
            {
                FileName = explorer,
                Arguments = $"\"{resolved.Folder}\"",
                UseShellExecute = false
            });

            return new RemoteActionResult
            {
                SessionId = sessionId,
                Action = RemoteActionResult.FrontolDbFolder,
                Success = true
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось открыть каталог базы Frontol");
            return Fail(sessionId, ex.Message);
        }
    }

    private RemoteActionResult Fail(Guid sessionId, string error, string? tried = null)
    {
        logger.LogWarning(
            "Каталог базы Frontol не открыт: {Error}. ini={Ini}, path={Path}",
            error,
            FrontolDbFolderLaunch.IniPathOnThisMachine(),
            tried);

        return new RemoteActionResult
        {
            SessionId = sessionId,
            Action = RemoteActionResult.FrontolDbFolder,
            Success = false,
            Error = error
        };
    }
}
