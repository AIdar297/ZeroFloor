using ZeroFloor.Models;

namespace ZeroFloor.Engine;

/// <summary>
/// Применяет список <see cref="Effect"/> к <see cref="GameState"/> (с clamp 0..100 для шкал,
/// который уже обеспечивает сам GameState.SetStat/AddToStat). Единственное место, где
/// setFlag/addStat/setStat из сценарных данных превращаются в реальное изменение состояния.
/// </summary>
public sealed class EffectApplier
{
    public void Apply(IEnumerable<Effect> effects, GameState state)
    {
        foreach (var effect in effects)
            Apply(effect, state);
    }

    public void Apply(Effect effect, GameState state)
    {
        switch (effect.Type)
        {
            case EffectType.SetFlag:
                state.SetFlag(RequireKey(effect), RequireFlagValue(effect));
                break;

            case EffectType.AddStat:
                state.AddToStat(ParseStat(effect), RequireNumericValue(effect));
                break;

            case EffectType.SetStat:
                state.SetStat(ParseStat(effect), RequireNumericValue(effect));
                break;

            default:
                throw new ScenarioValidationException($"Неизвестный тип эффекта: '{effect.Type}'.");
        }
    }

    private static string RequireKey(Effect effect) =>
        string.IsNullOrWhiteSpace(effect.Key)
            ? throw new ScenarioValidationException($"Эффект '{effect.Type}' не содержит ключа (имени флага/шкалы).")
            : effect.Key;

    private static FlagValue RequireFlagValue(Effect effect) =>
        effect.FlagValue
        ?? throw new ScenarioValidationException($"Эффект setFlag для ключа '{effect.Key}' не содержит значения флага.");

    private static int RequireNumericValue(Effect effect) =>
        effect.NumericValue
        ?? throw new ScenarioValidationException($"Эффект '{effect.Type}' для ключа '{effect.Key}' не содержит числового значения.");

    private static StatId ParseStat(Effect effect) =>
        Enum.TryParse<StatId>(effect.Key, ignoreCase: true, out var stat)
            ? stat
            : throw new ScenarioValidationException(
                $"'{effect.Key}' не является известной игровой шкалой (допустимы только K, L, R, H).");
}
