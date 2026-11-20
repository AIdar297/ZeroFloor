using System.Windows.Input;
using ZeroFloor.Common;
using ZeroFloor.Models;
using ZeroFloor.Services;
using ZeroFloor.Services.Presentation.World3D;

namespace ZeroFloor.ViewModels;

/// <summary>
/// Мост между интерактивным 3D-эпизодом (Services/Presentation/World3D, живёт во View) и
/// сценарием (NovelViewModel → GameSessionController). Сам не содержит игровых правил сюжета:
/// получает от эпизода ключ маршрута/исхода и передаёт их наверх ровно один раз. Также держит
/// bindable-настройки меню паузы эпизода (чувствительность мыши, инверсия, тряска, громкость).
/// </summary>
public sealed class Episode3DViewModel : ViewModelBase, IEpisodeStoryBridge, IDisposable
{
    private readonly Action<string> _onRouteCommitted;
    private readonly Action<string, string> _onCompleted;
    private readonly ISettingsStore _settings;
    private bool _routeCommitted;
    private bool _completed;
    private bool _disposed;

    public Episode3DViewModel(MinigameKind kind, ISettingsStore settings, Action<string> onRouteCommitted,
        Action<string, string> onCompleted, Action onExitToMenu)
    {
        Kind = kind;
        _settings = settings;
        _onRouteCommitted = onRouteCommitted;
        _onCompleted = onCompleted;
        ExitToMenuCommand = new RelayCommand(_ => onExitToMenu());
    }

    public MinigameKind Kind { get; }

    public string Title => Kind switch
    {
        MinigameKind.MarketStealth3D => "Ярмарка теней",
        MinigameKind.ArchiveStealth3D => "Архив Смотрителей",
        _ => "Шахты"
    };

    public ICommand ExitToMenuCommand { get; }

    public bool IsCompleted => _completed;

    public void CommitRoute(string routeKey)
    {
        if (_routeCommitted || _completed || _disposed) return;
        _routeCommitted = true;
        _onRouteCommitted(routeKey);
    }

    public void Complete(string outcomeKey, string routeKey)
    {
        if (_completed || _disposed) return;
        _completed = true;
        _onCompleted(outcomeKey, routeKey);
    }

    // ---------------------------------------------------------------- настройки паузы

    public double MouseSensitivity
    {
        get => _settings.Current.EpisodeMouseSensitivity;
        set { _settings.Current.EpisodeMouseSensitivity = Math.Clamp(value, 0.2, 3); Changed(); }
    }

    public bool InvertY
    {
        get => _settings.Current.EpisodeInvertY;
        set { _settings.Current.EpisodeInvertY = value; Changed(); }
    }

    public bool CameraShake
    {
        get => _settings.Current.EpisodeCameraShake;
        set { _settings.Current.EpisodeCameraShake = value; Changed(); }
    }

    public bool ShowControlHints
    {
        get => _settings.Current.EpisodeShowControlHints;
        set { _settings.Current.EpisodeShowControlHints = value; Changed(); }
    }

    public double SfxVolume
    {
        get => _settings.Current.SfxVolume;
        set { _settings.Current.SfxVolume = Math.Clamp(value, 0, 1); Changed(); }
    }

    private void Changed([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        OnPropertyChanged(name);
        _settings.NotifyChanged();
    }

    public void Dispose() => _disposed = true;
}
