using ZeroFloor.Models;

namespace ZeroFloor.Engine;

/// <summary>
/// Система определения концовок (requirement 9) — единственный прямой гейт концовок
/// (Выбор 10). Условия и идентификаторы веток/концовок зафиксированы и взяты без изменений
/// из PROJECT_ANALYSIS.md, раздел 8, и CLAUDE.md, правило 2:
///   A «Побег наверх»      (END_03_ESCAPE)   — всегда доступна;
///   B «Обрушение»         (END_01_COLLAPSE) — K ≥ 40;
///   C «Новый Смотритель»  (END_02_SUCCESSOR)— father_file_found = true;
///   D «Баланс» (скрытая)  (END_04_BALANCE)  — H ≥ 70 И father_file_found = true И sonya_exposed = false.
/// Выделена в отдельный компонент намеренно (а не выражена как обычные выборы сцены),
/// чтобы критичная для всей игры логика гейта была в одном явном, тестируемом месте —
/// см. TECHNICAL_ARCHITECTURE.md, раздел 4. Не менять условия/идентификаторы без отдельного
/// решения сценариста.
/// </summary>
public sealed class EndingManager
{
    private static readonly IReadOnlyList<EndingBranch> Branches = new[]
    {
        new EndingBranch("A", "END_03_ESCAPE", Condition: null),

        new EndingBranch("B", "END_01_COLLAPSE", new StatComparisonCondition
        {
            Stat = StatId.K,
            Operator = ComparisonOperator.GreaterThanOrEqual,
            Value = 40
        }),

        new EndingBranch("C", "END_02_SUCCESSOR", new FlagEqualsCondition
        {
            FlagKey = "father_file_found",
            ExpectedValue = FlagValue.FromBoolean(true)
        }),

        new EndingBranch("D", "END_04_BALANCE", new AndCondition
        {
            Children = new ConditionNode[]
            {
                new StatComparisonCondition { Stat = StatId.H, Operator = ComparisonOperator.GreaterThanOrEqual, Value = 70 },
                new FlagEqualsCondition { FlagKey = "father_file_found", ExpectedValue = FlagValue.FromBoolean(true) },
                new FlagEqualsCondition { FlagKey = "sonya_exposed", ExpectedValue = FlagValue.FromBoolean(false) }
            }
        })
    };

    private readonly ConditionEvaluator _conditionEvaluator;

    public EndingManager(ConditionEvaluator conditionEvaluator) => _conditionEvaluator = conditionEvaluator;

    /// <summary>Ветки, доступные игроку прямо сейчас — ветка A присутствует всегда.</summary>
    public IReadOnlyList<string> GetAvailableBranchIds(GameState state) =>
        Branches
            .Where(branch => _conditionEvaluator.Evaluate(branch.Condition, state))
            .Select(branch => branch.BranchId)
            .ToList();

    /// <summary>
    /// Фиксирует выбранную ветку и возвращает id концовки. После первого успешного вызова
    /// концовка зафиксирована в GameState.CurrentEndingId и повторный вызов (в т.ч. с другой
    /// веткой) запрещён — ветка Gate10 не пересчитывается задним числом ни при каких
    /// последующих изменениях шкал (CLAUDE.md, правило 2).
    /// </summary>
    public string ResolveEnding(string branchId, GameState state)
    {
        if (state.CurrentEndingId is not null)
            throw new ScenarioValidationException(
                $"Концовка уже определена ('{state.CurrentEndingId}', ветка '{state.CurrentBranchId}') " +
                "и не может быть пересчитана повторно.");

        if (string.IsNullOrWhiteSpace(branchId))
            throw new ScenarioValidationException("Не указан идентификатор ветки концовки.");

        var branch = Branches.FirstOrDefault(b => b.BranchId == branchId)
            ?? throw new ScenarioValidationException($"Неизвестная ветка концовки: '{branchId}'.");

        if (!_conditionEvaluator.Evaluate(branch.Condition, state))
            throw new ScenarioValidationException(
                $"Ветка '{branchId}' сейчас недоступна — условие разблокировки не выполнено.");

        state.CurrentBranchId = branch.BranchId;
        state.CurrentEndingId = branch.EndingId;
        return branch.EndingId;
    }

    private sealed record EndingBranch(string BranchId, string EndingId, ConditionNode? Condition);
}
