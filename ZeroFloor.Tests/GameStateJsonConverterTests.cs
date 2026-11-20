using System.Text.Json;
using ZeroFloor.Models;
using ZeroFloor.Services.Json;

namespace ZeroFloor.Tests;

/// <summary>GameState инкапсулирует своё состояние за методами-мутаторами, а не публичными
/// сеттерами коллекций — GameStateJsonConverter обязан восстановить ВСЁ (статы, флаги трёх
/// видов, позицию, appliedChoiceIds, viewedNodeIds, историю выборов, журнал реплик) ровно
/// тем же публичным API, которым пользуется Engine. Это прямая проверка требования
/// "сохранение должно включать текущую главу/сцену/узел/переменные/флаги/состояние
/// концовки/историю выборов/состояние сюжетных событий".</summary>
public sealed class GameStateJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = SaveJsonOptions.Create();

    private static GameState BuildFullyPopulatedState()
    {
        var state = new GameState();

        state.SetStat(StatId.K, 42);
        state.SetStat(StatId.L, 10);
        state.SetStat(StatId.R, 77);
        state.SetStat(StatId.H, 5);

        state.SetFlag("father_file_found", FlagValue.FromBoolean(true));
        state.SetFlag("underground_game_outcome", FlagValue.FromString("betrayed"));
        state.SetFlag("visits_count", FlagValue.FromInteger(3));

        state.CurrentChapterId = "CH03_SOMETHING";
        state.CurrentSceneId = "CH03_SC02";
        state.CurrentNodeId = "N010";
        state.CurrentEndingId = "END_02_SUCCESSOR";
        state.CurrentBranchId = "C";

        state.MarkChoiceApplied("C001");
        state.MarkChoiceApplied("C002");

        state.MarkNodeViewed("N001");
        state.MarkNodeViewed("N002");

        state.RecordChoice(new ChoiceHistoryEntry("CH01_SC01", "N005", "C001", "Рискованный вариант", new DateTime(2026, 1, 1, 10, 0, 0)));

        state.AppendToDialogueLog(new DialogueLogRecord(
            DialogueLogEntryKind.Line, "CH01_SC01", "N001", "narrator", "Город молчит.", new DateTime(2026, 1, 1, 9, 0, 0)));
        state.AppendToDialogueLog(new DialogueLogRecord(
            DialogueLogEntryKind.ChoiceMade, "CH01_SC01", "N005", "Вы", "Рискованный вариант", new DateTime(2026, 1, 1, 9, 5, 0)));

        return state;
    }

    [Fact]
    public void RoundTrip_PreservesEverything()
    {
        var original = BuildFullyPopulatedState();

        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<GameState>(json, Options)!;

        Assert.Equal(42, restored.GetStat(StatId.K));
        Assert.Equal(10, restored.GetStat(StatId.L));
        Assert.Equal(77, restored.GetStat(StatId.R));
        Assert.Equal(5, restored.GetStat(StatId.H));

        Assert.Equal(FlagValue.FromBoolean(true), restored.GetFlag("father_file_found"));
        Assert.Equal(FlagValue.FromString("betrayed"), restored.GetFlag("underground_game_outcome"));
        Assert.Equal(FlagValue.FromInteger(3), restored.GetFlag("visits_count"));

        Assert.Equal("CH03_SOMETHING", restored.CurrentChapterId);
        Assert.Equal("CH03_SC02", restored.CurrentSceneId);
        Assert.Equal("N010", restored.CurrentNodeId);
        Assert.Equal("END_02_SUCCESSOR", restored.CurrentEndingId);
        Assert.Equal("C", restored.CurrentBranchId);

        Assert.True(restored.HasAppliedChoice("C001"));
        Assert.True(restored.HasAppliedChoice("C002"));
        Assert.False(restored.HasAppliedChoice("C003"));

        Assert.Equal(new[] { "N001", "N002" }, restored.ViewedNodeIds);

        var historyEntry = Assert.Single(restored.ChoiceHistory);
        Assert.Equal("C001", historyEntry.ChoiceId);
        Assert.Equal("Рискованный вариант", historyEntry.ChoiceText);

        Assert.Equal(2, restored.DialogueLog.Count);
        Assert.Equal(DialogueLogEntryKind.Line, restored.DialogueLog[0].Kind);
        Assert.Equal(DialogueLogEntryKind.ChoiceMade, restored.DialogueLog[1].Kind);
    }

    [Fact]
    public void EmptyState_RoundTrips_WithoutErrors()
    {
        var original = new GameState();

        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<GameState>(json, Options)!;

        Assert.Equal(50, restored.GetStat(StatId.K));
        Assert.Equal(60, restored.GetStat(StatId.H));
        Assert.Empty(restored.Flags);
        Assert.Empty(restored.AppliedChoiceIds);
        Assert.Null(restored.CurrentEndingId);
    }
}
