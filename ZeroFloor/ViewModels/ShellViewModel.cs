using System.Windows.Threading;
using ZeroFloor.Common;

namespace ZeroFloor.ViewModels;

/// <summary>
/// ViewModel окна-оболочки: текущий экран плюс общие для всего приложения оверлеи
/// (экран загрузки, уведомления, диалог подтверждения) — единое место их показа,
/// чтобы не дублировать эту логику в каждом экране.
/// </summary>
public sealed class ShellViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private DispatcherTimer? _notificationTimer;
    private string? _notificationMessage;
    private ConfirmationDialogViewModel? _activeConfirmation;

    public ShellViewModel(AppServices services)
    {
        _services = services;

        _services.Navigation.CurrentViewModelChanged += (_, _) => OnPropertyChanged(nameof(CurrentViewModel));

        _services.ScreenTransition.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(LoadingMessage));
        };

        _services.Notification.NotificationRequested += (_, message) => ShowNotification(message);

        _services.Confirmation.ConfirmationRequested += (_, request) =>
            ActiveConfirmation = new ConfirmationDialogViewModel(request, () => ActiveConfirmation = null);
    }

    public ViewModelBase? CurrentViewModel => _services.Navigation.CurrentViewModel;

    public bool IsLoading => _services.ScreenTransition.IsLoading;

    public string? LoadingMessage => _services.ScreenTransition.Message;

    public string? NotificationMessage
    {
        get => _notificationMessage;
        private set => SetProperty(ref _notificationMessage, value);
    }

    public ConfirmationDialogViewModel? ActiveConfirmation
    {
        get => _activeConfirmation;
        private set => SetProperty(ref _activeConfirmation, value);
    }

    private void ShowNotification(string message)
    {
        NotificationMessage = message;

        _notificationTimer?.Stop();
        _notificationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
        _notificationTimer.Tick += (_, _) =>
        {
            _notificationTimer!.Stop();
            NotificationMessage = null;
        };
        _notificationTimer.Start();
    }
}
