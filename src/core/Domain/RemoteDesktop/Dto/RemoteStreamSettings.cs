namespace Domain.RemoteDesktop.Dto;

public class RemoteStreamSettings
{
    public Guid SessionId { get; set; }
    public int Quality { get; set; }
    public int MaxSide { get; set; }
    public int IntervalMs { get; set; }
}
