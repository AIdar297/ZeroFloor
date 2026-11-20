using System.Windows.Input;
using ZeroFloor.Common;

namespace ZeroFloor.ViewModels;

public sealed class PauseMenuViewModel : ViewModelBase
{
    public PauseMenuViewModel(
        Action onResume,
        Action onSave,
        Action onOpenLoad,
        Action onOpenSettings,
        Action onExitToMenu,
        Action onExitApplication)
    {
        ResumeCommand = new RelayCommand(_ => onResume());
        SaveCommand = new RelayCommand(_ => onSave());
        OpenLoadCommand = new RelayCommand(_ => onOpenLoad());
        OpenSettingsCommand = new RelayCommand(_ => onOpenSettings());
        ExitToMenuCommand = new RelayCommand(_ => onExitToMenu());
        ExitApplicationCommand = new RelayCommand(_ => onExitApplication());
    }

    public ICommand ResumeCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand OpenLoadCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitToMenuCommand { get; }
    public ICommand ExitApplicationCommand { get; }
}
