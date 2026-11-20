using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using ZeroFloor.Common;
using ZeroFloor.Models;

namespace ZeroFloor.ViewModels;

/// <summary>Фрагмент из пула: настоящий (PairIndex ≥ 0) или ложный (PairIndex = -1). Перенос —
/// «выбрать фрагмент, затем строку» (одинаково работает с мышью и клавиатурой).</summary>
public sealed class CodeFragmentViewModel : ViewModelBase
{
    private bool _isSelected;
    private int _wrongPulse;

    internal CodeFragmentViewModel(int pairIndex, string text)
    {
        PairIndex = pairIndex;
        Text = text;
    }

    public int PairIndex { get; }
    public string Text { get; }
    public bool IsDecoy => PairIndex < 0;

    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Растёт при каждой ошибке с этим фрагментом — View «трясёт» плашку.</summary>
    public int WrongPulse
    {
        get => _wrongPulse;
        internal set => SetProperty(ref _wrongPulse, value);
    }
}

/// <summary>Строка испорченной записи: подсказка (CorruptedHint) постепенно «гаснет», пока строка
/// не восстановлена; после восстановления показывает чистый текст.</summary>
public sealed class CodeSlotViewModel : ViewModelBase
{
    public const char Dead = '●';

    private bool _isFilled;
    private string _filledText = string.Empty;
    private string _hint;
    private int _decayPulse;

    internal CodeSlotViewModel(int pairIndex, string hint)
    {
        PairIndex = pairIndex;
        _hint = hint;
        Readable = CountReadable(hint);
    }

    public int PairIndex { get; }

    public string Hint
    {
        get => _hint;
        internal set
        {
            if (SetProperty(ref _hint, value))
            {
                OnPropertyChanged(nameof(Integrity));
                DecayPulse++;
            }
        }
    }

    /// <summary>Сколько читаемых символов было изначально (для доли целостности).</summary>
    public int Readable { get; }

    /// <summary>0..1 — какая часть исходно читаемых символов ещё видна.</summary>
    public double Integrity => Readable == 0 ? 0 : Math.Clamp(CountReadable(_hint) / (double)Readable, 0, 1);

    public int DecayPulse
    {
        get => _decayPulse;
        private set => SetProperty(ref _decayPulse, value);
    }

    public bool IsFilled
    {
        get => _isFilled;
        internal set => SetProperty(ref _isFilled, value);
    }

    public string FilledText
    {
        get => _filledText;
        internal set => SetProperty(ref _filledText, value);
    }

    internal static int CountReadable(string s) => s.Count(c => char.IsLetterOrDigit(c));
}

/// <summary>
/// «Сборка разрушенного кода» (Глава 8, файл отца). Игрок переносит восстановленные фрагменты в
/// строки испорченной записи, пока не истёк таймер и не превышен лимит ошибок. Усложнения, все
/// заданы данными сцены (CodeAssemblyLayout) и выключены по умолчанию:
/// - ложные фрагменты (Decoys) — правдоподобные перестановки слов/цифр; положить такой — ошибка;
/// - распад записи (DecaySeconds) — протокол гасит буквы в невосстановленных строках, читать
///   подсказки со временем всё труднее;
/// - штраф времени за ошибку (ErrorTimePenalty);
/// - перемешивание пула (ShuffleSeconds) — буфер фрагментов «прыгает»;
/// - серии (ComboWindowSeconds/ComboBonusSeconds) — быстрые точные попадания дают время.
/// Исход как прежде: успех — Advance(), провал — FailureNodeId (father_file_found = false).
/// </summary>
public sealed class CodeAssemblyViewModel : ViewModelBase, IDisposable
{
    private const int TickIntervalMs = 100;

    private readonly CodeAssemblyLayout _layout;
    private readonly Action _onSuccess;
    private readonly Action _onFailure;
    private readonly Random _random = new();

    private DispatcherTimer? _timer;
    private double _remainingSeconds;
    private int _errorCount;
    private int _placedCount;
    private bool _isResolved;
    private CodeFragmentViewModel? _selectedFragment;
    private double _decayClock;
    private double _shuffleClock;
    private double _sinceLastHit = double.MaxValue;
    private double _elapsed;
    private int _combo;
    private string _statusText = string.Empty;
    private bool _statusIsGood;
    private int _statusPulse;
    private double _timeLimit;

    public CodeAssemblyViewModel(CodeAssemblyLayout layout, Action onSuccess, Action onFailure)
    {
        _layout = layout;
        _onSuccess = onSuccess;
        _onFailure = onFailure;
        MaxErrors = Math.Max(1, layout.MaxErrors);
        _timeLimit = Math.Max(5, layout.TimeLimitSeconds);
        _remainingSeconds = _timeLimit;

        var pool = layout.Pairs.Select((p, i) => new CodeFragmentViewModel(i, p.CleanText))
            .Concat(layout.Decoys.Select(d => new CodeFragmentViewModel(-1, d)))
            .OrderBy(_ => _random.Next());
        Fragments = new ObservableCollection<CodeFragmentViewModel>(pool);
        Slots = new ObservableCollection<CodeSlotViewModel>(
            layout.Pairs.Select((pair, i) => new CodeSlotViewModel(i, pair.CorruptedHint)));

        SelectFragmentCommand = new RelayCommand(parameter => SelectFragment((CodeFragmentViewModel)parameter!));
        PlaceAtSlotCommand = new RelayCommand(parameter => PlaceAt((CodeSlotViewModel)parameter!));

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickIntervalMs) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>Шкала времени считается от исходного лимита; бонусы серий могут поднять её выше.</summary>
    public double TimeLimitSeconds => _timeLimit;
    public int MaxErrors { get; }

    public double RemainingSeconds
    {
        get => _remainingSeconds;
        private set
        {
            if (SetProperty(ref _remainingSeconds, value))
                OnPropertyChanged(nameof(TimeFraction));
        }
    }

    public double TimeFraction => TimeLimitSeconds <= 0 ? 0 : Math.Clamp(RemainingSeconds / TimeLimitSeconds, 0, 1);

    public int ErrorCount
    {
        get => _errorCount;
        private set => SetProperty(ref _errorCount, value);
    }

    public int Combo
    {
        get => _combo;
        private set => SetProperty(ref _combo, value);
    }

    /// <summary>Средняя целостность невосстановленных строк, 0..1 (распад записи).</summary>
    public double RecordIntegrity
    {
        get
        {
            var open = Slots.Where(s => !s.IsFilled).ToList();
            return open.Count == 0 ? 1 : open.Average(s => s.Integrity);
        }
    }

    public bool HasDecoys => _layout.Decoys.Count > 0;
    public bool HasDecay => _layout.DecaySeconds > 0;

    /// <summary>Короткое сообщение о последнем событии («Серия ×2 · +2 с», «Ложный фрагмент · −4 с»).</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool StatusIsGood
    {
        get => _statusIsGood;
        private set => SetProperty(ref _statusIsGood, value);
    }

    /// <summary>Растёт при каждом новом сообщении — View проигрывает анимацию появления.</summary>
    public int StatusPulse
    {
        get => _statusPulse;
        private set => SetProperty(ref _statusPulse, value);
    }

    public ObservableCollection<CodeFragmentViewModel> Fragments { get; }
    public ObservableCollection<CodeSlotViewModel> Slots { get; }

    public ICommand SelectFragmentCommand { get; }
    public ICommand PlaceAtSlotCommand { get; }

    public void Dispose()
    {
        if (_timer is null)
            return;

        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }

    private void SelectFragment(CodeFragmentViewModel fragment)
    {
        if (_isResolved)
            return;

        if (ReferenceEquals(_selectedFragment, fragment))
        {
            fragment.IsSelected = false;
            _selectedFragment = null;
            return;
        }

        if (_selectedFragment is not null)
            _selectedFragment.IsSelected = false;

        _selectedFragment = fragment;
        fragment.IsSelected = true;
    }

    private void PlaceAt(CodeSlotViewModel slot)
    {
        if (_isResolved || slot.IsFilled || _selectedFragment is null)
            return;

        var fragment = _selectedFragment;
        fragment.IsSelected = false;
        _selectedFragment = null;

        if (fragment.PairIndex == slot.PairIndex)
        {
            slot.IsFilled = true;
            slot.FilledText = fragment.Text;
            Fragments.Remove(fragment);
            _placedCount++;

            if (_layout.ComboWindowSeconds > 0 && _sinceLastHit <= _layout.ComboWindowSeconds)
            {
                Combo++;
                RemainingSeconds += _layout.ComboBonusSeconds;
                Say($"Серия ×{Combo + 1} · +{_layout.ComboBonusSeconds:0} с", good: true);
            }
            else
            {
                Combo = 0;
                Say("Строка восстановлена", good: true);
            }
            _sinceLastHit = 0;
            OnPropertyChanged(nameof(RecordIntegrity));

            if (_placedCount == Slots.Count)
                Resolve(_onSuccess);

            return;
        }

        ErrorCount++;
        Combo = 0;
        fragment.WrongPulse++;
        RemainingSeconds = Math.Max(0, RemainingSeconds - _layout.ErrorTimePenalty);
        var penalty = _layout.ErrorTimePenalty > 0 ? $" · −{_layout.ErrorTimePenalty:0} с" : string.Empty;
        Say((fragment.IsDecoy ? "Ложный фрагмент" : "Не та строка") + penalty, good: false);

        if (ErrorCount >= MaxErrors || RemainingSeconds <= 0)
            Resolve(_onFailure);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_isResolved)
            return;

        const double dt = TickIntervalMs / 1000.0;
        _elapsed += dt;
        _sinceLastHit += dt;
        RemainingSeconds = Math.Max(0, RemainingSeconds - dt);

        if (_layout.DecaySeconds > 0)
        {
            _decayClock += dt;
            if (_decayClock >= _layout.DecaySeconds)
            {
                _decayClock = 0;
                DecayOneSymbol();
            }
        }

        if (_layout.ShuffleSeconds > 0)
        {
            _shuffleClock += dt;
            if (_shuffleClock >= _layout.ShuffleSeconds)
            {
                _shuffleClock = 0;
                ShufflePool();
            }
        }

        if (RemainingSeconds <= 0)
            Resolve(_onFailure);
    }

    /// <summary>Протокол очистки гасит одну букву в случайной невосстановленной строке, пока
    /// у строки не погаснет MaxDecayFraction исходно читаемых символов.</summary>
    private void DecayOneSymbol()
    {
        var candidates = Slots.Where(s => !s.IsFilled && 1 - s.Integrity < _layout.MaxDecayFraction).ToList();
        if (candidates.Count == 0)
            return;

        var slot = candidates[_random.Next(candidates.Count)];
        var letters = slot.Hint.Select((c, i) => (c, i)).Where(t => char.IsLetterOrDigit(t.c)).ToList();
        if (letters.Count == 0)
            return;

        var (_, index) = letters[_random.Next(letters.Count)];
        var sb = new StringBuilder(slot.Hint) { [index] = CodeSlotViewModel.Dead };
        slot.Hint = sb.ToString();
        OnPropertyChanged(nameof(RecordIntegrity));
    }

    private void ShufflePool()
    {
        if (Fragments.Count < 2)
            return;

        var order = Fragments.OrderBy(_ => _random.Next()).ToList();
        for (var i = 0; i < order.Count; i++)
        {
            var current = Fragments.IndexOf(order[i]);
            if (current != i)
                Fragments.Move(current, i);
        }
        Say("Буфер перемешан", good: false);
    }

    private void Say(string text, bool good)
    {
        StatusIsGood = good;
        StatusText = text;
        StatusPulse++;
    }

    private void Resolve(Action outcome)
    {
        _isResolved = true;
        _timer?.Stop();
        outcome();
    }

    /// <summary>Для автопроверки: выбрать фрагмент и положить в строку.</summary>
    public void TryPlace(CodeFragmentViewModel fragment, CodeSlotViewModel slot)
    {
        SelectFragment(fragment);
        PlaceAt(slot);
    }

    /// <summary>Для автопроверки: прокрутить внутренние часы на заданное время.</summary>
    public void Simulate(double seconds)
    {
        for (var t = 0.0; t < seconds && !_isResolved; t += TickIntervalMs / 1000.0)
            OnTick(null, EventArgs.Empty);
    }
}
