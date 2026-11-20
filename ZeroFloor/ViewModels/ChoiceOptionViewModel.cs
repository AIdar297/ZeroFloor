using System.Windows.Input;
using ZeroFloor.Common;

namespace ZeroFloor.ViewModels;

public sealed class ChoiceOptionViewModel : ViewModelBase
{
    private bool _isSelected;

    public ChoiceOptionViewModel(string text, Action onConfirm)
    {
        Text = text;
        ConfirmCommand = new RelayCommand(_ => onConfirm());
    }

    public string Text { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public ICommand ConfirmCommand { get; }
}
