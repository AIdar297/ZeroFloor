namespace ZeroFloor.Models;

/// <summary>Лист досье: заголовок и подписи полей (места для обрывков).</summary>
public sealed class DossierSheet
{
    public required string Title { get; init; }
    public IReadOnlyList<string> SlotLabels { get; init; } = Array.Empty<string>();
}

/// <summary>Один обрывок досье: к какому досье относится (индекс в Dossiers), на какое место
/// (0..N-1) в нём встаёт и что на нём написано. Тексты — фрагменты уже существующих реплик
/// досье Главы 8, а не новый сюжет.</summary>
public sealed class DossierFragment
{
    public required int Dossier { get; init; }
    public required int Slot { get; init; }
    public required string Text { get; init; }
}

/// <summary>
/// Данные мини-игры «Сборка досье» (Глава 8, сцена 8.2, MinigameKind.DossierAssembly): игрок
/// раскладывает перемешанные обрывки по местам в двух досье. Хорошая сборка (не меньше
/// SuccessFraction мест за отведённое время) выбирает SuccessChoiceId узла, иначе —
/// FailureChoiceId; оба — уже существующие выборы узла, их эффекты применяет ChoiceProcessor.
/// Дополнительные эффекты исхода — SuccessEffects/FailureEffects (правило мини-игр: setFlag и
/// addStat в пределах ±10, см. GameSessionController.ApplyMinigameOutcomeEffects).
/// </summary>
public sealed class DossierAssemblyLayout
{
    public IReadOnlyList<DossierSheet> Dossiers { get; init; } = Array.Empty<DossierSheet>();
    public IReadOnlyList<DossierFragment> Fragments { get; init; } = Array.Empty<DossierFragment>();
    public int TimeLimitSeconds { get; init; } = 60;
    public int MaxMistakes { get; init; } = 4;
    public double SuccessFraction { get; init; } = 0.75;
    public required string SuccessChoiceId { get; init; }
    public required string FailureChoiceId { get; init; }
    public IReadOnlyList<Effect> SuccessEffects { get; init; } = Array.Empty<Effect>();
    public IReadOnlyList<Effect> FailureEffects { get; init; } = Array.Empty<Effect>();
}
