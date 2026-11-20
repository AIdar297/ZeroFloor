using System.Windows.Input;
using ZeroFloor.Common;
using ZeroFloor.Services;

namespace ZeroFloor.ViewModels;

public sealed class ConfirmationDialogViewModel : ViewModelBase
{
    public ConfirmationDialogViewModel(ConfirmationRequest request, Action close)
    {
        Title = request.Title;
        Message = request.Message;
        ConfirmLabel = request.ConfirmLabel;
        CancelLabel = request.CancelLabel;
        IsDanger = request.IsDanger;

        ConfirmCommand = new RelayCommand(_ =>
        {
            close();
            request.OnConfirm();
        });

        CancelCommand = new RelayCommand(_ =>
        {
            close();
            request.OnCancel?.Invoke();
        });
    }

    public string Title { get; }
    public string Message { get; }
    public string ConfirmLabel { get; }
    public string CancelLabel { get; }
    public bool IsDanger { get; }

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }
}
