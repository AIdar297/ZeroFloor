namespace ZeroFloor.Models;

public enum CameraCueKind
{
    ZoomIn,
    ZoomOut,
    Reset
}

/// <summary>Лёгкое кинематографичное приближение/отдаление всей сцены целиком (не персонажа).</summary>
public sealed class CameraCue
{
    public required CameraCueKind Kind { get; init; }

    /// <summary>Целевой масштаб сцены. По умолчанию — сдержанное значение (не "наезд на лицо").</summary>
    public double Amount { get; init; } = 1.06;

    public int DurationMs { get; init; } = 2200;
}
