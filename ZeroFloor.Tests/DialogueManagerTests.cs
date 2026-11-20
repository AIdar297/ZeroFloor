using ZeroFloor.Engine;
using ZeroFloor.Models;
using ZeroFloor.Services;
using static ZeroFloor.Tests.Support.ScenarioTestFactory;

namespace ZeroFloor.Tests;

/// <summary>Простая реализация IScenarioRepository для юнит-тестов движка без файлов на диске.</summary>
internal sealed class InMemoryScenarioRepository : IScenarioRepository
{
    private readonly Dictionary<string, SceneDefinition> _scenes;

    public InMemoryScenarioRepository(params SceneDefinition[] scenes) =>
        _scenes = scenes.ToDictionary(s => s.SceneId);

    public IReadOnlyCollection<string> SceneIds => _scenes.Keys;

    public SceneDefinition? GetScene(string sceneId) => _scenes.TryGetValue(sceneId, out var scene) ? scene : null;
}

public class DialogueManagerTests
{
    private static DialogueManager CreateManager(params SceneDefinition[] scenes)
    {
        var sceneManager = new SceneManager(new InMemoryScenarioRepository(scenes));
        return new DialogueManager(sceneManager, new EffectApplier());
    }

    [Fact]
    public void EnterScene_SetsCurrentSceneAndEntryNode()
    {
        var scene = Scene("S1", Node("N1", "Первая реплика"), Node("N2", "Вторая реплика"));
        var manager = CreateManager(scene);
        var state = new GameState();

        manager.EnterScene("S1", state);

        Assert.Equal("S1", manager.CurrentScene.SceneId);
        Assert.Equal("N1", manager.CurrentNode.NodeId);
        Assert.Equal("S1", state.CurrentSceneId);
        Assert.Equal("N1", state.CurrentNodeId);
        Assert.Contains("N1", state.ViewedNodeIds);
    }

    [Fact]
    public void EnterScene_UnknownSceneId_Throws()
    {
        var manager = CreateManager();
        var state = new GameState();

        Assert.Throws<ScenarioValidationException>(() => manager.EnterScene("NO_SUCH_SCENE", state));
    }

    [Fact]
    public void GoToNode_MovesToExistingNode()
    {
        var scene = Scene("S1", Node("N1", "Первая"), Node("N2", "Вторая"));
        var manager = CreateManager(scene);
        var state = new GameState();
        manager.EnterScene("S1", state);

        manager.GoToNode("N2", state);

        Assert.Equal("N2", manager.CurrentNode.NodeId);
    }

    [Fact]
    public void GoToNode_UnknownNodeId_Throws()
    {
        var scene = Scene("S1", Node("N1", "Первая"));
        var manager = CreateManager(scene);
        var state = new GameState();
        manager.EnterScene("S1", state);

        Assert.Throws<ScenarioValidationException>(() => manager.GoToNode("N999", state));
    }

    [Fact]
    public void EnterScene_NodeWithoutText_Throws()
    {
        var badNode = new DialogueNode { NodeId = "N1", Speaker = "narrator", Text = "" };
        var scene = Scene("S1", badNode);
        var manager = CreateManager(scene);
        var state = new GameState();

        Assert.Throws<ScenarioValidationException>(() => manager.EnterScene("S1", state));
    }

    [Fact]
    public void EnterNode_AppliesNodeEffects()
    {
        var scene = Scene("S1", Node("N1", "Реплика с эффектом", effects: new[] { AddStat(StatId.H, 5) }));
        var manager = CreateManager(scene);
        var state = new GameState();

        manager.EnterScene("S1", state);

        Assert.Equal(65, state.GetStat(StatId.H));
    }

    [Fact]
    public void EnterSceneAt_ResumesOnSpecificNode_WithoutReplayingEntryNodeEffects()
    {
        var scene = Scene("S1",
            Node("N1", "Входной узел", effects: new[] { AddStat(StatId.H, 10) }, nextNodeId: "N2"),
            Node("N2", "Второй узел"));
        var manager = CreateManager(scene);
        var state = new GameState();

        manager.EnterSceneAt("S1", "N2", state);

        Assert.Equal("N2", manager.CurrentNode.NodeId);
        Assert.Equal(60, state.GetStat(StatId.H)); // эффект N1 не применён — на него не заходили
    }

    [Fact]
    public void EnterSceneAt_UnknownNodeId_Throws()
    {
        var scene = Scene("S1", Node("N1", "x"));
        var manager = CreateManager(scene);
        var state = new GameState();

        Assert.Throws<ScenarioValidationException>(() => manager.EnterSceneAt("S1", "N999", state));
    }

    [Fact]
    public void EnterScene_SceneWithoutNodes_Throws()
    {
        var scene = new SceneDefinition { SceneId = "EMPTY", Nodes = Array.Empty<DialogueNode>() };
        var manager = CreateManager(scene);
        var state = new GameState();

        Assert.Throws<ScenarioValidationException>(() => manager.EnterScene("EMPTY", state));
    }
}
