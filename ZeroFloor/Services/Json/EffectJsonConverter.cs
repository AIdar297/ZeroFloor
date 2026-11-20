using System.Text.Json;
using System.Text.Json.Serialization;
using ZeroFloor.Models;

namespace ZeroFloor.Services.Json;

/// <summary>
/// В сценарных JSON-файлах эффект — {"type": "...", "key": "...", "value": ...}, где "value"
/// — bool для setFlag и число для addStat/setStat (единое поле, как в иллюстративном примере
/// Story Bible). Модель Effect хранит это как два раздельных nullable-свойства — конвертер
/// мирит "плоский" JSON с типобезопасной C#-моделью.
/// </summary>
public sealed class EffectJsonConverter : JsonConverter<Effect>
{
    public override Effect Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (!root.TryGetProperty("type", out var typeProperty))
            throw new JsonException("У эффекта отсутствует поле 'type'.");

        var typeString = typeProperty.GetString();
        if (string.IsNullOrWhiteSpace(typeString) || !Enum.TryParse<EffectType>(typeString, true, out var effectType))
            throw new JsonException($"Неизвестный тип эффекта: '{typeString}'. Допустимы: setFlag, addStat, setStat.");

        var key = root.TryGetProperty("key", out var keyProperty) ? keyProperty.GetString() ?? string.Empty : string.Empty;

        if (!root.TryGetProperty("value", out var valueProperty))
            throw new JsonException($"У эффекта '{typeString}' (ключ '{key}') отсутствует поле 'value'.");

        return effectType switch
        {
            EffectType.SetFlag => new Effect { Type = effectType, Key = key, FlagValue = ReadFlagValue(valueProperty, typeString, key) },
            EffectType.AddStat or EffectType.SetStat => new Effect { Type = effectType, Key = key, NumericValue = ReadNumericValue(valueProperty, typeString, key) },
            _ => throw new JsonException($"Неизвестный тип эффекта: '{typeString}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, Effect value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("type", JsonNamingPolicy.CamelCase.ConvertName(value.Type.ToString()));
        writer.WriteString("key", value.Key);

        switch (value.Type)
        {
            case EffectType.SetFlag:
                WriteFlagValue(writer, value.FlagValue
                    ?? throw new JsonException($"Эффект setFlag (ключ '{value.Key}') не содержит значения флага."));
                break;

            case EffectType.AddStat or EffectType.SetStat:
                writer.WriteNumber("value", value.NumericValue
                    ?? throw new JsonException($"Эффект '{value.Type}' (ключ '{value.Key}') не содержит числового значения."));
                break;

            default:
                throw new JsonException($"Неизвестный тип эффекта: '{value.Type}'.");
        }

        writer.WriteEndObject();
    }

    private static FlagValue ReadFlagValue(JsonElement element, string typeString, string key) => element.ValueKind switch
    {
        JsonValueKind.True => FlagValue.FromBoolean(true),
        JsonValueKind.False => FlagValue.FromBoolean(false),
        JsonValueKind.Number => FlagValue.FromInteger(element.GetInt32()),
        JsonValueKind.String => FlagValue.FromString(element.GetString() ?? string.Empty),
        _ => throw new JsonException($"Эффект '{typeString}' (ключ '{key}'): недопустимое значение флага ({element.ValueKind}).")
    };

    private static int ReadNumericValue(JsonElement element, string typeString, string key) =>
        element.ValueKind == JsonValueKind.Number
            ? element.GetInt32()
            : throw new JsonException($"Эффект '{typeString}' (ключ '{key}'): значение должно быть числом.");

    private static void WriteFlagValue(Utf8JsonWriter writer, FlagValue flagValue)
    {
        switch (flagValue.Kind)
        {
            case FlagValueKind.Boolean:
                writer.WriteBoolean("value", flagValue.BooleanValue);
                break;
            case FlagValueKind.Integer:
                writer.WriteNumber("value", flagValue.IntegerValue);
                break;
            case FlagValueKind.String:
                writer.WriteString("value", flagValue.StringValue);
                break;
            default:
                throw new JsonException($"Неизвестный тип значения флага: {flagValue.Kind}.");
        }
    }
}
