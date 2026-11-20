namespace ZeroFloor.Models;

/// <summary>Одна запись журнала реплик/истории диалогов — требование "журнал уже прочитанных
/// реплик и история диалогов". Хранится в GameState (а не только в ViewModel), поэтому
/// переживает сохранение/загрузку — журнал не теряется при продолжении игры из сейва.
/// Kind различает обычную реплику персонажа/рассказчика от записи о сделанном игроком
/// выборе — обе показываются в одной хронологической ленте журнала.</summary>
public enum DialogueLogEntryKind
{
    Line,
    ChoiceMade
}

public sealed record DialogueLogRecord(
    DialogueLogEntryKind Kind,
    string SceneId,
    string NodeId,
    string Speaker,
    string Text,
    DateTime Timestamp);
