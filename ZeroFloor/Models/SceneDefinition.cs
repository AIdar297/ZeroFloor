namespace ZeroFloor.Models;

public sealed class SceneDefinition
{
    public required string SceneId { get; init; }

    // Человекочитаемое название сцены для сценарных документов/редактора; движком и
    // валидатором не используется и не влияет на игровую логику (чисто справочное поле).
    public string? Title { get; init; }

    public string? Background { get; init; }
    public string? Music { get; init; }
    public string? Ambience { get; init; }

    // Как сцена визуально встречает игрока — чистые данные для Presentation-слоя.
    public BackgroundTransitionCue? BackgroundTransition { get; init; }
    public AmbientEffectKind AmbientEffect { get; init; } = AmbientEffectKind.None;

    public IReadOnlyList<DialogueNode> Nodes { get; init; } = Array.Empty<DialogueNode>();
}
