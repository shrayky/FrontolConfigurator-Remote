namespace Domain.RemoteDesktop.Dto;

/// <summary>
/// Запрос запуска диспетчера задач на кассе.
/// </summary>
public class RemoteTaskManager
{
    public Guid SessionId { get; set; }
}
