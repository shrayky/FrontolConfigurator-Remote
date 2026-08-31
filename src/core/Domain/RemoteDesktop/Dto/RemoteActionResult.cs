namespace Domain.RemoteDesktop.Dto;

/// <summary>
/// Результат действия remote-модуля для окна оператора.
/// </summary>
public class RemoteActionResult
{
    public const string FrontolAdmin = "frontolAdmin";

    public Guid SessionId { get; set; }

    public string Action { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string Error { get; set; } = string.Empty;
}
