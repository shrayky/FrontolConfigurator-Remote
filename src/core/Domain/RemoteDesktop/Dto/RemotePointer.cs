namespace Domain.RemoteDesktop.Dto;

public class RemotePointer
{
    public Guid SessionId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public string Ev { get; set; } = string.Empty;
    public int Buttons { get; set; }
}
