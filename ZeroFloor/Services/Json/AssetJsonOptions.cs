using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZeroFloor.Services.Json;

/// <summary>Сериализация реестров ассетов (Data/Characters/characters.json,
/// Data/Backgrounds/backgrounds.json) — плоские POCO (CharacterDefinition,
/// BackgroundDefinition), без полиморфных/самодельных типов, поэтому отдельного ручного
/// конвертера не требуется (как и для AppSettings); JsonStringEnumConverter нужен только для
/// перечислений вроде CharacterDefinition.DefaultPosition (StagePosition), чтобы в JSON можно
/// было писать "CenterLeft", а не числовой код.</summary>
public static class AssetJsonOptions
{
    public static JsonSerializerOptions Create() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
