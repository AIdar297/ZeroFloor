using ZeroFloor.Models;

namespace ZeroFloor.Engine;

/// <summary>
/// Обработчик выборов и условий (requirement: "предусмотри обработчик выборов и условий").
/// Фильтрует доступные варианты по условиям, применяет эффекты выбранного варианта ровно
/// один раз (защита от повторного применения последствий — GameState.AppliedChoiceIds)
/// и определяет, куда ведёт выбор.
/// </summary>
public sealed class ChoiceProcessor
{
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly EffectApplier _effectApplier;

    public ChoiceProcessor(ConditionEvaluator conditionEvaluator, EffectApplier effectApplier)
    {
        _conditionEvaluator = conditionEvaluator;
        _effectApplier = effectApplier;
    }

    /// <summary>Варианты, чьё условие (если оно задано) сейчас истинно — именно их показывает UI.</summary>
    public IReadOnlyList<ChoiceDefinition> GetAvailableChoices(DialogueNode node, GameState state) =>
        node.Choices.Where(choice => _conditionEvaluator.Evaluate(choice.Condition, state)).ToList();

    /// <summary>
    /// Применяет выбор с указанным id. Если выбор уже был применён ранее (повторная загрузка
    /// сохранения на этой же сцене, двойной клик и т.п.) — эффекты не применяются повторно,
    /// но переход всё равно корректно определяется (идемпотентность).
    /// </summary>
    public ChoiceOutcome ApplyChoice(DialogueNode node, string choiceId, GameState state)
    {
        if (string.IsNullOrWhiteSpace(choiceId))
            throw new ScenarioValidationException("Не указан идентификатор выбора.");

        var choice = node.Choices.FirstOrDefault(c => c.ChoiceId == choiceId)
            ?? throw new ScenarioValidationException(
                $"Узел '{node.NodeId}' не содержит выбора с id '{choiceId}'.");

        if (!_conditionEvaluator.Evaluate(choice.Condition, state))
            throw new ScenarioValidationException(
                $"Выбор '{choiceId}' в узле '{node.NodeId}' сейчас недоступен — условие не выполнено.");

        if (!state.HasAppliedChoice(choiceId))
        {
            _effectApplier.Apply(choice.Effects, state);
            state.MarkChoiceApplied(choiceId);
        }

        return ResolveOutcome(node, choice);
    }

    private static ChoiceOutcome ResolveOutcome(DialogueNode node, ChoiceDefinition choice)
    {
        var destinationCount = Count(choice.NextNodeId) + Count(choice.NextSceneId) + Count(choice.EndingId);

        if (destinationCount != 1)
            throw new ScenarioValidationException(
                $"Выбор '{choice.ChoiceId}' в узле '{node.NodeId}' должен вести ровно в одно место " +
                $"(следующий узел, следующую сцену или концовку), а указано направлений: {destinationCount}.");

        if (choice.EndingId is not null)
            return ChoiceOutcome.ToEnding(choice.EndingId);

        if (choice.NextSceneId is not null)
            return ChoiceOutcome.ToScene(choice.NextSceneId);

        return ChoiceOutcome.ToNode(choice.NextNodeId!);
    }

    private static int Count(string? value) => string.IsNullOrEmpty(value) ? 0 : 1;
}
