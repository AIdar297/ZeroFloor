namespace ZeroFloor.Models;

public enum TextSpeed
{
    Slow,
    Normal,
    Fast,
    Instant
}

/// <summary>Full — анимации как задумано; Reduced — те же анимации, но короче и мягче
/// (для чувствительности к движению); Off — мгновенное применение конечного состояния,
/// без визуального перехода вовсе.</summary>
public enum AnimationLevel
{
    Full,
    Reduced,
    Off
}

/// <summary>Размер окна в оконном режиме (не влияет на полноэкранный режим — там окно
/// всегда занимает весь экран). Весь интерфейс спроектирован на холсте 1920x1080 и
/// масштабируется Viewbox'ом целиком (см. MainWindow.xaml), поэтому любое из этих
/// разрешений поддерживается без искажений пропорций.</summary>
public enum WindowResolution
{
    Hd1280x720,
    Hd1600x900,
    FullHd1920x1080
}

public sealed class AppSettings
{
    // Пять категорий громкости по заданию: Master — общий множитель поверх всех остальных
    // (эффективная громкость = MasterVolume × громкость категории), плюс четыре отдельные.
    public double MasterVolume { get; set; } = 1.0;
    public double MusicVolume { get; set; } = 0.8;
    public double SfxVolume { get; set; } = 0.8;
    public double AmbienceVolume { get; set; } = 0.8;
    public double VoiceVolume { get; set; } = 0.9;

    public TextSpeed TextSpeed { get; set; } = TextSpeed.Normal;

    /// <summary>Автоматическое продолжение диалогов (авточтение) — по умолчанию выключено;
    /// останавливается на узле с выбором (NovelViewModel не запускает таймер, когда показаны
    /// варианты выбора).</summary>
    public bool AutoAdvanceEnabled { get; set; } = false;

    /// <summary>Задержка автопродолжения в секундах — требование "скорость автоматического
    /// режима". Диапазон задаётся в SettingsView (0.5–8 секунд).</summary>
    public double AutoAdvanceDelaySeconds { get; set; } = 2.5;

    /// <summary>Разрешён ли пропуск ЕЩЁ НЕ прочитанного текста — по умолчанию выключено
    /// (PROJECT_ANALYSIS 13.3). Пропуск уже прочитанного (ViewedNodeIds) работает всегда,
    /// независимо от этого флага — см. NovelViewModel.SkipCommand.</summary>
    public bool AllowSkipUnread { get; set; } = false;

    /// <summary>Отключает таймер ответа на реплике (см. DialogueNode.ChoiceTimerCue, Глава 5
    /// «Индикатор подозрения») — для игроков, которым нужно больше времени на чтение/выбор.
    /// По умолчанию включено (как и задумано сценой); сам HUD-индикатор подозрения при этом
    /// остаётся видимым — отключается только автовыбор по таймауту.</summary>
    public bool DisableChoiceTimers { get; set; } = false;

    public double UiScale { get; set; } = 1.0;
    public AnimationLevel AnimationLevel { get; set; } = AnimationLevel.Full;

    // Управление в интерактивных 3D-эпизодах (Главы 2А и 6) — меняется в их собственном меню паузы.
    public double EpisodeMouseSensitivity { get; set; } = 1.0;
    public bool EpisodeInvertY { get; set; } = false;
    public bool EpisodeCameraShake { get; set; } = true;
    public bool EpisodeShowControlHints { get; set; } = true;

    public bool IsFullscreen { get; set; } = false;
    public WindowResolution WindowResolution { get; set; } = WindowResolution.Hd1600x900;
}
