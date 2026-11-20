namespace ZeroFloor.Models;

/// <summary>
/// Данные мини-игры «Калибровка обходчика» (Глава 3, MinigameKind.RingLock) — чистая сценарная
/// конфигурация, читается только Presentation-слоем (RingLockViewModel), см. комментарий
/// MinigameCue. Два кольца с насечками: внешнее крутится автономно, внутреннее — игроком;
/// у каждого ровно один "паз" (насечка), и когда оба паза одновременно проходят через
/// контрольную точку, это окно совмещения. Контрольная точка не фиксирована намертво сверху —
/// каждые TargetHoldSeconds она перескакивает на новый случайный угол (см. RingLockViewModel —
/// не даёт просто пялиться в одно место на циферблате всю мини-игру). Игрок сам выбирает режим
/// сложности прямо в мини-игре (риск/выжидание — см. RingLockMode), а не отдельным сюжетным
/// выбором.
/// </summary>
public sealed class RingLockLayout
{
    public int OuterTabCount { get; init; } = 28;
    public int InnerTabCount { get; init; } = 20;

    /// <summary>Сколько секунд контрольная точка остаётся на одном месте, прежде чем перескочить
    /// на новый случайный угол.</summary>
    public double TargetHoldSeconds { get; init; } = 3;

    /// <summary>Скорость вращения внешнего кольца (градусы/сек) в режиме «рискнуть».</summary>
    public double RiskOuterSpeedDegPerSec { get; init; } = 90;

    /// <summary>Допуск совмещения (градусы от контрольной точки) в режиме «рискнуть» — уже окно.</summary>
    public double RiskToleranceDeg { get; init; } = 6;

    /// <summary>Скорость вращения внешнего кольца (градусы/сек) в режиме «выждать».</summary>
    public double WaitOuterSpeedDegPerSec { get; init; } = 40;

    /// <summary>Допуск совмещения (градусы) в режиме «выждать» — шире окно.</summary>
    public double WaitToleranceDeg { get; init; } = 14;

    /// <summary>Скорость вращения внутреннего кольца игроком (градусы/сек при зажатой стрелке).</summary>
    public double InnerRotateSpeedDegPerSec { get; init; } = 140;

    /// <summary>Сколько совмещений подряд нужно для завершения взлома.</summary>
    public int RequiredStreak { get; init; } = 3;

    /// <summary>Сколько промахов (нажатий не в момент совмещения) считается провалом — взлом всё
    /// равно происходит (сюжетно гарантирован), но аварийно.</summary>
    public int MaxErrorsBeforeFailure { get; init; } = 3;

    /// <summary>Общий лимит сессии (секунды) — страховка на случай, если игрок вообще не
    /// нажимает подтверждение; по истечении засчитывается провал (как и исчерпание попыток).</summary>
    public double MaxSessionSeconds { get; init; } = 45;
}
