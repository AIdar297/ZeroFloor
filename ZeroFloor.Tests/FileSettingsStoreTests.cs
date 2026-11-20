using System.IO;
using ZeroFloor.Models;
using ZeroFloor.Services;

namespace ZeroFloor.Tests;

/// <summary>Требование "настройки должны сохраняться между запусками игры" — проверяет, что
/// FileSettingsStore пишет настройки на диск и следующий запуск (новый экземпляр того же
/// файла) читает ровно то, что было записано, а повреждённый файл настроек не ломает запуск
/// (откат на значения по умолчанию, как и для повреждённого сейва).</summary>
public sealed class FileSettingsStoreTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _filePath;

    public FileSettingsStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ZeroFloorSettingsTests_" + Guid.NewGuid());
        _filePath = Path.Combine(_tempDirectory, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    [Fact]
    public void FirstLaunch_NoFileYet_UsesDefaults()
    {
        var store = new FileSettingsStore(_filePath);

        Assert.Equal(1.0, store.Current.MasterVolume);
        Assert.False(store.Current.AutoAdvanceEnabled);
        Assert.False(store.Current.IsFullscreen);
    }

    [Fact]
    public void ChangedSetting_PersistsAcrossNewStoreInstance_SimulatingRestart()
    {
        var store = new FileSettingsStore(_filePath);
        store.Current.MasterVolume = 0.42;
        store.Current.AutoAdvanceEnabled = true;
        store.Current.AutoAdvanceDelaySeconds = 4.5;
        store.Current.IsFullscreen = true;
        store.Current.WindowResolution = WindowResolution.FullHd1920x1080;
        store.Current.TextSpeed = TextSpeed.Fast;
        store.NotifyChanged();

        Assert.True(File.Exists(_filePath));

        // Новый экземпляр того же файла — имитация перезапуска игры.
        var restarted = new FileSettingsStore(_filePath);

        Assert.Equal(0.42, restarted.Current.MasterVolume);
        Assert.True(restarted.Current.AutoAdvanceEnabled);
        Assert.Equal(4.5, restarted.Current.AutoAdvanceDelaySeconds);
        Assert.True(restarted.Current.IsFullscreen);
        Assert.Equal(WindowResolution.FullHd1920x1080, restarted.Current.WindowResolution);
        Assert.Equal(TextSpeed.Fast, restarted.Current.TextSpeed);
    }

    [Fact]
    public void CorruptedSettingsFile_FallsBackToDefaults_DoesNotThrow()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_filePath, "{ not valid json at all");

        var store = new FileSettingsStore(_filePath);

        Assert.Equal(1.0, store.Current.MasterVolume);
    }

    [Fact]
    public void NotifyChanged_RaisesChangedEvent()
    {
        var store = new FileSettingsStore(_filePath);
        var raised = false;
        store.Changed += (_, _) => raised = true;

        store.NotifyChanged();

        Assert.True(raised);
    }
}
