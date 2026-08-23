namespace RemoteApp.Services;

internal interface IStartedProcess : IDisposable
{
    bool HasExited { get; }
    void Kill();
}
