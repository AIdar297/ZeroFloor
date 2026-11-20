using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZeroFloor.Services.Json;

/// <summary>Сериализация AppSettings — простой POCO без полиморфных/самодельных типов,
/// поэтому отдельного ручного конвертера не требуется (в отличие от GameState/сценария).</summary>
public static class SettingsJsonOptions
{
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}
