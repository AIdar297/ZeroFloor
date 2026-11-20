namespace ZeroFloor.Services;

/// <summary>Легковесное описание одного слота для экрана "Сохранения" — не требует разбора
/// всего GameState. IsCorrupted=true (CorruptionMessage заполнен) — требование "защита от
/// повреждённых файлов сохранения": такой слот не ломает список остальных, просто показывается
/// как нечитаемый, с возможностью сохранить в него поверх (перезапись решает проблему), но без
/// возможности загрузить.</summary>
public sealed record SaveSlotSummary(
    SaveSlotKind Kind,
    int SlotIndex,
    bool IsOccupied,
    bool IsCorrupted,
    DateTime? Timestamp,
    int PlayTimeSeconds,
    string? ChapterId,
    string? SceneId,
    string? LocationSummary,
    string? CurrentEndingId,
    string? CorruptionMessage)
{
    public bool CanLoad => IsOccupied && !IsCorrupted;
}
