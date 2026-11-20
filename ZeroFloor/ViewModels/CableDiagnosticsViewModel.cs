using System.Windows.Input;
using System.Windows.Threading;
using ZeroFloor.Common;
using ZeroFloor.Models;

namespace ZeroFloor.ViewModels;

public enum CableDiagnosticsOutcome
{
    Success,
    Partial,
    Failure
}

/// <summary>
/// Мини-игра «Диагностика троса» (Глава 1, пользовательское дополнение к сценарию, по тому же
/// принципу, что остальные мини-игры — сюжетный флоучарт не меняется кодом, только данными
/// сцены, см. DialogueNode.MinigameCue). В отличие от всех остальных, у неё не два исхода, а
/// три: стрелка натяжения сама "сползает" вниз и дрожит, игрок удерживает кнопку ("насос"),
/// чтобы поднимать натяжение, и должен удержать её в зелёной зоне RequiredStableSeconds подряд.
/// Одна попытка, без повтора — по истечении общего лимита сессии или сразу по достижению
/// устойчивости исход окончательный: SUCCESS (устойчивость набрана), PARTIAL (не набрана, но
/// заметная доля сессии прошла в зелёной зоне — в т.ч. если игрок просто пропустил мини-игру,
/// см. SkipCommand) или FAILURE (натяжение практически не выводилось в безопасный диапазон).
/// </summary>
public sealed class CableDiagnosticsViewModel : ViewModelBase, IDisposable
{
    private const int TickIntervalMs = 100;

    // Высота шкалы в пикселях — то же намеренное дублирование константы между VM и XAML, что и
    // у TimingBar/StealthGrid (TrackWidth там же): разметка CableDiagnosticsOverlayControl.xaml
    // задаёт Height="480" тому же треку.
    private const double TrackHeight = 480;
    private const double NeedleHeight = 6;

    private readonly CableDiagnosticsLayout _layout;
    private readonly Action<CableDiagnosticsOutcome> _onResolved;
    private readonly Random _random = new();

    private DispatcherTimer? _timer;
    private double _tension;
    private bool _isPumping;
    private bool _isResolved;
    private double _stableSeconds;
    private double _inZoneSeconds;
    private double _elapsedSeconds;

    public CableDiagnosticsViewModel(CableDiagnosticsLayout layout, Action<CableDiagnosticsOutcome> onResolved)
    {
        _layout = layout;
        _onResolved = onResolved;
        _tension = Math.Clamp(layout.StartTension, 0, 1);

        SkipCommand = new RelayCommand(_ => Resolve(CableDiagnosticsOutcome.Partial));

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickIntervalMs) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>0..1 — текущее натяжение, позиция стрелки на шкале.</summary>
    public double Tension
    {
        get => _tension;
        private set
        {
            if (SetProperty(ref _tension, value))
                OnPropertyChanged(nameof(NeedleTop));
        }
    }

    public double GreenMin => _layout.GreenMin;
    public double GreenMax => _layout.GreenMax;
    public double YellowMin => Math.Max(0, _layout.GreenMin - _layout.YellowMargin);
    public double YellowMax => Math.Min(1, _layout.GreenMax + _layout.YellowMargin);

    // Пиксельные координаты для прямого биндинга в XAML (без конвертеров) — шкала растёт снизу
    // вверх (0 внизу, 1 вверху), поэтому Y считается от (1 - значение).
    public double NeedleTop => (1 - Tension) * TrackHeight - NeedleHeight / 2;
    public double GreenZoneTop => (1 - GreenMax) * TrackHeight;
    public double GreenZoneHeight => (GreenMax - GreenMin) * TrackHeight;
    public double YellowZoneTop => (1 - YellowMax) * TrackHeight;
    public double YellowZoneHeight => (YellowMax - YellowMin) * TrackHeight;

    /// <summary>0..1 — прогресс текущей непрерывной серии удержания в зелёной зоне; сбрасывается
    /// в 0, как только стрелка покидает зону (для наглядной обратной связи игроку). Вычисляемое
    /// свойство — уведомление об изменении шлёт OnTick явным OnPropertyChanged.</summary>
    public double StableProgress => Math.Clamp(_stableSeconds / _layout.RequiredStableSeconds, 0, 1);

    /// <summary>0..1 — сколько от общего лимита сессии уже прошло (для шкалы времени).</summary>
    public double SessionProgress => Math.Clamp(_elapsedSeconds / _layout.MaxSessionSeconds, 0, 1);

    public ICommand SkipCommand { get; }

    public void StartPumping() => _isPumping = true;

    public void StopPumping() => _isPumping = false;

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

        const double dt = TickIntervalMs / 1000.0;
        _elapsedSeconds += dt;

        var rate = (_isPumping ? _layout.PumpRate : 0) - _layout.DecayRate;
        var jitter = (_random.NextDouble() * 2 - 1) * _layout.JitterAmplitude;
        Tension = Math.Clamp(Tension + (rate + jitter) * dt, 0, 1);

        var inZone = Tension >= GreenMin && Tension <= GreenMax;
        if (inZone)
        {
            _stableSeconds += dt;
            _inZoneSeconds += dt;

            if (_stableSeconds >= _layout.RequiredStableSeconds)
            {
                Resolve(CableDiagnosticsOutcome.Success);
                return;
            }
        }
        else
        {
            _stableSeconds = 0;
        }

        OnPropertyChanged(nameof(StableProgress));
        OnPropertyChanged(nameof(SessionProgress));

        if (_elapsedSeconds >= _layout.MaxSessionSeconds)
        {
            var inZoneFraction = _inZoneSeconds / _elapsedSeconds;
            Resolve(inZoneFraction >= _layout.PartialThresholdFraction
                ? CableDiagnosticsOutcome.Partial
                : CableDiagnosticsOutcome.Failure);
        }
    }

    private void Resolve(CableDiagnosticsOutcome outcome)
    {
        if (_isResolved)
            return;

        _isResolved = true;
        _timer?.Stop();
        _onResolved(outcome);
    }
}
