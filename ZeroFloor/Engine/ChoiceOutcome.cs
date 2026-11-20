namespace ZeroFloor.Engine;

/// <summary>Куда ведёт применённый выбор: ровно одно из трёх направлений.</summary>
public sealed class ChoiceOutcome
{
    public string? NodeId { get; private init; }
    public string? SceneId { get; private init; }
    public string? EndingId { get; private init; }

    public static ChoiceOutcome ToNode(string nodeId) => new() { NodeId = nodeId };
    public static ChoiceOutcome ToScene(string sceneId) => new() { SceneId = sceneId };
    public static ChoiceOutcome ToEnding(string endingId) => new() { EndingId = endingId };
}
