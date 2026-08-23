namespace Domain.RemoteDesktop;

public static class SendInputMapping
{
    public const int AbsoluteMax = 65535;

    /// <summary>
    /// Переводит нормализованную координату 0..1 в абсолютную координату SendInput (0..65535).
    /// </summary>
    public static int ToAbsolute(float normalized)
    {
        var clamped = Math.Clamp(normalized, 0f, 1f);
        return (int)Math.Round(clamped * AbsoluteMax, MidpointRounding.AwayFromZero);
    }
}
