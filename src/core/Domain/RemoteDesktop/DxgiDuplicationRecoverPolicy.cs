namespace Domain.RemoteDesktop;

public static class DxgiDuplicationRecoverPolicy
{
    public const int MaxInitialCreateFailures = 5;
    public static readonly TimeSpan CaptureTimeout = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Постоянный отказ только если DXGI ещё ни разу не работал и попытки исчерпаны.
    /// </summary>
    public static bool IsPermanentCreateFailure(bool everReady, int consecutiveFailures)
        => !everReady && consecutiveFailures >= MaxInitialCreateFailures;
}
