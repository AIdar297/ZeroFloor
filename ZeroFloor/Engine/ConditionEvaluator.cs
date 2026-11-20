using ZeroFloor.Models;

namespace ZeroFloor.Engine;

/// <summary>
/// Вычисляет дерево условий (<see cref="ConditionNode"/>) над текущим <see cref="GameState"/>.
/// Чистая функция без побочных эффектов — единственное место в проекте, где "понимается"
/// смысл операторов сравнения и логических связок (requirement: больше/меньше/равно,
/// И/ИЛИ/НЕ, проверка флагов). См. PROJECT_ANALYSIS.md, разделы 7–8.
/// </summary>
public sealed class ConditionEvaluator
{
    /// <summary>Отсутствие условия (null) означает «всегда доступно» — так ветка A Gate10
    /// и большинство обычных реплик без условия моделируются без лишнего кода на вызывающей
    /// стороне.</summary>
    public bool Evaluate(ConditionNode? condition, GameState state)
    {
        if (condition is null)
            return true;

        return condition switch
        {
            StatComparisonCondition c => EvaluateStatComparison(c, state),
            FlagEqualsCondition c => EvaluateFlagEquals(c, state),
            AndCondition c => EvaluateAnd(c, state),
            OrCondition c => EvaluateOr(c, state),
            NotCondition c => !Evaluate(c.Child, state),
            _ => throw new ScenarioValidationException(
                $"Неизвестный тип узла условия: '{condition.GetType().Name}'.")
        };
    }

    private static bool EvaluateStatComparison(StatComparisonCondition condition, GameState state)
    {
        var actual = state.GetStat(condition.Stat);

        return condition.Operator switch
        {
            ComparisonOperator.GreaterThan => actual > condition.Value,
            ComparisonOperator.GreaterThanOrEqual => actual >= condition.Value,
            ComparisonOperator.LessThan => actual < condition.Value,
            ComparisonOperator.LessThanOrEqual => actual <= condition.Value,
            ComparisonOperator.Equal => actual == condition.Value,
            _ => throw new ScenarioValidationException(
                $"Неизвестный оператор сравнения: '{condition.Operator}'.")
        };
    }

    private static bool EvaluateFlagEquals(FlagEqualsCondition condition, GameState state)
    {
        if (string.IsNullOrWhiteSpace(condition.FlagKey))
            throw new ScenarioValidationException("У условия проверки флага не указан ключ флага.");

        var actual = state.GetFlag(condition.FlagKey);

        // Флаг, который ещё ни разу не устанавливался, считается равным нулевому значению
        // своего типа (false / 0 / пустая строка) — это и есть поведение "по умолчанию",
        // многократно описанное в Story Bible (например, sonya_exposed = ложь по умолчанию).
        if (actual is null)
        {
            return condition.ExpectedValue.Kind switch
            {
                FlagValueKind.Boolean => condition.ExpectedValue.BooleanValue == false,
                FlagValueKind.Integer => condition.ExpectedValue.IntegerValue == 0,
                FlagValueKind.String => string.IsNullOrEmpty(condition.ExpectedValue.StringValue),
                _ => false
            };
        }

        return actual.Value.Equals(condition.ExpectedValue);
    }

    private bool EvaluateAnd(AndCondition condition, GameState state)
    {
        if (condition.Children.Count == 0)
            throw new ScenarioValidationException("Условие 'and' не содержит дочерних условий.");

        return condition.Children.All(child => Evaluate(child, state));
    }

    private bool EvaluateOr(OrCondition condition, GameState state)
    {
        if (condition.Children.Count == 0)
            throw new ScenarioValidationException("Условие 'or' не содержит дочерних условий.");

        return condition.Children.Any(child => Evaluate(child, state));
    }
}
