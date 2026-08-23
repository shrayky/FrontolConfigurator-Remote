using System.Diagnostics;

namespace RemoteApp.Services;

internal sealed class SystemStartedProcess(Process process) : IStartedProcess
{
    public bool HasExited => process.HasExited;

    /// <summary>
    /// Завершает процесс и его дерево.
    /// </summary>
    public void Kill()
    {
        if (process.HasExited)
            return;

        process.Kill(entireProcessTree: true);
        process.WaitForExit(5_000);
    }

    public void Dispose() => process.Dispose();
}
