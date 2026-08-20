namespace Domain.RemoteDesktop.Dto;

public class RemoteJpegFrame
{
    public Guid SessionId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] Jpeg { get; set; } = [];
}
