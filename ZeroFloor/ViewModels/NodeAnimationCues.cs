using ZeroFloor.Models;

namespace ZeroFloor.ViewModels;

/// <summary>
/// Чистые данные о том, что должно проанимировать View при входе в текущий узел/сцену —
/// никаких ссылок на WPF. ViewModel только передаёт это дальше (из DialogueNode/SceneDefinition),
/// фактическую анимацию строит View через ICharacterAnimationService/ISceneTransitionService.
/// </summary>
public sealed record NodeAnimationCues(
    string Speaker,
    IReadOnlyList<CharacterCue> CharacterCues,
    CameraCue? CameraCue,
    ScreenFadeCue? ScreenFadeCue,
    bool IsSceneEntry,
    string? Background,
    BackgroundTransitionCue? BackgroundTransition,
    AmbientEffectKind AmbientEffect);
