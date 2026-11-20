using System.Windows.Input;
using System.Windows.Threading;
using ZeroFloor.Common;

namespace ZeroFloor.ViewModels;

/// <summary>
/// Мини-игра «Кража обходчика» (Глава 2, пользовательское дополнение к сценарию, по тому же
/// принципу, что и WirePuzzleViewModel в Главе 3 — сюжетный флоучарт не меняется кодом, только
/// данными сцены, см. DialogueNode.MinigameCue). Полоса внимания Гоши: маркер непрерывно бегает
/// туда-сюда по полосе, игрок нажимает кнопку один раз, стараясь поймать его в зелёной зоне.
/// В отличие от WirePuzzle, здесь у провала есть реальные сюжетные последствия — решает
/// их не эта ViewModel (она только сообщает успех/провал через callback), а узел сценария
/// через MinigameCue.FailureNodeId (см. NovelViewModel.RefreshFromCurrentNode).
/// </summary>
public sealed class TimingBarViewModel : ViewModelBase, IDisposable
{
    // Вся геометрия полосы считается в пикселях одного и того же холста, на который завязана
    // разметка TimingBarOverlayControl.xaml (TrackWidth там задан тем же числом) — то же
    // намеренное дублирование константы между VM и XAML, что и у WirePuzzle (Rows/TileSize).
    private const double TrackWidth = 700;
    private const double MarkerWidth = 10;
    private const double YellowZoneWidth = 60;
    private const double TickIntervalMs = 16;

    private readonly Action _onSuccess;
    private readonly Action _onFailure;
    private readonly double _speedPxPerMs;

    private DispatcherTimer? _timer;
    private double _markerX;
    private int _direction = 1;
    private bool _isResolved;

    public TimingBarViewModel(double greenZoneWidthFraction, double sweepSeconds, Action onSuccess, Action onFailure)
    {
        _onSuccess = onSuccess;
        _onFailure = onFailure;

        var greenWidth = Math.Clamp(greenZoneWidthFraction, 0.05, 0.6) * TrackWidth;
        GreenZoneWidth = greenWidth;
        GreenZoneLeft = (TrackWidth - greenWidth) / 2;
        YellowLeftZoneLeft = GreenZoneLeft - YellowZoneWidth;
        YellowRightZoneLeft = GreenZoneLeft + greenWidth;

        var sweepMs = Math.Max(0.3, sweepSeconds) * 1000;
        _speedPxPerMs = TrackWidth / sweepMs;

        PressCommand = new RelayCommand(_ => Press());

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickIntervalMs) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public double GreenZoneLeft { get; }
    public double GreenZoneWidth { get; }
    public double YellowLeftZoneLeft { get; }
    public double YellowRightZoneLeft { get; }
    public double YellowZoneWidthValue => YellowZoneWidth;
    public double MarkerWidthValue => MarkerWidth;

    public double MarkerX
    {
        get => _markerX;
        private set => SetProperty(ref _markerX, value);
    }

    public ICommand PressCommand { get; }

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
        if (_isResolved)
            return;

        var next = _markerX + _direction * _speedPxPerMs * TickIntervalMs;

        if (next <= 0)
        {
            next = 0;
            _direction = 1;
        }
        else if (next >= TrackWidth - MarkerWidth)
        {
            next = TrackWidth - MarkerWidth;
            _direction = -1;
        }

        MarkerX = next;
    }

    private void Press()
    {
        if (_isResolved)
            return;

        _isResolved = true;
        _timer?.Stop();

        var markerCenter = MarkerX + MarkerWidth / 2;
        if (markerCenter >= GreenZoneLeft && markerCenter <= GreenZoneLeft + GreenZoneWidth)
            _onSuccess();
        else
            _onFailure();
    }
}
