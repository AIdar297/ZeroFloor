using ZeroFloor.Engine;
using ZeroFloor.Models;
using static ZeroFloor.Tests.Support.ScenarioTestFactory;

namespace ZeroFloor.Tests;

public class ConditionEvaluatorTests
{
    private readonly ConditionEvaluator _sut = new();

    [Fact]
    public void Evaluate_NullCondition_IsAlwaysTrue()
    {
        var state = new GameState();
        Assert.True(_sut.Evaluate(null, state));
    }

    // Шкала K выставляется в 40; каждый кейс сравнивает её с threshold через указанный оператор.
    [Theory]
    [InlineData(ComparisonOperator.GreaterThan, 39, true)]
    [InlineData(ComparisonOperator.GreaterThan, 40, false)]
    [InlineData(ComparisonOperator.GreaterThan, 41, false)]
    [InlineData(ComparisonOperator.GreaterThanOrEqual, 39, true)]
    [InlineData(ComparisonOperator.GreaterThanOrEqual, 40, true)]
    [InlineData(ComparisonOperator.GreaterThanOrEqual, 41, false)]
    [InlineData(ComparisonOperator.LessThan, 41, true)]
    [InlineData(ComparisonOperator.LessThan, 40, false)]
    [InlineData(ComparisonOperator.LessThan, 39, false)]
    [InlineData(ComparisonOperator.LessThanOrEqual, 40, true)]
    [InlineData(ComparisonOperator.LessThanOrEqual, 39, false)]
    [InlineData(ComparisonOperator.Equal, 40, true)]
    [InlineData(ComparisonOperator.Equal, 41, false)]
    public void Evaluate_StatComparison_AllOperators(ComparisonOperator op, int threshold, bool expected)
    {
        var state = new GameState();
        state.SetStat(StatId.K, 40);

        var condition = new StatComparisonCondition { Stat = StatId.K, Operator = op, Value = threshold };

        Assert.Equal(expected, _sut.Evaluate(condition, state));
    }

    [Fact]
    public void Evaluate_FlagEquals_SetFlagMatches()
    {
        var state = new GameState();
        state.SetFlag("father_file_found", FlagValue.FromBoolean(true));

        Assert.True(_sut.Evaluate(FlagIs("father_file_found", true), state));
        Assert.False(_sut.Evaluate(FlagIs("father_file_found", false), state));
    }

    [Fact]
    public void Evaluate_FlagEquals_UnsetFlag_DefaultsToFalse()
    {
        var state = new GameState();

        // sonya_exposed по умолчанию ложь, пока явно не установлен (Story Bible, раздел 16).
        Assert.True(_sut.Evaluate(FlagIs("sonya_exposed", false), state));
        Assert.False(_sut.Evaluate(FlagIs("sonya_exposed", true), state));
    }

    [Fact]
    public void Evaluate_And_AllChildrenMustBeTrue()
    {
        var state = new GameState();
        state.SetStat(StatId.H, 70);
        state.SetFlag("father_file_found", FlagValue.FromBoolean(true));
        state.SetFlag("sonya_exposed", FlagValue.FromBoolean(false));

        var condition = new AndCondition
        {
            Children = new ConditionNode[]
            {
                StatAtLeast(StatId.H, 70),
                FlagIs("father_file_found", true),
                FlagIs("sonya_exposed", false)
            }
        };

        Assert.True(_sut.Evaluate(condition, state));

        state.SetFlag("sonya_exposed", FlagValue.FromBoolean(true));
        Assert.False(_sut.Evaluate(condition, state));
    }

    [Fact]
    public void Evaluate_Or_AnyChildTrueIsEnough()
    {
        var state = new GameState();
        state.SetStat(StatId.K, 10);

        var condition = new OrCondition
        {
            Children = new ConditionNode[]
            {
                StatAtLeast(StatId.K, 40),
                FlagIs("father_file_found", true)
            }
        };

        Assert.False(_sut.Evaluate(condition, state));

        state.SetFlag("father_file_found", FlagValue.FromBoolean(true));
        Assert.True(_sut.Evaluate(condition, state));
    }

    [Fact]
    public void Evaluate_Not_InvertsChild()
    {
        var state = new GameState();
        state.SetFlag("sonya_exposed", FlagValue.FromBoolean(true));

        var condition = new NotCondition { Child = FlagIs("sonya_exposed", false) };

        Assert.True(_sut.Evaluate(condition, state));
    }

    [Fact]
    public void Evaluate_And_WithNoChildren_Throws()
    {
        var state = new GameState();
        var condition = new AndCondition { Children = Array.Empty<ConditionNode>() };

        Assert.Throws<ScenarioValidationException>(() => _sut.Evaluate(condition, state));
    }

    [Fact]
    public void Evaluate_Or_WithNoChildren_Throws()
    {
        var state = new GameState();
        var condition = new OrCondition { Children = Array.Empty<ConditionNode>() };

        Assert.Throws<ScenarioValidationException>(() => _sut.Evaluate(condition, state));
    }

    [Fact]
    public void Evaluate_FlagEquals_WithEmptyKey_Throws()
    {
        var state = new GameState();
        var condition = new FlagEqualsCondition { FlagKey = string.Empty, ExpectedValue = FlagValue.FromBoolean(true) };

        Assert.Throws<ScenarioValidationException>(() => _sut.Evaluate(condition, state));
    }
}
