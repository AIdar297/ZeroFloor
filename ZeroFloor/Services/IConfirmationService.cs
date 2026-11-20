namespace ZeroFloor.Services;

public interface IConfirmationService
{
    event EventHandler<ConfirmationRequest>? ConfirmationRequested;

    void RequestConfirmation(ConfirmationRequest request);
}
