using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZeroFloor.Services.Json;

/// <summary>Единая настройка сериализации сценарных JSON-файлов.</summary>
public static class ScenarioJsonOptions
{
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        // Порядок важен: более специфичные конвертеры должны идти раньше общего
        // JsonStringEnumConverter, иначе он перехватит их типы первым.
        options.Converters.Add(new FlagValueJsonConverter());
        options.Converters.Add(new EffectJsonConverter());
        options.Converters.Add(new ComparisonOperatorJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}
