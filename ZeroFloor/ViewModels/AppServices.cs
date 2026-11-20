using ZeroFloor.Services;
using ZeroFloor.Services.Presentation;

namespace ZeroFloor.ViewModels;

/// <summary>
/// Единый набор общих сервисов, передаваемый конструкторам экранных ViewModel вместо
/// списка отдельных параметров. Один источник правды о том, какие сервисы существуют —
/// расширение набора не требует правки сигнатур во всех ViewModel, только этого класса.
/// </summary>
public sealed class AppServices
{
    public required INavigationService Navigation { get; init; }
    public required IScreenTransitionService ScreenTransition { get; init; }
    public required INotificationService Notification { get; init; }
    public required IConfirmationService Confirmation { get; init; }
    public required ISaveService SaveService { get; init; }
    public required ISettingsStore Settings { get; init; }
    public required IScenarioRepository ScenarioRepository { get; init; }

    // IAssetService здесь уместен по той же причине, что и IAudioService ниже: его методы
    // принимают только id (string), не FrameworkElement — ViewModel может дёргать его
    // напрямую (NovelViewModel.Assets), а View читает результат (BitmapImage) без нарушения MVVM.
    public required IAssetService Asset { get; init; }

    // IAudioService здесь уместен (в отличие от ICharacterAnimationService/ISceneTransitionService
    // ниже): его методы принимают только id ресурсов (string/int), не FrameworkElement —
    // ViewModel может дёргать его напрямую, ничего не нарушая в MVVM.
    public required IAudioService Audio { get; init; }

    // ICharacterAnimationService/ISceneTransitionService здесь намеренно нет: их методы
    // принимают FrameworkElement, которого у ViewModel быть не должно (MVVM). ViewModel
    // передаёт во View только данные (CharacterCue и т.п. через событие), а сами сервисы
    // анимации View получает через App.Current — см. NovelView.xaml.cs.
}
