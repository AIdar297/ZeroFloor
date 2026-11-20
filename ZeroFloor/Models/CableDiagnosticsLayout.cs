namespace ZeroFloor.Models;

/// <summary>
/// Данные мини-игры «Диагностика троса» (Глава 1, MinigameKind.CableDiagnostics) — чистая
/// сценарная конфигурация, читается только Presentation-слоем (CableDiagnosticsViewModel), см.
/// комментарий MinigameCue. Натяжение троса — значение 0..1: стрелка сама "сползает" вниз
/// (DecayRate) и дрожит (JitterAmplitude), игрок удерживает кнопку, чтобы поднимать натяжение
/// (PumpRate), стараясь удержать стрелку в зелёной зоне [GreenMin, GreenMax] RequiredStableSeconds
/// подряд.
/// </summary>
public sealed class CableDiagnosticsLayout
{
    /// <summary>Начальное натяжение (0..1) — трос уже провисает, ниже зелёной зоны.</summary>
    public double StartTension { get; init; } = 0.25;

    public double GreenMin { get; init; } = 0.45;
    public double GreenMax { get; init; } = 0.65;

    /// <summary>Ширина жёлтой зоны по обе стороны от зелёной (чисто визуальная подсказка —
    /// дальше неё, до края шкалы, красная зона).</summary>
    public double YellowMargin { get; init; } = 0.15;

    /// <summary>Натяжение, теряемое в секунду, пока кнопка не зажата (трос слабеет сам по себе).</summary>
    public double DecayRate { get; init; } = 0.12;

    /// <summary>Натяжение, добавляемое в секунду поверх DecayRate, пока зажата кнопка (насос).</summary>
    public double PumpRate { get; init; } = 0.30;

    /// <summary>Амплитуда случайного дрожания стрелки в секунду — трос "дрожит", даже если
    /// игрок держит кнопку идеально ровно.</summary>
    public double JitterAmplitude { get; init; } = 0.05;

    /// <summary>Сколько секунд подряд нужно удержать стрелку в зелёной зоне для SUCCESS.</summary>
    public double RequiredStableSeconds { get; init; } = 6.0;

    /// <summary>Общий лимит сессии (секунды) — одна попытка, без повтора; если за это время не
    /// набралось RequiredStableSeconds подряд, исход решает PartialThresholdFraction.</summary>
    public double MaxSessionSeconds { get; init; } = 18.0;

    /// <summary>Доля (0..1) общего времени сессии, проведённого в зелёной зоне, ниже которой
    /// по истечении MaxSessionSeconds засчитывается FAILURE, а не PARTIAL.</summary>
    public double PartialThresholdFraction { get; init; } = 0.4;
}
