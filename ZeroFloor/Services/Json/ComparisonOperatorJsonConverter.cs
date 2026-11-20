using System.Text.Json;
using System.Text.Json.Serialization;
using ZeroFloor.Models;

namespace ZeroFloor.Services.Json;

/// <summary>Символьная запись оператора сравнения в сценарных данных: >, >=, &lt;, &lt;=, =.</summary>
public sealed class ComparisonOperatorJsonConverter : JsonConverter<ComparisonOperator>
{
    public override ComparisonOperator Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();

        return raw switch
        {
            ">" => ComparisonOperator.GreaterThan,
            ">=" => ComparisonOperator.GreaterThanOrEqual,
            "<" => ComparisonOperator.LessThan,
            "<=" => ComparisonOperator.LessThanOrEqual,
            "=" or "==" => ComparisonOperator.Equal,
            _ => throw new JsonException($"Неизвестный оператор сравнения: '{raw}'. Допустимы: >, >=, <, <=, =.")
        };
    }

    public override void Write(Utf8JsonWriter writer, ComparisonOperator value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            ComparisonOperator.GreaterThan => ">",
            ComparisonOperator.GreaterThanOrEqual => ">=",
            ComparisonOperator.LessThan => "<",
            ComparisonOperator.LessThanOrEqual => "<=",
            ComparisonOperator.Equal => "=",
            _ => throw new JsonException($"Неизвестный оператор сравнения: '{value}'.")
        });
}
