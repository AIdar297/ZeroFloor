namespace ZeroFloor.Models;

public sealed class GameState
{
    private readonly Dictionary<StatId, int> _stats = new()
    {
        [StatId.K] = 50,
        [StatId.L] = 50,
        [StatId.R] = 50,
        [StatId.H] = 60
    };

    private readonly Dictionary<string, FlagValue> _flags = new();
    private readonly HashSet<string> _appliedChoiceIds = new();
    private readonly List<string> _viewedNodeIds = new();
    private readonly List<ChoiceHistoryEntry> _choiceHistory = new();
    private readonly List<DialogueLogRecord> _dialogueLog = new();

    public string? CurrentChapterId { get; set; }
    public string? CurrentSceneId { get; set; }
    public string? CurrentNodeId { get; set; }
    public string? CurrentEndingId { get; set; }
    public string? CurrentBranchId { get; set; }

    public IReadOnlyDictionary<StatId, int> Stats => _stats;
    public IReadOnlyDictionary<string, FlagValue> Flags => _flags;
    public IReadOnlyCollection<string> AppliedChoiceIds => _appliedChoiceIds;
    public IReadOnlyList<string> ViewedNodeIds => _viewedNodeIds;

    /// <summary>Хронология сделанных игроком выборов — требование "история сделанных выборов"
    /// в сохранении (см. ChoiceHistoryEntry). Только для чтения/журнала, не влияет на правила.</summary>
    public IReadOnlyList<ChoiceHistoryEntry> ChoiceHistory => _choiceHistory;

    /// <summary>Журнал реплик и записей о выборах текущего прохождения — требование "журнал
    /// уже прочитанных реплик и история диалогов". Хранится здесь (не только в ViewModel),
    /// чтобы переживать сохранение/загрузку.</summary>
    public IReadOnlyList<DialogueLogRecord> DialogueLog => _dialogueLog;

    public int GetStat(StatId stat) => _stats[stat];

    public void SetStat(StatId stat, int value) => _stats[stat] = Math.Clamp(value, 0, 100);

    public void AddToStat(StatId stat, int delta) => SetStat(stat, _stats[stat] + delta);

    public FlagValue? GetFlag(string key) => _flags.TryGetValue(key, out var value) ? value : null;

    public void SetFlag(string key, FlagValue value) => _flags[key] = value;

    public bool HasAppliedChoice(string choiceId) => _appliedChoiceIds.Contains(choiceId);

    public void MarkChoiceApplied(string choiceId) => _appliedChoiceIds.Add(choiceId);

    public void MarkNodeViewed(string nodeId)
    {
        if (!_viewedNodeIds.Contains(nodeId))
            _viewedNodeIds.Add(nodeId);
    }

    public void RecordChoice(ChoiceHistoryEntry entry) => _choiceHistory.Add(entry);

    public void AppendToDialogueLog(DialogueLogRecord record) => _dialogueLog.Add(record);

    /// <summary>Используется только восстановлением сейва (см. GameStateJsonConverter) —
    /// заменяет весь список целиком, не дописывает к уже накопленному.</summary>
    public void RestoreChoiceHistory(IEnumerable<ChoiceHistoryEntry> entries)
    {
        _choiceHistory.Clear();
        _choiceHistory.AddRange(entries);
    }

    /// <summary>Используется только восстановлением сейва (см. GameStateJsonConverter).</summary>
    public void RestoreDialogueLog(IEnumerable<DialogueLogRecord> records)
    {
        _dialogueLog.Clear();
        _dialogueLog.AddRange(records);
    }
}
