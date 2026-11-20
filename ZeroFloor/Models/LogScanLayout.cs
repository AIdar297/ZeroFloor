namespace ZeroFloor.Models;

/// <summary>
/// Данные мини-игры «Скан лога панели» (Глава 3, MinigameKind.LogScan) — чистая сценарная
/// конфигурация, читается только Presentation-слоем (LogScanViewModel), см. комментарий
/// MinigameCue. Лог прокручивается строка за строкой с интервалом LineIntervalSeconds; строка
/// TargetLine появляется в потоке ровно дважды — первое появление (окно реакции №1) и, если
/// игрок не успел нажать подтверждение, второе (окно №2). Не успел и во второй раз — лента
/// автоматически останавливается на этой строке (FAILURE), не прокручиваясь дальше.
/// </summary>
public sealed class LogScanLayout
{
    /// <summary>Строки-"шум" для заполнения ленты до/между окнами реакции — зацикливаются, если
    /// нужно больше строк, чем задано здесь.</summary>
    public IReadOnlyList<string> FillerLines { get; init; } = Array.Empty<string>();

    public string TargetLine { get; init; } = string.Empty;

    /// <summary>Сколько строк-"шума" прокручивается перед первым появлением TargetLine.</summary>
    public int LinesBeforeFirstWindow { get; init; } = 4;

    /// <summary>Сколько строк-"шума" прокручивается между первым и вторым появлением TargetLine.</summary>
    public int LinesBetweenWindows { get; init; } = 3;

    /// <summary>Секунды на одну строку — одновременно и скорость прокрутки, и длительность
    /// каждого из двух окон реакции.</summary>
    public double LineIntervalSeconds { get; init; } = 1.4;

    /// <summary>Сколько строк одновременно видно в ленте (старые уходят сверху).</summary>
    public int VisibleLineCount { get; init; } = 7;
}
