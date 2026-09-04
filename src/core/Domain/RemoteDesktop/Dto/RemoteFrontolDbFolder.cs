namespace Domain.RemoteDesktop.Dto;

/// <summary>
/// Запрос открытия каталога базы Frontol на кассе.
/// </summary>
public class RemoteFrontolDbFolder
{
    public Guid SessionId { get; set; }
}
