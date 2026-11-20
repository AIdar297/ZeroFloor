using System.Runtime.CompilerServices;
using System.Windows.Input;
using ZeroFloor.Common;
using ZeroFloor.Models;
using ZeroFloor.Services;

namespace ZeroFloor.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsStore _settingsStore;
    private readonly AppSettings _settings;

    public SettingsViewModel(AppServices services, Action onBack)
    {
        _settingsStore = services.Settings;
        _settings = services.Settings.Current;
        BackCommand = new RelayCommand(_ => onBack());
    }

    public double MasterVolume
    {
        get => _settings.MasterVolume;
        set => SetAndNotify(v => _settings.MasterVolume = v, value);
    }

    public double MusicVolume
    {
        get => _settings.MusicVolume;
        set => SetAndNotify(v => _settings.MusicVolume = v, value);
    }

    public double SfxVolume
    {
        get => _settings.SfxVolume;
        set => SetAndNotify(v => _settings.SfxVolume = v, value);
    }

    public double AmbienceVolume
    {
        get => _settings.AmbienceVolume;
        set => SetAndNotify(v => _settings.AmbienceVolume = v, value);
    }

    public double VoiceVolume
    {
        get => _settings.VoiceVolume;
        set => SetAndNotify(v => _settings.VoiceVolume = v, value);
    }

    public double UiScale
    {
        get => _settings.UiScale;
        set => SetAndNotify(v => _settings.UiScale = v, value);
    }

    public bool AllowSkipUnread
    {
        get => _settings.AllowSkipUnread;
        set => SetAndNotify(v => _settings.AllowSkipUnread = v, value);
    }

    public bool AutoAdvanceEnabled
    {
        get => _settings.AutoAdvanceEnabled;
        set => SetAndNotify(v => _settings.AutoAdvanceEnabled = v, value);
    }

    public bool DisableChoiceTimers
    {
        get => _settings.DisableChoiceTimers;
        set => SetAndNotify(v => _settings.DisableChoiceTimers = v, value);
    }

    /// <summary>Требование "скорость автоматического режима" — задержка перед автопродолжением
    /// в секундах, 0.5–8 (см. SettingsView, слайдер).</summary>
    public double AutoAdvanceDelaySeconds
    {
        get => _settings.AutoAdvanceDelaySeconds;
        set => SetAndNotify(v => _settings.AutoAdvanceDelaySeconds = v, value);
    }

    public bool IsFullscreen
    {
        get => _settings.IsFullscreen;
        set => SetAndNotify(v => _settings.IsFullscreen = v, value);
    }

    public IReadOnlyList<WindowResolution> WindowResolutionOptions { get; } = Enum.GetValues<WindowResolution>();

    public WindowResolution WindowResolution
    {
        get => _settings.WindowResolution;
        set => SetAndNotify(v => _settings.WindowResolution = v, value);
    }

    public IReadOnlyList<TextSpeed> TextSpeedOptions { get; } = Enum.GetValues<TextSpeed>();

    public TextSpeed TextSpeed
    {
        get => _settings.TextSpeed;
        set => SetAndNotify(v => _settings.TextSpeed = v, value);
    }

    public IReadOnlyList<AnimationLevel> AnimationLevelOptions { get; } = Enum.GetValues<AnimationLevel>();

    public AnimationLevel AnimationLevel
    {
        get => _settings.AnimationLevel;
        set => SetAndNotify(v => _settings.AnimationLevel = v, value);
    }

    public ICommand BackCommand { get; }

    /// <summary>Применяет значение, уведомляет об изменении этого bindable-свойства и сразу
    /// сигнализирует ISettingsStore.Changed — подписчики (AudioService, NovelViewModel,
    /// MainWindow и т.д.) применяют новое значение немедленно, не дожидаясь перезапуска/
    /// следующего узла, и FileSettingsStore тут же сохраняет настройки на диск (требование
    /// "настройки должны сохраняться между запусками игры").</summary>
    private void SetAndNotify<T>(Action<T> apply, T value, [CallerMemberName] string? propertyName = null)
    {
        apply(value);
        OnPropertyChanged(propertyName);
        _settingsStore.NotifyChanged();
    }
}
