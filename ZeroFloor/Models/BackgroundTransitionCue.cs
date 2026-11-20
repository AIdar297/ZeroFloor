namespace ZeroFloor.Models;

/// <summary>Переход между фоновыми изображениями при входе в сцену — мягкий кроссфейд
/// плюс (опционально) лёгкий параллакс, пока фон виден.</summary>
public sealed class BackgroundTransitionCue
{
    public int DurationMs { get; init; } = 1100;
    public bool EnableParallax { get; init; } = true;
}

public enum AmbientEffectKind
{
    None,
    Dust,
    Haze,
    Flicker
}
