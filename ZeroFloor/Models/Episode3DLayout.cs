namespace ZeroFloor.Models;

/// <summary>
/// Сценарные данные интерактивного 3D-эпизода (MinigameKind.MarketStealth3D — Глава 2А
/// «Ярмарка теней», MinigameKind.ShaftsChase3D — Глава 6 «Шахты»). Как и остальные части
/// MinigameCue — чистые данные: геометрию локации, модели и правила движения строит
/// Presentation-слой (Services/Presentation/World3D), а здесь лежит только то, что связывает
/// эпизод со сценарным графом:
/// - какие маршруты по локации соответствуют каким выборам ЭТОГО ЖЕ узла (выбор применяется
///   обычным ChoiceProcessor — эффекты/идемпотентность/история не дублируются);
/// - какие эффекты даёт каждый исход эпизода: setFlag (любой флаг) и addStat в пределах ±10
///   (решение автора; проверяют ScenarioValidator и GameSessionController.ApplyMinigameOutcomeEffects);
/// - на какой узел той же сцены ведёт исход, если он требует отдельной реплики (напр. тревога).
/// </summary>
public sealed class Episode3DLayout
{
    /// <summary>Ключ маршрута (напр. "detour"/"direct" или "residential"/"ventilation") → choiceId
    /// узла, несущего этот MinigameCue. Игрок выбирает маршрут физически, движок получает обычный
    /// SelectChoice.</summary>
    public IReadOnlyDictionary<string, string> RouteChoiceIds { get; init; } = new Dictionary<string, string>();

    /// <summary>true — выбор маршрута применяется в момент входа в проход (Глава 6, Выбор 6);
    /// false — по завершении эпизода (Глава 2А: маршрут обхода дрона — флейвор без эффектов,
    /// определяется по тому, какой частью рынка прошёл игрок).</summary>
    public bool CommitRouteOnEntry { get; init; }

    /// <summary>Ключ исхода (напр. "CLEAN"/"DETECTED", "PERFECT"/"NORMAL"/"ROUGH") → эффекты,
    /// применяемые при этом исходе: setFlag и addStat в пределах ±10 (см. валидатор).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Effect>> OutcomeEffects { get; init; } =
        new Dictionary<string, IReadOnlyList<Effect>>();

    /// <summary>Ключ исхода → узел ТОЙ ЖЕ сцены, на который эпизод переходит вместо выбора
    /// маршрута (напр. "DETECTED" → напряжённый вариант выхода к Гоше). Необязательно.</summary>
    public IReadOnlyDictionary<string, string> OutcomeNodeIds { get; init; } = new Dictionary<string, string>();
}
