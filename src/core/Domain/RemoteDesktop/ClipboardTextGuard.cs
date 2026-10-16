namespace Domain.RemoteDesktop;

public static class ClipboardTextGuard
{
    public const int MaxChars = 32_000;

    public static bool IsAllowed(string? text) =>
        !string.IsNullOrEmpty(text) && text.Length <= MaxChars;
}
