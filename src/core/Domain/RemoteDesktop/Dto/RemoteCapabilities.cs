namespace Domain.RemoteDesktop.Dto;

public class RemoteCapabilities
{
    public Guid SessionId { get; set; }
    public bool StreamSettings { get; set; }
    public bool Clipboard { get; set; }
}
