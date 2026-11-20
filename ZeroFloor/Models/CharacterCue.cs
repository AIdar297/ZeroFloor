namespace ZeroFloor.Models;

public enum CharacterCueKind
{
    Enter,
    Exit,
    MoveTo,
    ScaleTo
}

/// <summary>
/// Сценарная "режиссёрская ремарка" для персонажа на узле: появиться/исчезнуть/переместиться/
/// изменить масштаб. Чистые данные — никакой WPF здесь нет; выполняет их Presentation-слой
/// (ICharacterAnimationService), не Engine.
/// </summary>
public sealed class CharacterCue
{
    public required string CharacterId { get; init; }
    public required CharacterCueKind Kind { get; init; }

    /// <summary>Для Enter/MoveTo — куда встать. Для Enter позиция обязательна по смыслу
    /// (хотя структурно не required, чтобы не плодить варианты конструктора в JSON).</summary>
    public StagePosition? Position { get; init; }

    /// <summary>Для ScaleTo — целевой масштаб (1.0 — обычный размер). Также может
    /// сопровождать Enter/MoveTo (например, "подходит ближе" = чуть крупнее).</summary>
    public double? Scale { get; init; }

    /// <summary>Вертикальная поправка положения в пикселях от базовой линии сцены (0 —
    /// персонаж стоит на "полу"; отрицательное — выше, положительное — ниже). Задаётся сценой,
    /// не персонажем (см. CharacterDefinition) — требование "положение по вертикали,
    /// настраиваемое отдельно для каждой сцены". Для Enter/MoveTo; null равнозначно 0.</summary>
    public double? VerticalOffset { get; init; }

    /// <summary>Порядок отрисовки среди персонажей, видимых одновременно на сцене — больше
    /// значение рисуется поверх (аналог CSS/WPF z-index). 0 по умолчанию — персонажи без явно
    /// заданного порядка рисуются в порядке появления (requirement "порядок отображения").</summary>
    public int ZIndex { get; init; }

    public int DurationMs { get; init; } = 600;
}
