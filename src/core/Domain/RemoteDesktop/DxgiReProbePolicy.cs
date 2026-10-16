namespace Domain.RemoteDesktop;

/// <summary>
/// Повторные попытки поднять DXGI Desktop Duplication после неудач на старте.
/// Отказ DXGI почти всегда временный: загрузка системы, сессия RDP, сброс драйвера,
/// защищённый стол. Без повторных проб такой разовый сбой переводил кассу на медленный
/// GDI до перезапуска процесса.
/// </summary>
public static class DxgiReProbePolicy
{
    /// <summary>
    /// Пауза между попытками. Проба стоит полного пересоздания устройства D3D,
    /// поэтому в цикле кадров (~15 раз в секунду) её нельзя делать чаще.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Пора ли снова попробовать DXGI: только после постоянного отказа и не чаще Interval.
    /// nowTicks — монотонный счётчик (Environment.TickCount64), а не календарное время.
    /// </summary>
    public static bool ShouldProbe(bool initFailed, long nowTicks, long lastProbeTicks) =>
        initFailed && nowTicks - lastProbeTicks >= (long)Interval.TotalMilliseconds;
}
