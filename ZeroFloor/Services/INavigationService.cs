using ZeroFloor.Common;

namespace ZeroFloor.Services;

public interface INavigationService
{
    ViewModelBase? CurrentViewModel { get; }

    event EventHandler? CurrentViewModelChanged;

    void NavigateTo(ViewModelBase viewModel);
}
