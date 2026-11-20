using ZeroFloor.Common;

namespace ZeroFloor.Services;

public sealed class NavigationService : INavigationService
{
    public ViewModelBase? CurrentViewModel { get; private set; }

    public event EventHandler? CurrentViewModelChanged;

    public void NavigateTo(ViewModelBase viewModel)
    {
        CurrentViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
    }
}
