using System.IO;
using System.Text.Json;
using ZeroFloor.Models;
using ZeroFloor.Services.Json;

namespace ZeroFloor.Services;

/// <summary>
/// Файловое хранилище настроек — требование "настройки должны сохраняться между запусками
/// игры". Загружается один раз при старте (повреждённый/отсутствующий файл — тихий откат на
/// значения по умолчанию, см. CLAUDE.md правило 8: битый файл не должен ронять приложение);
/// каждый <see cref="NotifyChanged"/> перезаписывает файл атомарно (временный файл → замена),
/// так что настройки, применённые "на лету" (громкость и т.п.), сразу переживут перезапуск.
/// Заменяет <see cref="InMemorySettingsStore"/> в композиционном корне (App.xaml.cs) —
/// интерфейс <see cref="ISettingsStore"/> тот же, поэтому все существующие потребители
/// (AudioService, CharacterAnimationService, SceneTransitionService, UiRevealService)
/// не замечают подмену реализации.
/// </summary>
public sealed class FileSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = SettingsJsonOptions.Create();

    private readonly string _filePath;
    private readonly object _syncRoot = new();

    public FileSettingsStore(string filePath)
    {
        _filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        Current = Load();
    }

    public AppSettings Current { get; }

    public event EventHandler? Changed;

    public void NotifyChanged()
    {
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private AppSettings Load()
    {
        if (!File.Exists(_filePath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Повреждённый файл настроек не должен мешать запуску игры — просто начинаем
            // с настроек по умолчанию (как и для повреждённого сейва, см. SaveService).
            return new AppSettings();
        }
    }

    private void Save()
    {
        lock (_syncRoot)
        {
            try
            {
                var json = JsonSerializer.Serialize(Current, Options);
                var tempPath = _filePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _filePath, overwrite: true);
            }
            catch (IOException)
            {
                // Не удалось сохранить настройки на диск — не повод прерывать игровую сессию;
                // значения остаются применёнными в памяти (Current), следующая попытка
                // NotifyChanged (например, другой слайдер) попробует записать снова.
            }
        }
    }
}
