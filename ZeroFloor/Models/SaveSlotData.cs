namespace ZeroFloor.Models;

/// <summary>Полный снимок прохождения для одного слота сохранения — требование раздела
/// PROJECT_ANALYSIS 13.4/TECHNICAL_ARCHITECTURE раздел 9: глава, сцена, узел, состояние
/// концовки (если достигнута) и весь GameState (переменные, флаги, appliedChoiceIds,
/// история просмотренных узлов, история выборов, журнал реплик — см. GameState).
/// ChapterId/SceneId/NodeId/CurrentEndingId дублируют соответствующие поля GameState на
/// верхнем уровне сознательно — чтобы список слотов (SaveSlotSummary) мог показать главу/
/// сцену без необходимости разбирать весь GameState.</summary>
public sealed record SaveSlotData
{
    public required int SaveVersion { get; init; }
    public required DateTime Timestamp { get; init; }
    public required int PlayTimeSeconds { get; init; }
    public string? ChapterId { get; init; }
    public string? SceneId { get; init; }
    public string? NodeId { get; init; }
    public string? CurrentEndingId { get; init; }

    /// <summary>Краткое описание места сохранения для экрана "Сохранения" — говорящий и
    /// отрывок текущей реплики (напр. "Макс: Город молчит, но не спит…"). Формируется в
    /// момент сохранения (NovelViewModel), не восстанавливается из сценария — переживает
    /// правки текста реплик в JSON без порчи старых сейвов.</summary>
    public string? LocationSummary { get; init; }

    public required GameState GameState { get; init; }
}
