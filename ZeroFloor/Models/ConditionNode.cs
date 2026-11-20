using System.Text.Json.Serialization;

namespace ZeroFloor.Models;

/// <summary>
/// Дерево условий доступности выбора/узла/ветки концовки. Полиморфная JSON-сериализация
/// по дискриминатору "type" — см. ScenarioJsonOptions. Вычисляется ConditionEvaluator.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(StatComparisonCondition), "statCompare")]
[JsonDerivedType(typeof(FlagEqualsCondition), "flagEquals")]
[JsonDerivedType(typeof(AndCondition), "and")]
[JsonDerivedType(typeof(OrCondition), "or")]
[JsonDerivedType(typeof(NotCondition), "not")]
public abstract class ConditionNode
{
}

public enum ComparisonOperator
{
    GreaterThanOrEqual,
    LessThanOrEqual,
    GreaterThan,
    LessThan,
    Equal
}

public sealed class StatComparisonCondition : ConditionNode
{
    public required StatId Stat { get; init; }
    public required ComparisonOperator Operator { get; init; }
    public required int Value { get; init; }
}

public sealed class FlagEqualsCondition : ConditionNode
{
    public required string FlagKey { get; init; }

    // В JSON — просто "value" (как у Effect), чтобы сценарные данные выглядели единообразно:
    // {"type": "flagEquals", "flagKey": "...", "value": true}.
    [JsonPropertyName("value")]
    public required FlagValue ExpectedValue { get; init; }
}

public sealed class AndCondition : ConditionNode
{
    public required IReadOnlyList<ConditionNode> Children { get; init; }
}

public sealed class OrCondition : ConditionNode
{
    public required IReadOnlyList<ConditionNode> Children { get; init; }
}

public sealed class NotCondition : ConditionNode
{
    public required ConditionNode Child { get; init; }
}
