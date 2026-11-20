using ZeroFloor.Models;

namespace ZeroFloor.Services;

public interface ISettingsStore
{
    AppSettings Current { get; }

    /// <summary>Сигнал о том, что какая-то настройка изменилась (например, слайдер громкости) —
    /// подписчики (AudioService) применяют новое значение к уже играющим звукам немедленно,
    /// не дожидаясь следующего PlayXxx.</summary>
    event EventHandler? Changed;

    void NotifyChanged();
}
