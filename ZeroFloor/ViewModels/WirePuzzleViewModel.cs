using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using ZeroFloor.Common;

namespace ZeroFloor.ViewModels;

public enum PipeShape
{
    Straight,
    Elbow
}

/// <summary>Стороны плитки по часовой стрелке, начиная с севера — чистая механика, не связана с WPF.</summary>
internal enum PipeDirection
{
    North = 0,
    East = 1,
    South = 2,
    West = 3
}

/// <summary>
/// Одна плитка сетки «взлома панели» (Глава 3): фигура (прямая/уголок) фиксирована при
/// генерации, поворот меняется кликом игрока. Открытые стороны считаются от базового
/// положения фигуры (прямая: север-юг; уголок: север-восток), повёрнутого на RotationSteps
/// шагов по 90° — ровно так же, как визуально поворачивается сам элемент в разметке.
/// </summary>
public sealed class PipeTileViewModel : ViewModelBase
{
    private static readonly PipeDirection[] StraightBaseSides = { PipeDirection.North, PipeDirection.South };
    private static readonly PipeDirection[] ElbowBaseSides = { PipeDirection.North, PipeDirection.East };

    private int _rotationSteps;

    internal PipeTileViewModel(PipeShape shape, int row, int col, int initialRotationSteps, Action<PipeTileViewModel> onClicked)
    {
        Shape = shape;
        Row = row;
        Col = col;
        _rotationSteps = ((initialRotationSteps % 4) + 4) % 4;
        RotateCommand = new RelayCommand(_ => onClicked(this));
    }

    public PipeShape Shape { get; }
    public int Row { get; }
    public int Col { get; }

    public int RotationSteps
    {
        get => _rotationSteps;
        private set
        {
            if (SetProperty(ref _rotationSteps, value))
                OnPropertyChanged(nameof(RotationAngle));
        }
    }

    /// <summary>Угол для RotateTransform в разметке — геометрия плитки рисуется в базовом
    /// положении (0°), поворот применяется только визуально, поэтому открытые стороны
    /// (ComputeOpenSides) и картинка всегда синхронны без отдельной перерисовки.</summary>
    public double RotationAngle => RotationSteps * 90.0;

    public ICommand RotateCommand { get; }

    internal void Rotate() => RotationSteps = (RotationSteps + 1) % 4;

    internal IReadOnlyList<PipeDirection> OpenSides => ComputeOpenSides(Shape, RotationSteps);

    internal static IReadOnlyList<PipeDirection> ComputeOpenSides(PipeShape shape, int rotationSteps)
    {
        var baseSides = shape == PipeShape.Straight ? StraightBaseSides : ElbowBaseSides;
        return baseSides.Select(d => Rotate(d, rotationSteps)).ToList();
    }

    internal static PipeDirection Rotate(PipeDirection direction, int steps) => (PipeDirection)(((int)direction + steps) % 4);

    internal static PipeDirection Opposite(PipeDirection direction) => (PipeDirection)(((int)direction + 2) % 4);
}

/// <summary>
/// Мини-игра «Взлом лифтового узла» (Глава 3, пользовательское дополнение к сценарию —
/// см. обсуждение с автором: сюжетный флоучарт не меняется, провал — только локальный
/// повтор этой же мини-игры, без последствий для GameState/флагов). Сетка 3×N плиток:
/// игрок поворотами плиток должен замкнуть цепь от разъёма «вход» (левый край, строка
/// StartRow) до разъёма «выход» (правый край, строка EndRow) до истечения времени —
/// визуально нарастающая «шкала патруля». Решаемость гарантирована генерацией: сперва
/// строится случайный путь вход→выход и для его клеток подбирается фигура, подходящая
/// под направления входа/выхода; начальный поворот всех плиток (путевых и шумовых)
/// затем рандомизируется, так что игроку всегда есть что докручивать.
/// </summary>
public sealed class WirePuzzleViewModel : ViewModelBase, IDisposable
{
    private const int Rows = 3;

    private readonly int _cols;
    private readonly int _timeLimitSeconds;
    private readonly Action _onSuccess;
    private readonly Random _random = new();

    private DispatcherTimer? _timer;
    private double _elapsedSeconds;
    private bool _isGameOver;
    private bool _isResolved;
    private bool _isGameOverVisible;
    private double _progress;

    public WirePuzzleViewModel(int gridSize, int timeLimitSeconds, Action onSuccess)
    {
        _cols = Math.Max(3, gridSize);
        _timeLimitSeconds = Math.Max(3, timeLimitSeconds);
        _onSuccess = onSuccess;
        StartRow = Rows / 2;
        EndRow = Rows / 2;

        RetryCommand = new RelayCommand(_ => Restart());
        Tiles = new ObservableCollection<PipeTileViewModel>();
        Restart();
    }

    public int Cols => _cols;
    public int StartRow { get; }
    public int EndRow { get; }
    public ObservableCollection<PipeTileViewModel> Tiles { get; }
    public ICommand RetryCommand { get; }

    public bool IsGameOverVisible
    {
        get => _isGameOverVisible;
        private set => SetProperty(ref _isGameOverVisible, value);
    }

    /// <summary>0..1 — заполнение шкалы приближающегося патруля.</summary>
    public double Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    public void Restart()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
        }

        _isGameOver = false;
        _isResolved = false;
        IsGameOverVisible = false;
        _elapsedSeconds = 0;
        Progress = 0;

        Tiles.Clear();
        foreach (var tile in GenerateGrid())
            Tiles.Add(tile);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void Dispose()
    {
        if (_timer is null)
            return;

        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_isGameOver || _isResolved)
            return;

        _elapsedSeconds += 0.1;
        Progress = Math.Min(1.0, _elapsedSeconds / _timeLimitSeconds);

        if (_elapsedSeconds >= _timeLimitSeconds)
        {
            _isGameOver = true;
            _timer?.Stop();
            IsGameOverVisible = true;
        }
    }

    private void OnTileClicked(PipeTileViewModel tile)
    {
        if (_isGameOver || _isResolved)
            return;

        tile.Rotate();

        if (!IsSolved())
            return;

        _isResolved = true;
        _timer?.Stop();
        _onSuccess();
    }

    private IEnumerable<PipeTileViewModel> GenerateGrid()
    {
        var shapeAt = new PipeShape?[Rows, _cols];

        var path = new List<(int Row, int Col)> { (StartRow, 0) };
        var (curRow, curCol) = (StartRow, 0);

        while (curCol < _cols - 1 || curRow != EndRow)
        {
            var options = new List<(int dr, int dc)>();
            if (curCol < _cols - 1) options.Add((0, 1));
            if (curRow < EndRow) options.Add((1, 0));
            if (curRow > EndRow) options.Add((-1, 0));

            var (dr, dc) = options[_random.Next(options.Count)];
            curRow += dr;
            curCol += dc;
            path.Add((curRow, curCol));
        }

        for (var i = 0; i < path.Count; i++)
        {
            var (r, c) = path[i];

            var entry = i == 0
                ? PipeDirection.West
                : PipeTileViewModel.Opposite(DirectionTo(path[i - 1], path[i]));

            var exit = i == path.Count - 1
                ? PipeDirection.East
                : DirectionTo(path[i], path[i + 1]);

            shapeAt[r, c] = ResolveShape(entry, exit);
        }

        for (var r = 0; r < Rows; r++)
            for (var c = 0; c < _cols; c++)
                shapeAt[r, c] ??= _random.Next(2) == 0 ? PipeShape.Straight : PipeShape.Elbow;

        var result = new List<PipeTileViewModel>();
        for (var r = 0; r < Rows; r++)
            for (var c = 0; c < _cols; c++)
                result.Add(new PipeTileViewModel(shapeAt[r, c]!.Value, r, c, _random.Next(4), OnTileClicked));

        return result;
    }

    /// <summary>Фигура (прямая для противоположных сторон, уголок для соседних) — её поворот
    /// намеренно не фиксируется здесь: он рандомизируется в GenerateGrid, игрок находит его сам.</summary>
    private static PipeShape ResolveShape(PipeDirection entry, PipeDirection exit) =>
        PipeTileViewModel.Opposite(entry) == exit ? PipeShape.Straight : PipeShape.Elbow;

    private static PipeDirection DirectionTo((int Row, int Col) from, (int Row, int Col) to)
    {
        if (to.Col > from.Col) return PipeDirection.East;
        if (to.Row > from.Row) return PipeDirection.South;
        return PipeDirection.North;
    }

    private bool IsSolved()
    {
        var map = Tiles.ToDictionary(t => (t.Row, t.Col));

        if (!map.TryGetValue((StartRow, 0), out var startTile) || !startTile.OpenSides.Contains(PipeDirection.West))
            return false;

        var visited = new HashSet<(int, int)> { (StartRow, 0) };
        var stack = new Stack<(int, int)>();
        stack.Push((StartRow, 0));

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            var tile = map[current];

            foreach (var direction in tile.OpenSides)
            {
                var next = Step(current, direction);
                if (next.Row < 0 || next.Row >= Rows || next.Col < 0 || next.Col >= _cols)
                    continue;
                if (visited.Contains(next) || !map.TryGetValue(next, out var neighbor))
                    continue;
                if (!neighbor.OpenSides.Contains(PipeTileViewModel.Opposite(direction)))
                    continue;

                visited.Add(next);
                stack.Push(next);
            }
        }

        var exitCell = (EndRow, _cols - 1);
        return visited.Contains(exitCell) && map[exitCell].OpenSides.Contains(PipeDirection.East);
    }

    private static (int Row, int Col) Step((int Row, int Col) from, PipeDirection direction) => direction switch
    {
        PipeDirection.North => (from.Row - 1, from.Col),
        PipeDirection.South => (from.Row + 1, from.Col),
        PipeDirection.East => (from.Row, from.Col + 1),
        PipeDirection.West => (from.Row, from.Col - 1),
        _ => from
    };
}
