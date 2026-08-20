using System.Text.Json.Serialization;

namespace Domain.Configuration.Options;

public class CentralServerConnection
{
    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
}
