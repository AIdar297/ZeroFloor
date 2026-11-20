namespace ZeroFloor.Services;

public interface IScreenTransitionService
{
    bool IsLoading { get; }
    string? Message { get; }

    event EventHandler? Changed;

    void ShowLoading(string message);
    void HideLoading();
}
