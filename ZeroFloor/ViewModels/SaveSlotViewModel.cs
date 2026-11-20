using System.Windows.Input;
using ZeroFloor.Common;
using ZeroFloor.Services;

namespace ZeroFloor.ViewModels;

/// <summary>Одна плитка на экране "Сохранения" — показывает всё, что требование просит
/// отображать: время сохранения, главу и краткое описание места (requirement 5-6),
/// состояние "пусто"/"повреждено" (requirement 8).</summary>
public sealed class SaveSlotViewModel : ViewModelBase
{
    public SaveSlotViewModel(SaveSlotSummary summary, SaveLoadMode mode, Action<SaveSlotSummary> onSelected)
    {
        IsAutoSave = summary.Kind == SaveSlotKind.Auto;
        Title = IsAutoSave ? "Автосохранение" : $"Слот {summary.SlotIndex}";
        IsOccupied = summary.IsOccupied;
        IsCorrupted = summary.IsCorrupted;

        TimestampText = summary.Timestamp is { } timestamp ? timestamp.ToString("dd.MM.yyyy HH:mm") : string.Empty;

        StatusText = summary switch
        {
            { IsCorrupted: true } => "Файл повреждён",
            { IsOccupied: false } => "Пустой слот",
            _ => BuildChapterText(summary)
        };

        LocationText = summary.IsOccupied && !summary.IsCorrupted ? summary.LocationSummary ?? string.Empty : string.Empty;
        PlayTimeText = summary.IsOccupied && !summary.IsCorrupted ? FormatPlayTime(summary.PlayTimeSeconds) : string.Empty;
        EndingText = summary.CurrentEndingId is { } endingId ? $"Концовка: {endingId}" : string.Empty;

        // В режиме записи выбирать можно только ручные слоты (автосейв сюда не показывается
        // вовсе, см. SaveLoadViewModel.RefreshSlots), в режиме загрузки — только непустой и
        // не повреждённый слот (requirement 8: защита от повреждённых файлов).
        var canSelect = mode == SaveLoadMode.Save || summary.CanLoad;
        SelectCommand = new RelayCommand(_ => onSelected(summary), _ => canSelect);
    }

    public bool IsAutoSave { get; }
    public string Title { get; }
    public bool IsOccupied { get; }
    public bool IsCorrupted { get; }
    public string TimestampText { get; }
    public string StatusText { get; }
    public string LocationText { get; }
    public string PlayTimeText { get; }
    public string EndingText { get; }
    public ICommand SelectCommand { get; }

    private static string BuildChapterText(SaveSlotSummary summary) =>
        summary.ChapterId is { } chapterId ? $"Глава: {chapterId}" : string.Empty;

    private static string FormatPlayTime(int totalSeconds)
    {
        var time = TimeSpan.FromSeconds(totalSeconds);
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours} ч {time.Minutes} мин"
            : $"{time.Minutes} мин {time.Seconds} с";
    }
}
