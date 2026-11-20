namespace ZeroFloor.Models;

public enum ScreenFadeKind
{
    FadeToBlack,
    FadeFromBlack
}

/// <summary>Полноэкранное затемнение/осветление — киношная перебивка между эпизодами сцены.</summary>
public sealed class ScreenFadeCue
{
    public required ScreenFadeKind Kind { get; init; }
    public int DurationMs { get; init; } = 900;
}
