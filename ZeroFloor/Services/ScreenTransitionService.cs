namespace ZeroFloor.Services;

public sealed class ScreenTransitionService : IScreenTransitionService
{
    public bool IsLoading { get; private set; }
    public string? Message { get; private set; }

    public event EventHandler? Changed;

    public void ShowLoading(string message)
    {
        IsLoading = true;
        Message = message;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void HideLoading()
    {
        IsLoading = false;
        Message = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
