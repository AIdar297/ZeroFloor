using System.IO;
using System.Linq;
using System.Windows.Input;
using ZeroFloor.Common;
using ZeroFloor.Models;
using ZeroFloor.Services;

namespace ZeroFloor.ViewModels;

/// <summary>
/// Экран "Сохранения" в двух режимах (requirement 1-8 раздела "сохранения/загрузки"):
/// Save — запись текущего прохождения в выбранный ручной слот (с подтверждением перезаписи
/// занятого слота, requirement 7); Load — загрузка из ручного слота или автосохранения
/// (requirement 3-4), с защитой от повреждённых файлов (requirement 8, см. SaveSlotSummary.
/// IsCorrupted) и явным сообщением при несовместимой версии сейва.
/// </summary>
public sealed class SaveLoadViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private readonly SaveLoadMode _mode;
    private readonly Func<SaveSlotData>? _currentSnapshotProvider;
    private readonly Action<SaveSlotData> _onSlotLoaded;

    public SaveLoadViewModel(
        AppServices services,
        SaveLoadMode mode,
        Func<SaveSlotData>? currentSnapshotProvider,
        Action<SaveSlotData> onSlotLoaded,
        Action onBack)
    {
        _services = services;
        _mode = mode;
        _currentSnapshotProvider = currentSnapshotProvider;
        _onSlotLoaded = onSlotLoaded;

        Title = mode == SaveLoadMode.Save ? "СОХРАНЕНИЕ" : "ЗАГРУЗКА";
        BackCommand = new RelayCommand(_ => onBack());

        Slots = Array.Empty<SaveSlotViewModel>();
        RefreshSlots();
    }

    public string Title { get; }
    public IReadOnlyList<SaveSlotViewModel> Slots { get; private set; }
    public ICommand BackCommand { get; }

    private void RefreshSlots()
    {
        var summaries = new List<SaveSlotSummary>();

        // Автосохранение показывается только для загрузки — вручную в него не пишут
        // (это отдельный, управляемый движком слот, см. SaveSlotKind).
        if (_mode == SaveLoadMode.Load)
            summaries.Add(_services.SaveService.GetAutoSaveSummary());

        summaries.AddRange(_services.SaveService.GetManualSlotSummaries());

        Slots = summaries.Select(summary => new SaveSlotViewModel(summary, _mode, OnSlotSelected)).ToList();
        OnPropertyChanged(nameof(Slots));
    }

    private void OnSlotSelected(SaveSlotSummary summary)
    {
        if (_mode == SaveLoadMode.Save)
            TrySave(summary);
        else
            TryLoad(summary);
    }

    private void TrySave(SaveSlotSummary summary)
    {
        if (_currentSnapshotProvider is null || summary.Kind != SaveSlotKind.Manual)
            return;

        void DoSave()
        {
            try
            {
                _services.SaveService.SaveManualSlot(summary.SlotIndex, _currentSnapshotProvider());
                _services.Notification.Publish($"Сохранено в слот {summary.SlotIndex}.");
            }
            catch (IOException ex)
            {
                _services.Notification.Publish($"Не удалось сохранить: {ex.Message}");
            }

            RefreshSlots();
        }

        if (summary.IsOccupied)
        {
            // Требование "подтверждение перезаписи сохранения" — не трогаем файл до явного "Да".
            _services.Confirmation.RequestConfirmation(new ConfirmationRequest(
                "Перезаписать сохранение?",
                summary.IsCorrupted
                    ? $"Слот {summary.SlotIndex} повреждён и будет заменён текущим сохранением."
                    : $"Слот {summary.SlotIndex} уже содержит сохранение от " +
                      $"{summary.Timestamp:dd.MM.yyyy HH:mm}. Оно будет перезаписано.",
                OnConfirm: DoSave,
                IsDanger: true));
        }
        else
        {
            DoSave();
        }
    }

    private void TryLoad(SaveSlotSummary summary)
    {
        if (!summary.CanLoad)
            return;

        try
        {
            var data = summary.Kind == SaveSlotKind.Auto
                ? _services.SaveService.LoadAutoSave()
                : _services.SaveService.LoadManualSlot(summary.SlotIndex);

            _onSlotLoaded(data);
        }
        catch (SaveLoadException ex)
        {
            // Требование "защита от повреждённых файлов сохранения" — ошибка загрузки не
            // должна крашить игру, только сообщить игроку и обновить список (на случай, если
            // файл стал нечитаем только сейчас, между построением списка и кликом).
            _services.Notification.Publish($"Не удалось загрузить сохранение: {ex.Message}");
            RefreshSlots();
        }
    }
}
