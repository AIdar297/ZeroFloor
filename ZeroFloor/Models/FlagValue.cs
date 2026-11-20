namespace ZeroFloor.Models;

public enum FlagValueKind
{
    Boolean,
    Integer,
    String
}

public readonly struct FlagValue : IEquatable<FlagValue>
{
    public FlagValueKind Kind { get; }
    public bool BooleanValue { get; }
    public int IntegerValue { get; }
    public string? StringValue { get; }

    private FlagValue(FlagValueKind kind, bool booleanValue, int integerValue, string? stringValue)
    {
        Kind = kind;
        BooleanValue = booleanValue;
        IntegerValue = integerValue;
        StringValue = stringValue;
    }

    public static FlagValue FromBoolean(bool value) => new(FlagValueKind.Boolean, value, default, null);

    public static FlagValue FromInteger(int value) => new(FlagValueKind.Integer, default, value, null);

    public static FlagValue FromString(string value) => new(FlagValueKind.String, default, default, value);

    public bool Equals(FlagValue other) => Kind == other.Kind
        && BooleanValue == other.BooleanValue
        && IntegerValue == other.IntegerValue
        && StringValue == other.StringValue;

    public override bool Equals(object? obj) => obj is FlagValue other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Kind, BooleanValue, IntegerValue, StringValue);

    public override string ToString() => Kind switch
    {
        FlagValueKind.Boolean => BooleanValue.ToString(),
        FlagValueKind.Integer => IntegerValue.ToString(),
        FlagValueKind.String => StringValue ?? string.Empty,
        _ => string.Empty
    };
}
