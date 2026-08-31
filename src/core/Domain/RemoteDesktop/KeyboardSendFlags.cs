namespace Domain.RemoteDesktop;

/// <summary>
/// Флаги KEYBDINPUT для SendInput по virtual-key.
/// </summary>
public static class KeyboardSendFlags
{
    public const uint Extended = 0x0001;
    public const uint KeyUp = 0x0002;

    /// <summary>
    /// Собирает dwFlags: KEYUP при отпускании, EXTENDED для стрелок и правых модификаторов.
    /// </summary>
    public static uint From(bool down, int vk)
    {
        uint flags = 0;
        if (!down)
            flags |= KeyUp;
        if (IsExtended(vk))
            flags |= Extended;
        return flags;
    }

    /// <summary>
    /// Vk 0 — неизвестный code, в SendInput не отправляем.
    /// </summary>
    public static bool ShouldSend(int vk) => vk != 0;

    /// <summary>
    /// Левый/правый Shift, Ctrl, Alt — отпускаем, если остались зажатыми.
    /// </summary>
    public static readonly int[] ModifierVirtualKeys = [0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5];

    private static bool IsExtended(int vk) =>
        vk is 0x21 or 0x22 or 0x23 or 0x24
            or 0x25 or 0x26 or 0x27 or 0x28
            or 0x2C or 0x2D or 0x2E
            or 0x5B or 0x5C or 0x5D
            or 0x6F or 0x90
            or 0xA3 or 0xA5;
}
