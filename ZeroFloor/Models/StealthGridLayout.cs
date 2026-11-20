namespace ZeroFloor.Models;

/// <summary>Одна клетка сетки по координатам (строка, столбец), нумерация с 0 — общий тип для
/// всех ссылок на клетки внутри StealthGridLayout (старт, выход, укрытия, маршруты дронов).</summary>
public readonly record struct GridCell(int Row, int Col);

/// <summary>Маршрут патруля одного дрона: список клеток, которые дрон проходит по очереди,
/// затем возвращается в обратном порядке (маятник) — без отдельной логики разворота в данных,
/// только последовательность точек. Направление обзора на каждом шаге вычисляется как
/// направление движения к этой точке (см. StealthGridViewModel), а не задаётся отдельно.</summary>
public sealed class DroneRoute
{
    public IReadOnlyList<GridCell> Waypoints { get; init; } = Array.Empty<GridCell>();

    /// <summary>Длина конуса обзора в клетках по направлению движения; конус обрывается раньше
    /// при выходе за границы сетки или при попадании в клетку укрытия (Cover).</summary>
    public int ConeLength { get; init; } = 3;

    /// <summary>Миллисекунды между шагами патруля — чем меньше, тем быстрее и опаснее дрон.</summary>
    public int StepIntervalMs { get; init; } = 500;
}

/// <summary>
/// Данные мини-игры «Синхронизация слепых зон» (Глава 6, MinigameKind.StealthGrid) — чистая
/// сценарная конфигурация сетки, читается только Presentation-слоем (StealthGridViewModel),
/// см. комментарий MinigameCue. Размер и расстановка заданы контентом главы, а не кодом.
/// </summary>
public sealed class StealthGridLayout
{
    public required int Rows { get; init; }
    public required int Cols { get; init; }
    public required GridCell Start { get; init; }
    public required GridCell Exit { get; init; }

    /// <summary>Клетки-укрытия (ящики/трубы) — блокируют конус дрона (он не освещает клетку
    /// укрытия и не продолжается дальше неё) и сами никогда не считаются освещёнными.</summary>
    public IReadOnlyList<GridCell> CoverCells { get; init; } = Array.Empty<GridCell>();

    public IReadOnlyList<DroneRoute> Drones { get; init; } = Array.Empty<DroneRoute>();
}
