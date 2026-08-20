namespace Domain.RemoteDesktop;

public static class CursorDrawPosition
{
    /// <summary>
    /// Возвращает точку рисования курсора на кадре: позиция минус hotspot иконки.
    /// </summary>
    public static (int X, int Y) FromScreen(int cursorX, int cursorY, int hotspotX, int hotspotY)
        => (cursorX - hotspotX, cursorY - hotspotY);
}
