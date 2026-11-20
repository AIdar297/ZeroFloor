using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using ZeroFloor.Common;
using ZeroFloor.Models;

namespace ZeroFloor.ViewModels;

public enum LogScanOutcome
{
    Success,
    Partial,
    Failure
}

/// <summary>Одна строка ленты — IsTarget решает, засчитывается ли подтверждение, пока она
/// активна (используется и логикой, и XAML-подсветкой целевой строки — поэтому public, не
/// internal: WPF-биндинг отражает только публичные члены); IsActive управляет визуальным
/// "тускнением" уже прокрученных строк.</summary>
public sealed class LogLineViewModel : ViewModelBase
{
    private bool _isActive;

    internal LogLineViewModel(string text, bool isTarget)
    {
        Text = text;
        IsTarget = isTarget;
    }

    public string Text { get; }
    public bool IsTarget { get; }

    public bool IsActive
    {
        get => _isActive;
        internal set => SetProperty(ref _isActive, value);
    }
}

/// <summary>
/// Мини-игра «Скан лога панели» (Глава 3, пользовательское дополнение к сценарию, по тому же
/// принципу, что остальные мини-игры — сюжетный флоучарт не меняется кодом, только данными
/// сцены, см. DialogueNode.MinigameCue). Лог прокручивается строка за строкой (новые — снизу,
/// как на терминале); нужная строка (LogScanLayout.TargetLine) появляется в потоке ровно
/// дважды — это два окна реакции. Нажатие "Поймать строку", пока активна (последняя показанная)
/// строка — целевая, засчитывает исход: первое появление — SUCCESS, второе — PARTIAL. Не успел
/// ни разу — после второго появления лента сама останавливается (FAILURE), дальше не
/// прокручиваясь. Как у CableDiagnostics/RingLock, исходы ведут на разные узлы той же сцены
/// (PartialNodeId/FailureNodeId), без повтора — но здесь это не критично для сюжета: все три
/// исхода читают один и тот же текст улики дальше по сценарию, различается только то, как
/// именно Макс её заметил.
/// </summary>
public sealed class LogScanViewModel : ViewModelBase, IDisposable
{
    private readonly LogScanLayout _layout;
    private readonly Action<LogScanOutcome> _onResolved;
    private readonly List<(string Text, bool IsTarget)> _sequence;

    private DispatcherTimer? _timer;
    private int _revealIndex;
    private int _targetRevealOrdinal;
    private LogLineViewModel? _activeLine;
    private bool _isResolved;

    public LogScanViewModel(LogScanLayout layout, Action<LogScanOutcome> onResolved)
    {
        _layout = layout;
        _onResolved = onResolved;
        _sequence = BuildSequence(layout);

        Lines = new ObservableCollection<LogLineViewModel>();
        ConfirmCommand = new RelayCommand(_ => Confirm());

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(0.1, layout.LineIntervalSeconds)) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public ObservableCollection<LogLineViewModel> Lines { get; }
    public ICommand ConfirmCommand { get; }

    public void Dispose()
    {
        if (_timer is null)
            return;

        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }

    private static List<(string, bool)> BuildSequence(LogScanLayout layout)
    {
        var sequence = new List<(string, bool)>();
        AppendFiller(sequence, layout, layout.LinesBeforeFirstWindow);
        sequence.Add((layout.TargetLine, true));
        AppendFiller(sequence, layout, layout.LinesBetweenWindows);
        sequence.Add((layout.TargetLine, true));
        return sequence;
    }

    private static void AppendFiller(List<(string, bool)> sequence, LogScanLayout layout, int count)
    {
        for (var i = 0; i < count; i++)
            sequence.Add((layout.FillerLines[i % layout.FillerLines.Count], false));
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_isResolved)
            return;

        if (_revealIndex >= _sequence.Count)
        {
            // Второе появление целевой строки уже показано, и её окно реакции только что
            // закрылось без подтверждения — лента останавливается здесь, дальше не прокручивается.
            Resolve(LogScanOutcome.Failure);
            return;
        }

        var (text, isTarget) = _sequence[_revealIndex];
        _revealIndex++;
        if (isTarget)
            _targetRevealOrdinal++;

        foreach (var line in Lines)
            line.IsActive = false;

        var newLine = new LogLineViewModel(text, isTarget) { IsActive = true };
        Lines.Add(newLine);
        if (Lines.Count > _layout.VisibleLineCount)
            Lines.RemoveAt(0);

        _activeLine = newLine;
    }

    private void Confirm()
    {
        if (_isResolved || _activeLine is not { IsTarget: true })
            return;

        Resolve(_targetRevealOrdinal <= 1 ? LogScanOutcome.Success : LogScanOutcome.Partial);
    }

    private void Resolve(LogScanOutcome outcome)
    {
        if (_isResolved)
            return;

        _isResolved = true;
        _timer?.Stop();
        _onResolved(outcome);
    }
}
