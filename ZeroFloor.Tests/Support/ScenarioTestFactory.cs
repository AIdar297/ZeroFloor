using ZeroFloor.Models;

namespace ZeroFloor.Tests.Support;

/// <summary>Короткие фабричные методы для сборки тестовых сцен/узлов/выборов без JSON —
/// уменьшают дублирование однотипных object-initializer'ов по всему тестовому проекту.</summary>
internal static class ScenarioTestFactory
{
    public static SceneDefinition Scene(string sceneId, params DialogueNode[] nodes) => new()
    {
        SceneId = sceneId,
        Nodes = nodes
    };

    public static DialogueNode Node(
        string nodeId,
        string text,
        string speaker = "narrator",
        string? nextNodeId = null,
        string? nextSceneId = null,
        IReadOnlyList<ChoiceDefinition>? choices = null,
        IReadOnlyList<Effect>? effects = null) => new()
    {
        NodeId = nodeId,
        Speaker = speaker,
        Text = text,
        NextNodeId = nextNodeId,
        NextSceneId = nextSceneId,
        Choices = choices ?? Array.Empty<ChoiceDefinition>(),
        Effects = effects ?? Array.Empty<Effect>()
    };

    public static ChoiceDefinition Choice(
        string choiceId,
        string text,
        ConditionNode? condition = null,
        IReadOnlyList<Effect>? effects = null,
        string? nextNodeId = null,
        string? nextSceneId = null,
        string? endingId = null) => new()
    {
        ChoiceId = choiceId,
        Text = text,
        Condition = condition,
        Effects = effects ?? Array.Empty<Effect>(),
        NextNodeId = nextNodeId,
        NextSceneId = nextSceneId,
        EndingId = endingId
    };

    public static Effect AddStat(StatId stat, int delta) => new()
    {
        Type = EffectType.AddStat,
        Key = stat.ToString(),
        NumericValue = delta
    };

    public static Effect SetStat(StatId stat, int value) => new()
    {
        Type = EffectType.SetStat,
        Key = stat.ToString(),
        NumericValue = value
    };

    public static Effect SetFlag(string key, bool value) => new()
    {
        Type = EffectType.SetFlag,
        Key = key,
        FlagValue = FlagValue.FromBoolean(value)
    };

    public static StatComparisonCondition StatAtLeast(StatId stat, int value) => new()
    {
        Stat = stat,
        Operator = ComparisonOperator.GreaterThanOrEqual,
        Value = value
    };

    public static FlagEqualsCondition FlagIs(string key, bool value) => new()
    {
        FlagKey = key,
        ExpectedValue = FlagValue.FromBoolean(value)
    };
}
