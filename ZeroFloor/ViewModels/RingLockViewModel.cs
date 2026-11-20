using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using ZeroFloor.Common;
using ZeroFloor.Models;

namespace ZeroFloor.ViewModels;

/// <summary>Режим сложности, выбираемый игроком прямо в мини-игре (не отдельным сюжетным
/// выбором) — «рискнуть» (быстрее, уже окно) или «выждать» (медленнее, шире окно).</summary>
public enum RingLockMode
{
    Risk,
    Wait
}

public enum RingLockOutcome
{
    Success,
    Partial,
    Failure
}

/// <summary>Одна насечка кольца — её экранные координаты и угол поворота пересчитываются
/// каждый тик (см. RingLockViewModel.UpdateRing), IsNotch фиксирован при создании.</summary>
public sealed class RingTabViewModel : ViewModelBase
{
    private double _x;
    private double _y;
    private double _angle;

    internal RingTabViewModel(bool isNotch) => IsNotch = isNotch;

    public bool IsNotch { get; }

    public double X
    {
        get => _x;
        internal set => SetProperty(ref _x, value);
    }

    public double Y
    {
        get => _y;
        internal set => SetProperty(ref _y, value);
    }

    public double Angle
    {
        get => _angle;
        internal set => SetProperty(ref _angle, value);
    }
}

/// <summary>
/// Мини-игра «Калибровка обходчика» (Глава 3, пользовательское дополнение к сценарию, по тому
/// же принципу, что остальные мини-игры — сюжетный флоучарт не меняется кодом, только данными
/// сцены, см. DialogueNode.MinigameCue). Два кольца с насечками: внешнее крутится автономно,
/// внутреннее — игрок (стрелки/кнопки), у каждого кольца ровно один "паз"; когда оба паза
/// одновременно оказываются в пределах допуска от контрольной точки (верх циферблата, угол 0),
/// это окно совмещения — подтверждение (Пробел/кнопка) в этот момент засчитывает совмещение.
/// 3 совмещения подряд — взлом готов; взлом сюжетно гарантирован при любом исходе (см.
/// MinigameCue.FailureNodeId/PartialNodeId) — провал лишь меняет, насколько аварийно он прошёл.
/// </summary>
public sealed class RingLockViewModel : ViewModelBase, IDisposable
{
    // Геометрия в пикселях холста — то же намеренное дублирование константы между VM и XAML,
    // что и у других мини-игр (TrackWidth/TrackHeight там же): RingLockOverlayControl.xaml
    // использует тот же холст 640x640 с центром в (320,320).
    private const double CenterX = 320;
    private const double CenterY = 320;
    private const double OuterRadius = 230;
    private const double InnerRadius = 150;
    private const double TabHalfSize = 10;
    private const double TargetMarkerRadius = OuterRadius + 35;
    private const double TargetMarkerHalfSize = 11;
    private const int TickIntervalMs = 16;

    private readonly RingLockLayout _layout;
    private readonly Action<RingLockOutcome> _onResolved;
    private readonly Random _random = new();

    private DispatcherTimer? _timer;
    private RingLockMode? _mode;
    private double _outerAngle;
    private double _innerAngle;
    private double _targetAngle;
    private double _targetHoldElapsed;
    private int _rotateDirection;
    private int _streak;
    private int _errors;
    private double _elapsedSeconds;
    private bool _isResolved;

    public RingLockViewModel(RingLockLayout layout, Action<RingLockOutcome> onResolved)
    {
        _layout = layout;
        _onResolved = onResolved;
        _targetAngle = _random.NextDouble() * 360;

        OuterTabs = new ObservableCollection<RingTabViewModel>();
        InnerTabs = new ObservableCollection<RingTabViewModel>();
        BuildTabs(OuterTabs, layout.OuterTabCount);
        BuildTabs(InnerTabs, layout.InnerTabCount);
        UpdateRing(OuterTabs, OuterRadius, _outerAngle);
        UpdateRing(InnerTabs, InnerRadius, _innerAngle);

        SelectRiskCommand = new RelayCommand(_ => SelectMode(RingLockMode.Risk));
        SelectWaitCommand = new RelayCommand(_ => SelectMode(RingLockMode.Wait));
        ConfirmCommand = new RelayCommand(_ => Confirm());

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickIntervalMs) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public ObservableCollection<RingTabViewModel> OuterTabs { get; }
    public ObservableCollection<RingTabViewModel> InnerTabs { get; }

    public bool IsModeSelected => _mode is not null;
    public int Streak => _streak;
    public int RequiredStreak => _layout.RequiredStreak;

    /// <summary>Контрольная точка — не фиксирована сверху: каждые Layout.TargetHoldSeconds
    /// перескакивает на новый случайный угол (см. OnTick), чтобы нельзя было просто смотреть
    /// в одно место на циферблате всю мини-игру. Пиксельные координаты для прямого биндинга
    /// маркера в XAML, та же логика, что у насечек (UpdateRing).</summary>
    public double TargetMarkerX => CenterX + TargetMarkerRadius * Math.Sin(_targetAngle * Math.PI / 180.0) - TargetMarkerHalfSize;
    public double TargetMarkerY => CenterY - TargetMarkerRadius * Math.Cos(_targetAngle * Math.PI / 180.0) - TargetMarkerHalfSize;

    /// <summary>0..1 — насколько близко патруль (заполнение шкалы "дистанция до обходчика").</summary>
    public double PatrolProximity => Math.Clamp((double)_errors / _layout.MaxErrorsBeforeFailure, 0, 1);

    public ICommand SelectRiskCommand { get; }
    public ICommand SelectWaitCommand { get; }
    public ICommand ConfirmCommand { get; }

    public void StartRotatingLeft() => _rotateDirection = -1;
    public void StartRotatingRight() => _rotateDirection = 1;
    public void StopRotating() => _rotateDirection = 0;

    public void Dispose()
    {
        if (_timer is null)
            return;

        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }

    private void SelectMode(RingLockMode mode)
    {
        if (_mode is not null || _isResolved)
            return;

        _mode = mode;
        OnPropertyChanged(nameof(IsModeSelected));
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_isResolved || _mode is null)
            return;

        const double dt = TickIntervalMs / 1000.0;
        _elapsedSeconds += dt;

        var outerSpeed = _mode == RingLockMode.Risk ? _layout.RiskOuterSpeedDegPerSec : _layout.WaitOuterSpeedDegPerSec;
        _outerAngle = NormalizeAngle(_outerAngle + outerSpeed * dt);
        _innerAngle = NormalizeAngle(_innerAngle + _rotateDirection * _layout.InnerRotateSpeedDegPerSec * dt);

        UpdateRing(OuterTabs, OuterRadius, _outerAngle);
        UpdateRing(InnerTabs, InnerRadius, _innerAngle);

        _targetHoldElapsed += dt;
        if (_targetHoldElapsed >= _layout.TargetHoldSeconds)
        {
            _targetHoldElapsed = 0;
            _targetAngle = _random.NextDouble() * 360;
            OnPropertyChanged(nameof(TargetMarkerX));
            OnPropertyChanged(nameof(TargetMarkerY));
        }

        if (_elapsedSeconds >= _layout.MaxSessionSeconds)
            Resolve(RingLockOutcome.Failure);
    }

    private void Confirm()
    {
        if (_isResolved || _mode is null)
            return;

        var tolerance = _mode == RingLockMode.Risk ? _layout.RiskToleranceDeg : _layout.WaitToleranceDeg;
        var aligned = AngleDistance(_outerAngle, _targetAngle) <= tolerance && AngleDistance(_innerAngle, _targetAngle) <= tolerance;

        if (aligned)
        {
            _streak++;
            OnPropertyChanged(nameof(Streak));

            if (_streak >= _layout.RequiredStreak)
                Resolve(_errors == 0 ? RingLockOutcome.Success : RingLockOutcome.Partial);

            return;
        }

        _streak = 0;
        _errors++;
        OnPropertyChanged(nameof(Streak));
        OnPropertyChanged(nameof(PatrolProximity));

        if (_errors >= _layout.MaxErrorsBeforeFailure)
            Resolve(RingLockOutcome.Failure);
    }

    private void Resolve(RingLockOutcome outcome)
    {
        if (_isResolved)
            return;

        _isResolved = true;
        _timer?.Stop();
        _onResolved(outcome);
    }

    private static void BuildTabs(ObservableCollection<RingTabViewModel> tabs, int count)
    {
        for (var i = 0; i < count; i++)
            tabs.Add(new RingTabViewModel(isNotch: i == 0));
    }

    /// <summary>Насечка с индексом 0 всегда определяет абсолютный угол "паза" кольца — ровно
    /// текущий угол поворота самого кольца (см. Confirm/AngleDistance), поэтому её позиция
    /// здесь и исход совмещения в Confirm всегда согласованы.</summary>
    private static void UpdateRing(ObservableCollection<RingTabViewModel> tabs, double radius, double ringRotation)
    {
        var step = 360.0 / tabs.Count;
        for (var i = 0; i < tabs.Count; i++)
        {
            var angleDeg = ringRotation + i * step;
            var rad = angleDeg * Math.PI / 180.0;
            tabs[i].X = CenterX + radius * Math.Sin(rad) - TabHalfSize;
            tabs[i].Y = CenterY - radius * Math.Cos(rad) - TabHalfSize;
            tabs[i].Angle = angleDeg;
        }
    }

    private static double NormalizeAngle(double angle) => ((angle % 360) + 360) % 360;

    private static double AngleDistance(double a, double b)
    {
        var diff = Math.Abs(NormalizeAngle(a) - NormalizeAngle(b));
        return Math.Min(diff, 360 - diff);
    }
}
