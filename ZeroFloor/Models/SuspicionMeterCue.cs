namespace ZeroFloor.Models;

public enum SuspicionMeterKind
{
    Show,
    Hide
}

/// <summary>
/// Режиссёрская ремарка узла (как CharacterCue/CameraCue/ScreenFadeCue): делает видимым для
/// игрока значение произвольного числового флага (в игре — Глава 5, флаг suspicion) как
/// постоянный HUD-индикатор, не блокирующий диалог/выбор, в отличие от мини-игр с MinigameCue.
/// Show — индикатор появляется и остаётся виден на всех последующих узлах, пока не встретится
/// узел с Hide (сам индикатор не привязан к конкретному узлу — это persistent-состояние
/// NovelViewModel, см. его комментарий). Engine эту ремарку не читает и не валидирует, кроме
/// базовой целостности диапазона (см. ScenarioValidator) — значение самого флага по-прежнему
/// считает только обычный setFlag в эффектах узлов/выборов, эта ремарка лишь его показывает.
/// </summary>
public sealed class SuspicionMeterCue
{
    public required SuspicionMeterKind Kind { get; init; }

    /// <summary>Ключ флага, чьё значение показывается индикатором (по умолчанию — suspicion).
    /// Актуален только при Kind=Show.</summary>
    public string FlagKey { get; init; } = "suspicion";

    public int MinValue { get; init; } = 0;
    public int MaxValue { get; init; } = 10;

    /// <summary>Значения &gt;= этого порога, но &lt; RedThreshold, закрашивают индикатор жёлтым.</summary>
    public int YellowThreshold { get; init; } = 4;

    /// <summary>Значения &gt;= этого порога закрашивают индикатор красным.</summary>
    public int RedThreshold { get; init; } = 6;
}
