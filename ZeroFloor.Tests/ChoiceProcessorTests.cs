using ZeroFloor.Engine;
using ZeroFloor.Models;
using static ZeroFloor.Tests.Support.ScenarioTestFactory;

namespace ZeroFloor.Tests;

public class ChoiceProcessorTests
{
    private readonly ChoiceProcessor _sut = new(new ConditionEvaluator(), new EffectApplier());

    private static DialogueNode BuildNodeWithTwoChoices() => Node(
        "N1", "Реплика с выбором",
        choices: new[]
        {
            Choice("ALWAYS", "Доступен всегда", effects: new[] { AddStat(StatId.K, 10) }, nextNodeId: "N2"),
            Choice("GATED", "Доступен только при K>=60", condition: StatAtLeast(StatId.K, 60), nextNodeId: "N3")
        });

    [Fact]
    public void GetAvailableChoices_FiltersByCondition()
    {
        var node = BuildNodeWithTwoChoices();
        var state = new GameState();

        var available = _sut.GetAvailableChoices(node, state);

        Assert.Single(available);
        Assert.Equal("ALWAYS", available[0].ChoiceId);
    }

    [Fact]
    public void GetAvailableChoices_WhenConditionBecomesTrue_ChoiceAppears()
    {
        var node = BuildNodeWithTwoChoices();
        var state = new GameState();
        state.SetStat(StatId.K, 60);

        var available = _sut.GetAvailableChoices(node, state);

        Assert.Equal(2, available.Count);
    }

    [Fact]
    public void ApplyChoice_AppliesEffectsAndReturnsOutcome()
    {
        var node = BuildNodeWithTwoChoices();
        var state = new GameState();

        var outcome = _sut.ApplyChoice(node, "ALWAYS", state);

        Assert.Equal(60, state.GetStat(StatId.K));
        Assert.Equal("N2", outcome.NodeId);
        Assert.Null(outcome.SceneId);
        Assert.Null(outcome.EndingId);
    }

    [Fact]
    public void ApplyChoice_SameChoiceTwice_EffectsAppliedOnlyOnce()
    {
        var node = BuildNodeWithTwoChoices();
        var state = new GameState();

        _sut.ApplyChoice(node, "ALWAYS", state);
        _sut.ApplyChoice(node, "ALWAYS", state); // повторное применение (например, после загрузки)

        Assert.Equal(60, state.GetStat(StatId.K)); // не 70 — эффект не удвоился
    }

    [Fact]
    public void ApplyChoice_UnknownChoiceId_Throws()
    {
        var node = BuildNodeWithTwoChoices();
        var state = new GameState();

        Assert.Throws<ScenarioValidationException>(() => _sut.ApplyChoice(node, "NOPE", state));
    }

    [Fact]
    public void ApplyChoice_ConditionNotMet_Throws()
    {
        var node = BuildNodeWithTwoChoices();
        var state = new GameState();

        Assert.Throws<ScenarioValidationException>(() => _sut.ApplyChoice(node, "GATED", state));
    }

    [Fact]
    public void ApplyChoice_NoDestination_Throws()
    {
        var node = Node("N1", "Узел", choices: new[] { Choice("BAD", "Выбор без направления") });
        var state = new GameState();

        Assert.Throws<ScenarioValidationException>(() => _sut.ApplyChoice(node, "BAD", state));
    }

    [Fact]
    public void ApplyChoice_TwoDestinationsAtOnce_Throws()
    {
        var node = Node("N1", "Узел", choices: new[]
        {
            new ChoiceDefinition { ChoiceId = "BAD", Text = "x", NextNodeId = "N2", NextSceneId = "S2" }
        });
        var state = new GameState();

        Assert.Throws<ScenarioValidationException>(() => _sut.ApplyChoice(node, "BAD", state));
    }
}
