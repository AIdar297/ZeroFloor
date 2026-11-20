namespace ZeroFloor.Services;

public sealed class ConfirmationService : IConfirmationService
{
    public event EventHandler<ConfirmationRequest>? ConfirmationRequested;

    public void RequestConfirmation(ConfirmationRequest request) =>
        ConfirmationRequested?.Invoke(this, request);
}
