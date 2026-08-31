namespace Domain.RemoteDesktop.Dto;

public class RemoteKey
{
    public Guid SessionId { get; set; }

    /// <summary>
    /// KeyboardEvent.code из браузера.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Windows virtual-key, заполняет сервер Командера.
    /// </summary>
    public int Vk { get; set; }

    public bool Down { get; set; }

    public bool Repeat { get; set; }
}
