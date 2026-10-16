namespace Domain.RemoteDesktop;

/// <summary>
/// Управляет безопасным приёмом событий ввода от remote-модуля.
/// </summary>
public static class RemoteInputAcceptancePolicy
{
    /// <summary>
    /// Ввод применяется только если стрим этой сессии жив и событие адресовано именно ей.
    /// События без SessionId принимаются: так выглядит протокол хаба, отбрасывать их нельзя.
    /// </summary>
    public static bool ShouldApply(bool streamRunning, Guid activeSessionId, Guid incomingSessionId)
    {
        if (!streamRunning)
            return false;

        if (incomingSessionId == Guid.Empty)
            return true;

        return activeSessionId == incomingSessionId;
    }
}
