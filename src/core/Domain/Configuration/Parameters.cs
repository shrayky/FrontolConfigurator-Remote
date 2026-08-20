using System.Text.Json.Serialization;
using Domain.Configuration.Options;

namespace Domain.Configuration;

public class Parameters
{
    [JsonPropertyName("centralServerSettings")]
    public CentralServerConnection CentralServerSettings { get; set; } = new();
}
