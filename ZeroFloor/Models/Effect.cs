namespace ZeroFloor.Models;

public enum EffectType
{
    SetFlag,
    AddStat,
    SetStat
}

public sealed class Effect
{
    public required EffectType Type { get; init; }
    public required string Key { get; init; }
    public FlagValue? FlagValue { get; init; }
    public int? NumericValue { get; init; }
}
