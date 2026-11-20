namespace ZeroFloor.Models;

/// <summary>Одна пара мини-игры «Сборка разрушенного кода»: чистый (CleanText) фрагмент,
/// который игрок переносит в слот, и то, что показано на самом слоте (CorruptedHint) —
/// визуально узнаваемый, но частично испорченный вариант того же текста, по которому игрок
/// должен опознать нужный фрагмент.</summary>
public sealed class CodeFragmentPair
{
    public required string CleanText { get; init; }
    public required string CorruptedHint { get; init; }
}

/// <summary>
/// Данные мини-игры «Сборка разрушенного кода» (Глава 8, MinigameKind.CodeAssembly) — чистая
/// сценарная конфигурация, читается только Presentation-слоем (CodeAssemblyViewModel), см.
/// комментарий MinigameCue.
/// </summary>
public sealed class CodeAssemblyLayout
{
    public IReadOnlyList<CodeFragmentPair> Pairs { get; init; } = Array.Empty<CodeFragmentPair>();

    /// <summary>Секунды на всю мини-игру — по истечении, если не все пары собраны, провал.</summary>
    public int TimeLimitSeconds { get; init; } = 45;

    /// <summary>Сколько неверных попыток переноса допускается, прежде чем засчитать провал
    /// (попадание именно в этот лимит — провал, то есть лимит "включительно").</summary>
    public int MaxErrors { get; init; } = 3;

    /// <summary>Флаг, при котором действует «жёсткий режим» (напр. mg_ch8_archive_alert: после
    /// тревоги в архиве защитный протокол уже насторожен). Null — режим не используется.</summary>
    public string? HardModeFlag { get; init; }

    public int HardTimeLimitSeconds { get; init; } = 30;

    public int HardMaxErrors { get; init; } = 2;

    /// <summary>Ложные фрагменты: правдоподобные, но неверные варианты среди восстановленных
    /// (перестановки слов/цифр из настоящих строк). Положить такой в любую строку — ошибка.</summary>
    public IReadOnlyList<string> Decoys { get; init; } = Array.Empty<string>();

    /// <summary>Каждые N секунд защитный протокол гасит ещё один символ в случайной
    /// невосстановленной строке (0 — распада нет). Строка не гаснет больше чем на MaxDecayFraction.</summary>
    public double DecaySeconds { get; init; }

    public double MaxDecayFraction { get; init; } = 0.6;

    /// <summary>Штраф времени (с) за неверно положенный фрагмент — в дополнение к счётчику ошибок.</summary>
    public double ErrorTimePenalty { get; init; }

    /// <summary>Каждые N секунд пул фрагментов перемешивается (0 — не перемешивается).</summary>
    public double ShuffleSeconds { get; init; }

    /// <summary>Два верных попадания подряд не дольше ComboWindowSeconds друг от друга дают
    /// +ComboBonusSeconds ко времени (0 — без серий).</summary>
    public double ComboWindowSeconds { get; init; }

    public double ComboBonusSeconds { get; init; }
}
