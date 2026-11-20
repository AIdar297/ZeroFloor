namespace ZeroFloor.Models;

/// <summary>
/// Режиссёрская ремарка узла (как CharacterCue/CameraCue/ScreenFadeCue): у текущего узла с
/// выбором есть ограниченное время на ответ — если игрок не успевает выбрать сам, по истечении
/// TimeoutSeconds автоматически применяется выбор с идентификатором TimeoutChoiceId, ровно так
/// же, как если бы игрок кликнул по нему сам (эффекты/история/переход — без разницы). Engine
/// эту ремарку не читает — таймер и автовыбор полностью на Presentation-слое (NovelViewModel),
/// Engine в принципе не знает о времени. TimeoutChoiceId обязан быть одним из choices ТЕКУЩЕГО
/// узла (см. ScenarioValidator) — сценарист указывает заранее заданный, нейтрально-нервный
/// ответ, а не случайный.
/// </summary>
public sealed class ChoiceTimerCue
{
    public required double TimeoutSeconds { get; init; }
    public required string TimeoutChoiceId { get; init; }
}
