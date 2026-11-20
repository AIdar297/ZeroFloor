using ZeroFloor.Engine;
using ZeroFloor.Models;

namespace ZeroFloor.Tests;

/// <summary>
/// Повторяет тест-план Gate10 из PROJECT_ANALYSIS.md, раздел 13.5 (T07–T11), плюс
/// защиту от повторной фиксации уже определённой концовки.
/// </summary>
public class EndingManagerTests
{
    private readonly EndingManager _sut = new(new ConditionEvaluator());

    [Fact]
    public void BranchA_AlwaysAvailable()
    {
        var state = new GameState();
        Assert.Contains("A", _sut.GetAvailableBranchIds(state));
    }

    [Fact] // T07
    public void BranchB_Unavailable_WhenK39()
    {
        var state = new GameState();
        state.SetStat(StatId.K, 39);

        var available = _sut.GetAvailableBranchIds(state);

        Assert.DoesNotContain("B", available);
        Assert.Contains("A", available);
    }

    [Fact] // T08
    public void BranchB_Available_WhenK40()
    {
        var state = new GameState();
        state.SetStat(StatId.K, 40);

        var available = _sut.GetAvailableBranchIds(state);

        Assert.Contains("A", available);
        Assert.Contains("B", available);
    }

    [Fact] // T09
    public void BranchD_Unavailable_WhenH69_EvenWithFatherFileFound()
    {
        var state = new GameState();
        state.SetStat(StatId.H, 69);
        state.SetFlag("father_file_found", FlagValue.FromBoolean(true));
        state.SetFlag("sonya_exposed", FlagValue.FromBoolean(false));

        var available = _sut.GetAvailableBranchIds(state);

        Assert.Contains("C", available);
        Assert.DoesNotContain("D", available);
    }

    [Fact] // T10
    public void BranchesCAndD_Available_WhenH70AndFatherFileFoundAndNotExposed()
    {
        var state = new GameState();
        state.SetStat(StatId.H, 70);
        state.SetFlag("father_file_found", FlagValue.FromBoolean(true));
        state.SetFlag("sonya_exposed", FlagValue.FromBoolean(false));

        var available = _sut.GetAvailableBranchIds(state);

        Assert.Contains("C", available);
        Assert.Contains("D", available);
    }

    [Fact] // T11
    public void BranchD_Unavailable_WhenSonyaExposed_EvenWithHighH()
    {
        var state = new GameState();
        state.SetStat(StatId.H, 90);
        state.SetFlag("father_file_found", FlagValue.FromBoolean(true));
        state.SetFlag("sonya_exposed", FlagValue.FromBoolean(true));

        var available = _sut.GetAvailableBranchIds(state);

        Assert.DoesNotContain("D", available);
    }

    [Fact]
    public void BranchC_Unavailable_WithoutFatherFileFound()
    {
        var state = new GameState();
        Assert.DoesNotContain("C", _sut.GetAvailableBranchIds(state));
    }

    [Theory]
    [InlineData("A", "END_03_ESCAPE")]
    [InlineData("B", "END_01_COLLAPSE")]
    [InlineData("C", "END_02_SUCCESSOR")]
    [InlineData("D", "END_04_BALANCE")]
    public void ResolveEnding_ReturnsCorrectEndingId(string branchId, string expectedEndingId)
    {
        var state = new GameState();
        state.SetStat(StatId.K, 100);
        state.SetStat(StatId.H, 100);
        state.SetFlag("father_file_found", FlagValue.FromBoolean(true));
        state.SetFlag("sonya_exposed", FlagValue.FromBoolean(false));

        var endingId = _sut.ResolveEnding(branchId, state);

        Assert.Equal(expectedEndingId, endingId);
        Assert.Equal(expectedEndingId, state.CurrentEndingId);
        Assert.Equal(branchId, state.CurrentBranchId);
    }

    [Fact] // T12-подобный: защита от пересчёта после фиксации
    public void ResolveEnding_CalledTwice_SecondCallThrows()
    {
        var state = new GameState();

        _sut.ResolveEnding("A", state);

        Assert.Throws<ScenarioValidationException>(() => _sut.ResolveEnding("A", state));
    }

    [Fact]
    public void ResolveEnding_UnavailableBranch_Throws()
    {
        var state = new GameState();
        state.SetStat(StatId.K, 10); // < 40 — ветка B недоступна

        Assert.Throws<ScenarioValidationException>(() => _sut.ResolveEnding("B", state));
    }

    [Fact]
    public void ResolveEnding_UnknownBranchId_Throws()
    {
        var state = new GameState();

        Assert.Throws<ScenarioValidationException>(() => _sut.ResolveEnding("Z", state));
    }

    [Fact]
    public void GetAvailableBranchIds_WorstCaseK_StillUnlocksB()
    {
        // PROJECT_ANALYSIS.md, раздел 9.4: худшая комбинация (Выбор 4 "дистанция" +5,
        // Выбор 8 "публично" -15) даёт K = 50 + 5 - 15 = 40 — порог B пройден впритык.
        var state = new GameState();
        state.AddToStat(StatId.K, 5);
        state.AddToStat(StatId.K, -15);

        Assert.Equal(40, state.GetStat(StatId.K));
        Assert.Contains("B", _sut.GetAvailableBranchIds(state));
    }
}
