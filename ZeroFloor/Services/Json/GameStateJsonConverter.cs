using System.Text.Json;
using System.Text.Json.Serialization;
using ZeroFloor.Models;

namespace ZeroFloor.Services.Json;

/// <summary>
/// GameState инкапсулирует своё состояние за публичными методами-мутаторами (SetStat,
/// SetFlag, MarkChoiceApplied, MarkNodeViewed, RecordChoice, ...), а не публичными сеттерами
/// коллекций — поэтому обычная reflection-сериализация System.Text.Json не смогла бы
/// восстановить такой объект. Конвертер читает/пишет JSON вручную и восстанавливает
/// состояние ТОЛЬКО через тот же публичный API, которым пользуется Engine — не вскрывает
/// внутренние поля и не вводит отдельный путь мутации состояния (CLAUDE.md, правило 3:
/// один источник правды — сами методы GameState).
/// </summary>
public sealed class GameStateJsonConverter : JsonConverter<GameState>
{
    public override GameState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        var state = new GameState();

        if (root.TryGetProperty("stats", out var statsElement))
        {
            foreach (var property in statsElement.EnumerateObject())
            {
                if (Enum.TryParse<StatId>(property.Name, true, out var statId))
                    state.SetStat(statId, property.Value.GetInt32());
            }
        }

        if (root.TryGetProperty("flags", out var flagsElement))
        {
            foreach (var property in flagsElement.EnumerateObject())
                state.SetFlag(property.Name, ReadFlagValue(property.Value));
        }

        state.CurrentChapterId = GetNullableString(root, "currentChapterId");
        state.CurrentSceneId = GetNullableString(root, "currentSceneId");
        state.CurrentNodeId = GetNullableString(root, "currentNodeId");
        state.CurrentEndingId = GetNullableString(root, "currentEndingId");
        state.CurrentBranchId = GetNullableString(root, "currentBranchId");

        if (root.TryGetProperty("appliedChoiceIds", out var appliedElement))
            foreach (var item in appliedElement.EnumerateArray())
                state.MarkChoiceApplied(item.GetString() ?? string.Empty);

        if (root.TryGetProperty("viewedNodeIds", out var viewedElement))
            foreach (var item in viewedElement.EnumerateArray())
                state.MarkNodeViewed(item.GetString() ?? string.Empty);

        if (root.TryGetProperty("choiceHistory", out var historyElement))
        {
            var entries = JsonSerializer.Deserialize<List<ChoiceHistoryEntry>>(historyElement, options)
                ?? new List<ChoiceHistoryEntry>();
            state.RestoreChoiceHistory(entries);
        }

        if (root.TryGetProperty("dialogueLog", out var logElement))
        {
            var records = JsonSerializer.Deserialize<List<DialogueLogRecord>>(logElement, options)
                ?? new List<DialogueLogRecord>();
            state.RestoreDialogueLog(records);
        }

        return state;
    }

    public override void Write(Utf8JsonWriter writer, GameState value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("stats");
        writer.WriteStartObject();
        foreach (var (statId, statValue) in value.Stats)
            writer.WriteNumber(statId.ToString(), statValue);
        writer.WriteEndObject();

        writer.WritePropertyName("flags");
        writer.WriteStartObject();
        foreach (var (key, flagValue) in value.Flags)
        {
            writer.WritePropertyName(key);
            WriteFlagValue(writer, flagValue);
        }
        writer.WriteEndObject();

        WriteNullableString(writer, "currentChapterId", value.CurrentChapterId);
        WriteNullableString(writer, "currentSceneId", value.CurrentSceneId);
        WriteNullableString(writer, "currentNodeId", value.CurrentNodeId);
        WriteNullableString(writer, "currentEndingId", value.CurrentEndingId);
        WriteNullableString(writer, "currentBranchId", value.CurrentBranchId);

        writer.WritePropertyName("appliedChoiceIds");
        writer.WriteStartArray();
        foreach (var id in value.AppliedChoiceIds)
            writer.WriteStringValue(id);
        writer.WriteEndArray();

        writer.WritePropertyName("viewedNodeIds");
        writer.WriteStartArray();
        foreach (var id in value.ViewedNodeIds)
            writer.WriteStringValue(id);
        writer.WriteEndArray();

        writer.WritePropertyName("choiceHistory");
        JsonSerializer.Serialize(writer, value.ChoiceHistory, options);

        writer.WritePropertyName("dialogueLog");
        JsonSerializer.Serialize(writer, value.DialogueLog, options);

        writer.WriteEndObject();
    }

    private static FlagValue ReadFlagValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True => FlagValue.FromBoolean(true),
        JsonValueKind.False => FlagValue.FromBoolean(false),
        JsonValueKind.Number => FlagValue.FromInteger(element.GetInt32()),
        JsonValueKind.String => FlagValue.FromString(element.GetString() ?? string.Empty),
        _ => throw new JsonException($"Недопустимое значение флага в сохранении: {element.ValueKind}.")
    };

    private static void WriteFlagValue(Utf8JsonWriter writer, FlagValue value)
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

    private static string? GetNullableString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var element) && element.ValueKind != JsonValueKind.Null
            ? element.GetString()
            : null;

    private static void WriteNullableString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is null)
            writer.WriteNull(propertyName);
        else
            writer.WriteString(propertyName, value);
    }
}
