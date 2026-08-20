using System.Text.Json;
using Domain.Configuration;

namespace RemoteApp;

internal static class AgentConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Читает настройки центрального сервера из config.json агента Frontol.
    /// </summary>
    public static Parameters Load()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Automation",
            "fc",
            "config.json");

        if (!File.Exists(path))
            return new Parameters();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Parameters>(json, JsonOptions) ?? new Parameters();
        }
        catch (Exception)
        {
            return new Parameters();
        }
    }
}
