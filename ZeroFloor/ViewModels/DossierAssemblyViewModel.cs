using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using ZeroFloor.Common;
using ZeroFloor.Models;

namespace ZeroFloor.ViewModels;

/// <summary>Обрывок досье на столе.</summary>
public sealed class DossierCardViewModel : ViewModelBase
{
    private bool _isSelected;
    private bool _isPlaced;
    private int _wrongPulse;

    public DossierCardViewModel(DossierFragment fragment, double tilt)
    {
        Fragment = fragment;
        Tilt = tilt;
    }

    public DossierFragment Fragment { get; }
    public string Text => Fragment.Text;
    public double Tilt { get; }
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public bool IsPlaced { get => _isPlaced; set => SetProperty(ref _isPlaced, value); }

    /// <summary>Счётчик ошибочных попыток — View «трясёт» карточку при каждом увеличении.</summary>
    public int WrongPulse { get => _wrongPulse; set => SetProperty(ref _wrongPulse, value); }
}

/// <summary>Место для обрывка в листе досье.</summary>
public sealed class DossierSlotViewModel : ViewModelBase
{
    private string? _placedText;

    public DossierSlotViewModel(int dossier, int slot, string label)
    {
        Dossier = dossier;
        Slot = slot;
        Label = label;
    }

    public int Dossier { get; }
    public int Slot { get; }
    public string Label { get; }
    public string? PlacedText { get => _placedText; set { if (SetProperty(ref _placedText, value)) OnPropertyChanged(nameof(IsFilled)); } }
    public bool IsFilled => PlacedText is not null;
}

public sealed class DossierSheetViewModel
{
    public required string Title { get; init; }
    public required ObservableCollection<DossierSlotViewModel> Slots { get; init; }
}

/// <summary>
/// «Сборка досье» (Глава 8, сцена 8.2): перемешанные обрывки двух досье нужно разложить по
/// подписанным полям — по смыслу, а не по форме края, как в тексте сцены. Клик по обрывку и
/// клик по полю (или перетаскивание). Неверное поле — ошибка; время и число ошибок ограничены.
/// Итог (собрано ≥ SuccessFraction) передаётся наверх один раз — какой выбор узла применить,
/// решает NovelViewModel по данным сценария. Провал не блокирует главу.
/// </summary>
public sealed class DossierAssemblyViewModel : ViewModelBase, IDisposable
{
    private readonly DossierAssemblyLayout _layout;
    private readonly Action<bool> _onResolved;
    private readonly DispatcherTimer _timer;
    private double _remaining;
    private int _mistakes;
    private bool _finished;
    private bool _resolved;
    private DossierCardViewModel? _selected;
    private string _resultTitle = string.Empty;
    private string _resultText = string.Empty;
    private bool _succeeded;

    public DossierAssemblyViewModel(DossierAssemblyLayout layout, Action<bool> onResolved)
    {
        _layout = layout;
        _onResolved = onResolved;
        _remaining = Math.Max(10, layout.TimeLimitSeconds);

        Sheets = new ObservableCollection<DossierSheetViewModel>(layout.Dossiers.Select((d, i) => new DossierSheetViewModel
        {
            Title = d.Title,
            Slots = new ObservableCollection<DossierSlotViewModel>(d.SlotLabels.Select((label, s) => new DossierSlotViewModel(i, s, label)))
        }));

        var rng = new Random(layout.Fragments.Count * 7919);
        Cards = new ObservableCollection<DossierCardViewModel>(layout.Fragments
            .OrderBy(_ => rng.Next())
            .Select(f => new DossierCardViewModel(f, rng.NextDouble() * 6 - 3)));

        SelectCardCommand = new RelayCommand(p => SelectCard(p as DossierCardViewModel));
        PlaceCommand = new RelayCommand(p => { if (p is DossierSlotViewModel slot && _selected is not null) TryPlace(_selected, slot); });
        ContinueCommand = new RelayCommand(_ => Resolve(), _ => _finished);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Tick(0.1);
        _timer.Start();
    }

    public ObservableCollection<DossierSheetViewModel> Sheets { get; }
    public ObservableCollection<DossierCardViewModel> Cards { get; }
    public ICommand SelectCardCommand { get; }
    public ICommand PlaceCommand { get; }
    public ICommand ContinueCommand { get; }

    public int TotalCount => _layout.Fragments.Count;
    public int PlacedCount => Cards.Count(c => c.IsPlaced);
    public int Mistakes => _mistakes;
    public int MaxMistakes => Math.Max(1, _layout.MaxMistakes);
    public int MistakesLeft => Math.Max(0, MaxMistakes - _mistakes);
    public double TimeFraction => Math.Clamp(_remaining / Math.Max(10, _layout.TimeLimitSeconds), 0, 1);
    public string TimeText => $"{Math.Ceiling(_remaining):0} с";
    public bool IsFinished => _finished;
    public bool Succeeded => _succeeded;
    public string ResultTitle => _resultTitle;
    public string ResultText => _resultText;

    public void SelectCard(DossierCardViewModel? card)
    {
        if (_finished || card is null || card.IsPlaced) return;
        foreach (var c in Cards) c.IsSelected = ReferenceEquals(c, card) && !c.IsSelected;
        _selected = card.IsSelected ? card : null;
    }

    /// <summary>Попытка положить обрывок в поле. Возвращает true, если место верное.</summary>
    public bool TryPlace(DossierCardViewModel card, DossierSlotViewModel slot)
    {
        if (_finished || card.IsPlaced || slot.IsFilled) return false;

        if (card.Fragment.Dossier == slot.Dossier && card.Fragment.Slot == slot.Slot)
        {
            slot.PlacedText = card.Text;
            card.IsPlaced = true;
            card.IsSelected = false;
            _selected = null;
            OnPropertyChanged(nameof(PlacedCount));
            if (PlacedCount == TotalCount) Finish();
            return true;
        }

        _mistakes++;
        card.WrongPulse++;
        OnPropertyChanged(nameof(Mistakes));
        OnPropertyChanged(nameof(MistakesLeft));
        if (_mistakes >= MaxMistakes) Finish();
        return false;
    }

    private void Tick(double dt)
    {
        if (_finished) return;
        _remaining = Math.Max(0, _remaining - dt);
        OnPropertyChanged(nameof(TimeFraction));
        OnPropertyChanged(nameof(TimeText));
        if (_remaining <= 0) Finish();
    }

    private void Finish()
    {
        if (_finished) return;
        _finished = true;
        _timer.Stop();
        var fraction = PlacedCount / (double)Math.Max(1, TotalCount);
        _succeeded = fraction >= _layout.SuccessFraction;
        _resultTitle = _succeeded ? "Досье восстановлены" : "Картина неполная";
        _resultText = _succeeded
            ? $"Собрано {PlacedCount} из {TotalCount}. Строки встают на место — оба файла читаются полностью."
            : $"Собрано {PlacedCount} из {TotalCount}. Ключевые детали так и остаются в обрывках.";
        foreach (var c in Cards) c.IsSelected = false;
        OnPropertyChanged(nameof(IsFinished));
        OnPropertyChanged(nameof(Succeeded));
        OnPropertyChanged(nameof(ResultTitle));
        OnPropertyChanged(nameof(ResultText));
        CommandManager.InvalidateRequerySuggested();
    }

    private void Resolve()
    {
        if (_resolved || !_finished) return;
        _resolved = true;
        _onResolved(_succeeded);
    }

    /// <summary>Для автопроверки: разложить всё верно (или только часть).</summary>
    public void AutoSolve(int count)
    {
        foreach (var card in Cards.Where(c => !c.IsPlaced).Take(count).ToList())
        {
            var slot = Sheets[card.Fragment.Dossier].Slots[card.Fragment.Slot];
            TryPlace(card, slot);
        }
    }

    public void Dispose() => _timer.Stop();
}
