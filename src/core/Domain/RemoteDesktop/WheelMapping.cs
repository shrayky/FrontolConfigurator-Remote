namespace Domain.RemoteDesktop;

public static class WheelMapping
{
    public const int Notch = 120;
    public const int NotchThreshold = 100;

    /// <summary>
    /// Копит deltaY браузера. Положительный delta крутит вниз, а положительный mouseData Win32 — вверх.
    /// Шаг меньше 100 остаётся в remainder, иначе уходит один или несколько щелчков по 120.
    /// </summary>
    public static (int MouseData, int Remainder) Consume(int remainder, int deltaY)
    {
        var accumulated = remainder + deltaY;
        if (Math.Abs(accumulated) < NotchThreshold)
            return (0, accumulated);

        var sign = accumulated > 0 ? 1 : -1;
        var notches = Math.Max(1, Math.Abs(accumulated) / Notch);
        var leftover = accumulated - sign * notches * Notch;
        return (-sign * notches * Notch, leftover);
    }
}
