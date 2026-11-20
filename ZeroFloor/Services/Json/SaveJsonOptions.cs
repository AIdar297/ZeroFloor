using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZeroFloor.Services.Json;

/// <summary>Единая настройка сериализации файлов сохранения — отдельно от
/// ScenarioJsonOptions (тот настроен на сценарные JSON с полиморфными ConditionNode/Effect,
/// которых в сейвах нет; здесь свой конвертер для GameState целиком).</summary>
public static class SaveJsonOptions
{
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        options.Converters.Add(new FlagValueJsonConverter());
        options.Converters.Add(new GameStateJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}
