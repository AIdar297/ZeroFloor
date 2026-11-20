using ZeroFloor.Engine;
using ZeroFloor.Models;
using static ZeroFloor.Tests.Support.ScenarioTestFactory;

namespace ZeroFloor.Tests;

public class EffectApplierTests
{
    private readonly EffectApplier _sut = new();

    [Fact]
    public void Apply_AddStat_IncreasesStat()
    {
        var state = new GameState();

        _sut.Apply(AddStat(StatId.K, 15), state);

        Assert.Equal(65, state.GetStat(StatId.K));
    }

    [Fact]
    public void Apply_AddStat_NegativeDelta_DecreasesStat()
    {
        var state = new GameState();

        _sut.Apply(AddStat(StatId.L, -20), state);

        Assert.Equal(30, state.GetStat(StatId.L));
    }

    [Fact]
    public void Apply_AddStat_ClampsAtUpperBound()
    {
        var state = new GameState();

        _sut.Apply(AddStat(StatId.H, 1000), state);

        Assert.Equal(100, state.GetStat(StatId.H));
    }

    [Fact]
    public void Apply_AddStat_ClampsAtLowerBound()
    {
        var state = new GameState();

        _sut.Apply(AddStat(StatId.R, -1000), state);

        Assert.Equal(0, state.GetStat(StatId.R));
    }

    [Fact]
    public void Apply_SetStat_SetsExactValue()
    {
        var state = new GameState();

        _sut.Apply(SetStat(StatId.K, 77), state);

        Assert.Equal(77, state.GetStat(StatId.K));
    }

    [Fact]
    public void Apply_SetFlag_StoresFlagValue()
    {
        var state = new GameState();

        _sut.Apply(SetFlag("father_file_found", true), state);

        Assert.Equal(FlagValue.FromBoolean(true), state.GetFlag("father_file_found"));
    }

    [Fact]
    public void Apply_MultipleEffects_AllApplied()
    {
        var state = new GameState();

        _sut.Apply(new[] { AddStat(StatId.K, 15), AddStat(StatId.L, -5), SetFlag("korni_open", true) }, state);

        Assert.Equal(65, state.GetStat(StatId.K));
        Assert.Equal(45, state.GetStat(StatId.L));
        Assert.Equal(FlagValue.FromBoolean(true), state.GetFlag("korni_open"));
    }

    [Fact]
    public void Apply_AddStat_UnknownStatKey_Throws()
    {
        var state = new GameState();
        var effect = new Effect { Type = EffectType.AddStat, Key = "NOT_A_STAT", NumericValue = 10 };

        Assert.Throws<ScenarioValidationException>(() => _sut.Apply(effect, state));
    }

    [Fact]
    public void Apply_AddStat_MissingNumericValue_Throws()
    {
        var state = new GameState();
        var effect = new Effect { Type = EffectType.AddStat, Key = "K", NumericValue = null };

        Assert.Throws<ScenarioValidationException>(() => _sut.Apply(effect, state));
    }

    [Fact]
    public void Apply_SetFlag_MissingFlagValue_Throws()
    {
        var state = new GameState();
        var effect = new Effect { Type = EffectType.SetFlag, Key = "father_file_found", FlagValue = null };

        Assert.Throws<ScenarioValidationException>(() => _sut.Apply(effect, state));
    }
}
