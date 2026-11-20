using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using ZeroFloor.Common;
using ZeroFloor.Models;

namespace ZeroFloor.ViewModels;

public enum StealthCellKind
{
    Floor,
    Cover,
    Exit
}

/// <summary>Одна клетка сетки — координаты и вид фиксированы при построении, освещённость и
/// присутствие игрока/дрона меняются каждый тик/шаг (см. StealthGridViewModel).</summary>
public sealed class StealthCellViewModel : ViewModelBase
{
    private bool _isLit;
    private bool _hasPlayer;
    private bool _hasDrone;

    internal StealthCellViewModel(int row, int col, StealthCellKind kind)
    {
        Row = row;
        Col = col;
        Kind = kind;
    }

    public int Row { get; }
    public int Col { get; }
    public StealthCellKind Kind { get; }

    public bool IsLit
    {
        get => _isLit;
        internal set => SetProperty(ref _isLit, value);
    }

    public bool HasPlayer
    {
        get => _hasPlayer;
        internal set => SetProperty(ref _hasPlayer, value);
    }

    public bool HasDrone
    {
        get => _hasDrone;
        internal set => SetProperty(ref _hasDrone, value);
    }
}

/// <summary>
/// Мини-игра «Синхронизация слепых зон» (Глава 6, пользовательское дополнение к сценарию, по
/// тому же принципу, что WirePuzzleViewModel/TimingBarViewModel — сюжетный флоучарт не меняется
/// кодом, только данными сцены, см. DialogueNode.MinigameCue). Тактический grid-стелс: дроны
/// патрулируют по заданным маршрутам (StealthGridLayout.Drones), освещая конусом клетки перед
/// собой; игрок перемещается по клеткам, используя укрытия, чтобы блокировать конус, и должен
/// дойти до клетки выхода. Попадание в освещённую клетку — локальный Game Over с повтором той
/// же мини-игры с начала (как у WirePuzzle), без последствий для GameState.
/// </summary>
public sealed class StealthGridViewModel : ViewModelBase, IDisposable
{
    private const int TickIntervalMs = 100;

    private static readonly string[] GameOverMessages =
    {
        "СТАТУС: ОБЪЕКТ ЛИКВИДИРОВАН",
        "СТАТУС: ЗАДЕРЖАН И СТЁРТ ИЗ СПИСКОВ"
    };

    private readonly StealthGridLayout _layout;
    private readonly Action _onSuccess;
    private readonly Random _random = new();
    private readonly Dictionary<(int Row, int Col), StealthCellViewModel> _cellMap = new();
    private readonly List<DroneState> _drones = new();

    private DispatcherTimer? _timer;
    private (int Row, int Col) _player;
    private bool _isGameOver;
    private bool _isResolved;
    private bool _isGameOverVisible;
    private string _gameOverMessage = string.Empty;

    public StealthGridViewModel(StealthGridLayout layout, Action onSuccess)
    {
        _layout = layout;
        _onSuccess = onSuccess;

        Cells = new ObservableCollection<StealthCellViewModel>();

        MoveUpCommand = new RelayCommand(_ => Move(-1, 0));
        MoveDownCommand = new RelayCommand(_ => Move(1, 0));
        MoveLeftCommand = new RelayCommand(_ => Move(0, -1));
        MoveRightCommand = new RelayCommand(_ => Move(0, 1));
        RetryCommand = new RelayCommand(_ => Restart());

        Restart();
    }

    public int Rows => _layout.Rows;
    public int Cols => _layout.Cols;
    public ObservableCollection<StealthCellViewModel> Cells { get; }

    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand MoveLeftCommand { get; }
    public ICommand MoveRightCommand { get; }
    public ICommand RetryCommand { get; }

    public bool IsGameOverVisible
    {
        get => _isGameOverVisible;
        private set => SetProperty(ref _isGameOverVisible, value);
    }

    public string GameOverMessage
    {
        get => _gameOverMessage;
        private set => SetProperty(ref _gameOverMessage, value);
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
        _player = (_layout.Start.Row, _layout.Start.Col);

        Cells.Clear();
        _cellMap.Clear();

        var coverSet = new HashSet<(int Row, int Col)>(_layout.CoverCells.Select(c => (c.Row, c.Col)));

        for (var row = 0; row < Rows; row++)
        {
            for (var col = 0; col < Cols; col++)
            {
                var kind = (row, col) == (_layout.Exit.Row, _layout.Exit.Col) ? StealthCellKind.Exit
                    : coverSet.Contains((row, col)) ? StealthCellKind.Cover
                    : StealthCellKind.Floor;

                var cell = new StealthCellViewModel(row, col, kind);
                Cells.Add(cell);
                _cellMap[(row, col)] = cell;
            }
        }

        _drones.Clear();
        foreach (var route in _layout.Drones)
            _drones.Add(new DroneState(route));

        RecomputeLighting();
        UpdatePlayerMarker();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickIntervalMs) };
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

        var anyDroneMoved = false;
        foreach (var drone in _drones)
            if (drone.Advance(TickIntervalMs))
                anyDroneMoved = true;

        if (anyDroneMoved)
        {
            RecomputeLighting();
            CheckCaught();
        }
    }

    private void Move(int deltaRow, int deltaCol)
    {
        if (_isGameOver || _isResolved)
            return;

        var next = (Row: _player.Row + deltaRow, Col: _player.Col + deltaCol);
        if (next.Row < 0 || next.Row >= Rows || next.Col < 0 || next.Col >= Cols)
            return;

        _player = next;
        UpdatePlayerMarker();

        if (_player == (_layout.Exit.Row, _layout.Exit.Col))
        {
            _isResolved = true;
            _timer?.Stop();
            _onSuccess();
            return;
        }

        CheckCaught();
    }

    private void CheckCaught()
    {
        if (_isGameOver || _isResolved)
            return;

        if (!_cellMap[_player].IsLit)
            return;

        _isGameOver = true;
        _timer?.Stop();
        GameOverMessage = GameOverMessages[_random.Next(GameOverMessages.Length)];
        IsGameOverVisible = true;
    }

    private void UpdatePlayerMarker()
    {
        foreach (var cell in Cells)
            cell.HasPlayer = false;

        _cellMap[_player].HasPlayer = true;
    }

    private void RecomputeLighting()
    {
        foreach (var cell in Cells)
        {
            cell.IsLit = false;
            cell.HasDrone = false;
        }

        foreach (var drone in _drones)
        {
            if (_cellMap.TryGetValue(drone.Position, out var droneCell))
                droneCell.HasDrone = true;

            CastCone(drone);
        }
    }

    /// <summary>Конус обзора дрона: прямой луч длиной ConeLength клеток по направлению движения,
    /// с 1 клеткой расширения по бокам начиная со 2-го шага (подобие расширяющегося конуса).
    /// Обрывается на границе сетки или на клетке укрытия — дальше луч не проходит.</summary>
    private void CastCone(DroneState drone)
    {
        var (deltaRow, deltaCol) = drone.Facing;
        var (row, col) = drone.Position;

        for (var step = 1; step <= drone.ConeLength; step++)
        {
            var targetRow = row + deltaRow * step;
            var targetCol = col + deltaCol * step;

            if (!TryLight(targetRow, targetCol))
                break;

            if (step >= 2)
            {
                TryLight(targetRow + deltaCol, targetCol + deltaRow);
                TryLight(targetRow - deltaCol, targetCol - deltaRow);
            }
        }
    }

    private bool TryLight(int row, int col)
    {
        if (!_cellMap.TryGetValue((row, col), out var cell) || cell.Kind == StealthCellKind.Cover)
            return false;

        cell.IsLit = true;
        return true;
    }

    /// <summary>Внутреннее состояние патруля одного дрона — не ViewModel (ничего не биндится
    /// напрямую): дрон рисуется через StealthCellViewModel.HasDrone клетки его текущей позиции.</summary>
    private sealed class DroneState
    {
        private readonly List<(int Row, int Col)> _waypoints;
        private int _index;
        private int _direction = 1;
        private double _elapsedMs;

        public DroneState(DroneRoute route)
        {
            _waypoints = route.Waypoints.Select(w => (w.Row, w.Col)).ToList();
            ConeLength = Math.Max(1, route.ConeLength);
            StepIntervalMs = Math.Max(50, route.StepIntervalMs);
            Facing = ComputeFacing(_waypoints[0], _waypoints[Math.Min(1, _waypoints.Count - 1)]);
        }

        public int ConeLength { get; }
        public int StepIntervalMs { get; }
        public (int Row, int Col) Position => _waypoints[_index];
        public (int Row, int Col) Facing { get; private set; }

        /// <summary>Продвигает патруль на TickIntervalMs; возвращает true, если дрон в этот тик
        /// действительно переместился на новую точку маршрута (конус пересчитывать не нужно,
        /// если никто не двигался).</summary>
        public bool Advance(double deltaMs)
        {
            _elapsedMs += deltaMs;
            if (_elapsedMs < StepIntervalMs)
                return false;

            _elapsedMs = 0;

            if (_index + _direction < 0 || _index + _direction >= _waypoints.Count)
                _direction = -_direction;

            var previous = _waypoints[_index];
            _index += _direction;
            Facing = ComputeFacing(previous, _waypoints[_index]);
            return true;
        }

        private static (int Row, int Col) ComputeFacing((int Row, int Col) from, (int Row, int Col) to) =>
            (Math.Sign(to.Row - from.Row), Math.Sign(to.Col - from.Col)) is (0, 0) ? (0, 1) : (Math.Sign(to.Row - from.Row), Math.Sign(to.Col - from.Col));
    }
}
