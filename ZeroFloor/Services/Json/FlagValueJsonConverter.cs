using System.Text.Json;
using System.Text.Json.Serialization;
using ZeroFloor.Models;

namespace ZeroFloor.Services.Json;

/// <summary>
/// В сценарных JSON-файлах значение флага — обычное bool/число/строка (например
/// "value": true), а не вложенный объект с полями Kind/BooleanValue/... Конвертер читает
/// и пишет именно такой "плоский" вид.
/// </summary>
public sealed class FlagValueJsonConverter : JsonConverter<FlagValue>
{
    public override FlagValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => FlagValue.FromBoolean(true),
            JsonTokenType.False => FlagValue.FromBoolean(false),
            JsonTokenType.Number => FlagValue.FromInteger(reader.GetInt32()),
            JsonTokenType.String => FlagValue.FromString(reader.GetString() ?? string.Empty),
            _ => throw new JsonException(
                $"Значение флага должно быть true/false, числом или строкой — получено: {reader.TokenType}.")
        };

    public override void Write(Utf8JsonWriter writer, FlagValue value, JsonSerializerOptions options)
    {
        switch (value.Kind)
        {
            case FlagValueKind.Boolean:
                writer.WriteBooleanValue(value.BooleanValue);
                break;
            case FlagValueKind.Integer:
                writer.WriteNumberValue(value.IntegerValue);
                break;
            case FlagValueKind.String:
                writer.WriteStringValue(value.StringValue);
                break;
            default:
                throw new JsonException($"Неизвестный тип значения флага: {value.Kind}.");
        }
    }
}
