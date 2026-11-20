namespace ZeroFloor.Models;

/// <summary>Запись о сделанном игроком выборе — требование "история сделанных выборов"
/// в сохранении. Записывается один раз, в момент первого применения выбора (см.
/// GameSessionController.SelectChoice) — повторная загрузка сейва и повторный проход того
/// же узла не добавляют дубликат записи (идемпотентность, как у самих эффектов выбора).</summary>
public sealed record ChoiceHistoryEntry(
    string SceneId,
    string NodeId,
    string ChoiceId,
    string ChoiceText,
    DateTime Timestamp);
