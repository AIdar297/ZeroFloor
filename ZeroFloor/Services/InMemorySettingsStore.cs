using ZeroFloor.Models;

namespace ZeroFloor.Services;

/// <summary>
/// Временное хранилище настроек в памяти процесса. Заменяется файловым ISettingsService
/// (чтение/запись AppSettings на диск) по спецификации TECHNICAL_ARCHITECTURE.md, раздел 5.
/// </summary>
public sealed class InMemorySettingsStore : ISettingsStore
{
    public AppSettings Current { get; } = new();

    public event EventHandler? Changed;

    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
