namespace ZeroFloor.Models;

public sealed class DialogueNode
{
    public required string NodeId { get; init; }
    public required string Speaker { get; init; }
    public required string Text { get; init; }
    public IReadOnlyList<ChoiceDefinition> Choices { get; init; } = Array.Empty<ChoiceDefinition>();
    public IReadOnlyList<Effect> Effects { get; init; } = Array.Empty<Effect>();
    public string? NextNodeId { get; init; }
    public string? NextSceneId { get; init; }

    // Режиссёрские ремарки узла — чистые данные, выполняет Presentation-слой при входе
    // в узел; Engine их не читает и не валидирует (не влияют на GameState/переходы).
    public IReadOnlyList<CharacterCue> CharacterCues { get; init; } = Array.Empty<CharacterCue>();
    public CameraCue? CameraCue { get; init; }
    public ScreenFadeCue? ScreenFadeCue { get; init; }

    // Мини-игра поверх узла (см. MinigameCue) — как и остальные ремарки выше, чистые
    // данные для Presentation-слоя; успех ведёт дальше по NextNodeId/NextSceneId этого
    // же узла, провал — к повтору мини-игры без последствий для GameState.
    public MinigameCue? MinigameCue { get; init; }

    // Персистентный HUD-индикатор значения флага (Глава 5, «Индикатор подозрения») — в отличие
    // от мини-игр выше, не блокирует диалог/выбор; см. комментарий SuspicionMeterCue.
    public SuspicionMeterCue? SuspicionMeterCue { get; init; }

    // Ограничение времени на ответ у ЭТОГО узла с выбором — см. комментарий ChoiceTimerCue.
    public ChoiceTimerCue? ChoiceTimerCue { get; init; }

    // Аудио конкретной реплики. VoiceClip — id файла озвучки в Assets/Audio/Voice/ (без
    // расширения, может содержать "/" для подпапок, напр. "max/ch01_n002"); запускается
    // синхронно с показом этого узла. Null — у реплики просто нет озвучки (не ошибка).
    // SoundEffects — одноразовые звуки, проигрываемые при входе в узел (напр. хлопок двери).
    public string? VoiceClip { get; init; }
    public IReadOnlyList<string> SoundEffects { get; init; } = Array.Empty<string>();
}
