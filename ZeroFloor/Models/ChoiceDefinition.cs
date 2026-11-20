namespace ZeroFloor.Models;

public sealed class ChoiceDefinition
{
    public required string ChoiceId { get; init; }
    public required string Text { get; init; }
    public ConditionNode? Condition { get; init; }
    public IReadOnlyList<Effect> Effects { get; init; } = Array.Empty<Effect>();
    public string? NextNodeId { get; init; }
    public string? NextSceneId { get; init; }
    public string? EndingId { get; init; }
}
