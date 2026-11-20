using ZeroFloor.Engine;
using ZeroFloor.Models;
using static ZeroFloor.Tests.Support.ScenarioTestFactory;

namespace ZeroFloor.Tests;

public class ScenarioValidatorTests
{
    private readonly ScenarioValidator _sut = new();

    [Fact]
    public void Validate_WellFormedScenario_DoesNotThrow()
    {
        var scene1 = Scene("S1",
            Node("N1", "Первая", nextNodeId: "N2"),
            Node("N2", "Вторая", nextSceneId: "S2"));
        var scene2 = Scene("S2", Node("N1", "Третья", nextNodeId: "N1")); // терминальный самозамкнутый узел

        var exception = Record.Exception(() => _sut.Validate(new[] { scene1, scene2 }));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_DuplicateSceneId_Throws()
    {
        var scene1 = Scene("DUP", Node("N1", "x"));
        var scene2 = Scene("DUP", Node("N1", "y"));

        Assert.Throws<ScenarioValidationException>(() => _sut.Validate(new[] { scene1, scene2 }));
    }

    [Fact]
    public void Validate_DuplicateNodeIdWithinScene_Throws()
    {
        var scene = Scene("S1", Node("N1", "x"), Node("N1", "y"));

        Assert.Throws<ScenarioValidationException>(() => _sut.Validate(new[] { scene }));
    }

    [Fact]
    public void Validate_DanglingNextNodeId_Throws()
    {
        var scene = Scene("S1", Node("N1", "x", nextNodeId: "N999"));

        Assert.Throws<ScenarioValidationException>(() => _sut.Validate(new[] { scene }));
    }

    [Fact]
    public void Validate_DanglingNextSceneId_Throws()
    {
        var scene = Scene("S1", Node("N1", "x", nextSceneId: "S999"));

        Assert.Throws<ScenarioValidationException>(() => _sut.Validate(new[] { scene }));
    }

    [Fact]
    public void Validate_NodeWithBothNextNodeAndNextScene_Throws()
    {
        var node = new DialogueNode { NodeId = "N1", Speaker = "narrator", Text = "x", NextNodeId = "N2", NextSceneId = "S2" };
        var scene = Scene("S1", node, Node("N2", "y"));

        Assert.Throws<ScenarioValidationException>(() => _sut.Validate(new[] { scene }));
    }

    [Fact]
    public void Validate_NodeWithNeitherNextNorChoices_Throws()
    {
        var scene = Scene("S1", Node("N1", "тупик — некуда идти дальше"));

        Assert.Throws<ScenarioValidationException>(() => _sut.Validate(new[] { scene }));
    }

    [Fact]
    public void Validate_MissingDialogueText_Throws()
    {
        var node = new DialogueNode { NodeId = "N1", Speaker = "narrator", Text = "", NextNodeId = null };
        var scene = Scene("S1", node);

        Assert.Throws<ScenarioValidationException>(() => _sut.Validate(new[] { scene }));
    }

    [Fact]
    public void Validate_MissingSpeaker_Throws()
    {
        var node = new DialogueNode { NodeId = "N1", Speaker = "", Text = "текст есть" };
        var scene = Scene("S1", node);

        Assert.Throws<ScenarioValidationException>(() => _sut.Validate(new[] { scene }));
    }

    [Fact]
    public void Validate_DuplicateChoiceIdWithinNode_Throws()
    {
        var scene = Scene("S1", Node("N1", "x", choices: new[]
        {
            Choice("C1", "a", nextNodeId: "N1"),
            Choice("C1", "b", nextNodeId: "N1")
        }));

        Assert.Throws<ScenarioValidationException>(() => _sut.Validate(new[] { scene }));
    }

    [Fact]
    public void Validate_ChoiceWithNoDestination_Throws()
    {
        var scene = Scene("S1", Node("N1", "x", choices: new[] { Choice("C1", "a") }));

        Assert.Throws<ScenarioValidationException>(() => _sut.Validate(new[] { scene }));
    }

    [Fact]
    public void Validate_ChoiceWithDanglingNextNodeId_Throws()
    {
        var scene = Scene("S1", Node("N1", "x", choices: new[] { Choice("C1", "a", nextNodeId: "N999") }));

        Assert.Throws<ScenarioValidationException>(() => _sut.Validate(new[] { scene }));
    }

    [Fact]
    public void Validate_EmptyAndCondition_Throws()
    {
        var scene = Scene("S1", Node("N1", "x", choices: new[]
        {
            Choice("C1", "a", condition: new AndCondition { Children = Array.Empty<ConditionNode>() }, nextNodeId: "N1")
        }));

        Assert.Throws<ScenarioValidationException>(() => _sut.Validate(new[] { scene }));
    }

    [Fact]
    public void Validate_EffectWithUnknownStatKey_Throws()
    {
        var badEffect = new Effect { Type = EffectType.AddStat, Key = "NOT_A_STAT", NumericValue = 5 };
        var scene = Scene("S1", Node("N1", "x", choices: new[]
        {
            Choice("C1", "a", effects: new[] { badEffect }, nextNodeId: "N1")
        }));

        Assert.Throws<ScenarioValidationException>(() => _sut.Validate(new[] { scene }));
    }

    [Fact]
    public void Validate_SceneWithNoNodes_Throws()
    {
        var scene = new SceneDefinition { SceneId = "EMPTY", Nodes = Array.Empty<DialogueNode>() };

        Assert.Throws<ScenarioValidationException>(() => _sut.Validate(new[] { scene }));
    }
}
