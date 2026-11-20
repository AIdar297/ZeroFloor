using ZeroFloor.Models;

namespace ZeroFloor.Services;

/// <summary>
/// Файловые сохранения — требование раздела 9 TECHNICAL_ARCHITECTURE.md: ≥10 ручных слотов +
/// одно автосохранение, атомарная запись, версия формата, устойчивость к повреждённым файлам.
/// </summary>
public interface ISaveService
{
    int ManualSlotCount { get; }

    /// <summary>Текущая версия формата сейва, которую пишет SaveManualSlot/SaveAutoSave —
    /// ViewModel использует её при сборке нового снимка, не завязываясь на конкретный класс.</summary>
    int CurrentSaveVersion { get; }

    bool HasAnySave { get; }

    IReadOnlyList<SaveSlotSummary> GetManualSlotSummaries();

    SaveSlotSummary GetAutoSaveSummary();

    /// <summary>Бросает <see cref="SaveLoadException"/> (или <see cref="SaveVersionMismatchException"/>),
    /// если слот пуст, повреждён или сделан несовместимой версией — вызывающий код обязан поймать.</summary>
    SaveSlotData LoadManualSlot(int slotIndex);

    /// <summary>См. <see cref="LoadManualSlot"/> — те же гарантии и исключения.</summary>
    SaveSlotData LoadAutoSave();

    void SaveManualSlot(int slotIndex, SaveSlotData data);

    void SaveAutoSave(SaveSlotData data);

    void DeleteManualSlot(int slotIndex);

    /// <summary>Самый свежий по времени сейв среди ручных слотов и автосохранения (без
    /// повреждённых) — для кнопки "Продолжить" в главном меню. Null, если сохранений нет вовсе.</summary>
    (SaveSlotKind Kind, int SlotIndex)? FindMostRecentSave();
}
