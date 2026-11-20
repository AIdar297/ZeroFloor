using ZeroFloor.Models;

namespace ZeroFloor.Engine;

/// <summary>
/// Система последовательного выполнения событий (requirement 10): связывает
/// DialogueManager, ChoiceProcessor и EndingManager в один поток операций —
/// войти в сцену → показать узел → (дождаться выбора ИЛИ продолжить) → применить
/// последствия ровно один раз → перейти дальше → … → зафиксировать концовку.
/// Это единственный класс, который должен знать, что из движка когда вызывать;
/// presentation-слой (будущий NovelViewModel) обращается только к нему.
/// </summary>
public sealed class GameSessionController
{
    private readonly DialogueManager _dialogueManager;
    private readonly ChoiceProcessor _choiceProcessor;
    private readonly EndingManager _endingManager;
    private readonly EffectApplier _effectApplier;

    public GameSessionController(
        DialogueManager dialogueManager,
        ChoiceProcessor choiceProcessor,
        EndingManager endingManager,
        GameState state,
        EffectApplier? effectApplier = null)
    {
        _dialogueManager = dialogueManager;
        _choiceProcessor = choiceProcessor;
        _endingManager = endingManager;
        _effectApplier = effectApplier ?? new EffectApplier();
        State = state;
    }

    /// <summary>Максимальное изменение одной шкалы (K/L/R/H) за один эффект исхода мини-игры.
    /// Решение автора (2026-11-17): мини-игры могут влиять на шкалы и любые флаги, включая гейты
    /// концовок, но не сильнее ±10 за эффект — как крупный сюжетный выбор, не больше.</summary>
    public const int MaxMinigameStatDelta = 10;

    /// <summary>
    /// Применяет эффекты исхода мини-игры (MinigameCue.Episode3D.OutcomeEffects) через тот же
    /// EffectApplier, что и узлы/выборы. Разрешены setFlag (любой флаг) и addStat в пределах
    /// ±MaxMinigameStatDelta; setStat запрещён — исход мини-игры сдвигает шкалу, а не задаёт её
    /// заново. Нарушение — ошибка данных, а не молчаливое игнорирование.
    /// </summary>
    public void ApplyMinigameOutcomeEffects(IReadOnlyList<Effect> effects)
    {
        EnsureNotEnded();

        foreach (var effect in effects)
        {
            if (!IsAllowedMinigameOutcomeEffect(effect))
                throw new ScenarioValidationException(
                    $"Недопустимый эффект исхода мини-игры: {effect.Type} '{effect.Key}' (разрешены setFlag и addStat в пределах ±{MaxMinigameStatDelta}).");
        }

        _effectApplier.Apply(effects, State);
    }

    public static bool IsAllowedMinigameOutcomeEffect(Effect effect) =>
        effect.Type == EffectType.SetFlag ||
        (effect.Type == EffectType.AddStat && effect.NumericValue is { } delta && Math.Abs(delta) <= MaxMinigameStatDelta);

    public GameState State { get; }

    public SceneDefinition CurrentScene => _dialogueManager.CurrentScene;

    public DialogueNode CurrentNode => _dialogueManager.CurrentNode;

    public bool HasEnded => State.CurrentEndingId is not null;

    /// <summary>Сигнал presentation-слою выполнить автосохранение (см. AutoSaveTrigger) —
    /// движок сам не пишет на диск, этим занимается подписчик (NovelViewModel), у которого
    /// есть недоступный движку контекст (время игры, описание места).</summary>
    public event Action<AutoSaveTrigger>? AutoSaveRequested;

    /// <summary>Начинает сессию с входного узла указанной сцены.</summary>
    public void Start(string startingSceneId)
    {
        _dialogueManager.EnterScene(startingSceneId, State);
        AutoSaveRequested?.Invoke(AutoSaveTrigger.ChapterEntered);
    }

    /// <summary>Возобновляет сессию сразу на сохранённом узле (например, после загрузки) —
    /// в отличие от <see cref="Start"/>, не проигрывает входной узел сцены повторно.</summary>
    public void Resume(string sceneId, string nodeId) => _dialogueManager.EnterSceneAt(sceneId, nodeId, State);

    /// <summary>Варианты выбора текущего узла, доступные при текущем состоянии переменных/флагов.</summary>
    public IReadOnlyList<ChoiceDefinition> GetAvailableChoices()
    {
        EnsureNotEnded();
        return _choiceProcessor.GetAvailableChoices(CurrentNode, State);
    }

    /// <summary>
    /// Продолжает повествование без выбора. Допустимо только для узлов без вариантов выбора —
    /// узел с выборами обязан быть пройден через <see cref="SelectChoice"/>.
    /// </summary>
    public void Advance()
    {
        EnsureNotEnded();

        if (CurrentNode.Choices.Count > 0)
            throw new ScenarioValidationException(
                $"Узел '{CurrentNode.NodeId}' требует выбора одного из вариантов и не может быть пройден кнопкой «продолжить».");

        MoveTo(CurrentNode.NextNodeId, CurrentNode.NextSceneId, endingId: null);
    }

    /// <summary>
    /// Переход на конкретный узел ТЕКУЩЕЙ сцены в обход nextNodeId/nextSceneId текущего узла.
    /// Единственный вызывающий — Presentation-слой при провале мини-игры TimingBar (см.
    /// DialogueNode.MinigameCue.FailureNodeId): в отличие от обычного Advance()/SelectChoice(),
    /// здесь нет узлового выбора игрока — решение приняла мини-игра, а не сценарный граф.
    /// </summary>
    public void JumpToNode(string nodeId)
    {
        EnsureNotEnded();
        _dialogueManager.GoToNode(nodeId, State);
    }

    /// <summary>Применяет выбор игрока и переходит к результату (узел/сцена/концовка).</summary>
    public void SelectChoice(string choiceId)
    {
        EnsureNotEnded();

        var choice = CurrentNode.Choices.FirstOrDefault(c => c.ChoiceId == choiceId)
            ?? throw new ScenarioValidationException(
                $"Узел '{CurrentNode.NodeId}' не содержит выбора с id '{choiceId}'.");

        var wasAlreadyApplied = State.HasAppliedChoice(choiceId);
        var sceneIdBeforeMove = CurrentScene.SceneId;
        var nodeIdBeforeMove = CurrentNode.NodeId;

        var outcome = _choiceProcessor.ApplyChoice(CurrentNode, choiceId, State);

        // Записывается один раз, в момент первого реального применения — повторная загрузка
        // сейва на этом же узле не дублирует запись истории (та же идемпотентность, что и у
        // самих эффектов выбора, см. ChoiceProcessor).
        if (!wasAlreadyApplied)
        {
            State.RecordChoice(new ChoiceHistoryEntry(
                sceneIdBeforeMove, nodeIdBeforeMove, choiceId, choice.Text, DateTime.Now));
        }

        MoveTo(outcome.NodeId, outcome.SceneId, outcome.EndingId);
    }

    /// <summary>Ветки Gate10, доступные прямо сейчас (см. EndingManager).</summary>
    public IReadOnlyList<string> GetAvailableEndingBranches()
    {
        EnsureNotEnded();
        AutoSaveRequested?.Invoke(AutoSaveTrigger.BeforeFinalChoice);
        return _endingManager.GetAvailableBranchIds(State);
    }

    /// <summary>Фиксирует выбранную ветку Gate10 и завершает сессию концовкой.</summary>
    public string ResolveEnding(string branchId)
    {
        EnsureNotEnded();
        return _endingManager.ResolveEnding(branchId, State);
    }

    private void MoveTo(string? nodeId, string? sceneId, string? endingId)
    {
        if (endingId is not null)
        {
            // Обычный выбор (ChoiceDefinition.EndingId) задаёт итоговый id концовки напрямую,
            // в отличие от Gate10, где ветки и их условия — исключительная зона ответственности
            // EndingManager (см. его комментарий). Единственное, что здесь переиспользуется из
            // той же логики, — запрет повторной фиксации уже определённой концовки.
            if (State.CurrentEndingId is not null)
                throw new ScenarioValidationException(
                    $"Концовка уже определена ('{State.CurrentEndingId}') и не может быть установлена повторно.");

            State.CurrentEndingId = endingId;
            return;
        }

        if (sceneId is not null)
        {
            _dialogueManager.EnterScene(sceneId, State);
            AutoSaveRequested?.Invoke(AutoSaveTrigger.ChapterEntered);
            return;
        }

        if (nodeId is not null)
        {
            _dialogueManager.GoToNode(nodeId, State);
            return;
        }

        throw new ScenarioValidationException(
            $"Узел '{CurrentNode.NodeId}' не задаёт следующий шаг (нет nextNodeId/nextSceneId) и не содержит выборов — сценарный тупик.");
    }

    private void EnsureNotEnded()
    {
        if (HasEnded)
            throw new ScenarioValidationException(
                $"Сессия уже завершена концовкой '{State.CurrentEndingId}' — дальнейшие действия невозможны.");
    }
}
