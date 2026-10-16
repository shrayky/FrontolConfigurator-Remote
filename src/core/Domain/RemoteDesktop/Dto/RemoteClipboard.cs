namespace Domain.RemoteDesktop.Dto;

public class RemoteClipboard
{
    public Guid SessionId { get; set; }
    public string Text { get; set; } = string.Empty;
}
