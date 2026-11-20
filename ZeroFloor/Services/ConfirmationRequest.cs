namespace ZeroFloor.Services;

public sealed record ConfirmationRequest(
    string Title,
    string Message,
    Action OnConfirm,
    Action? OnCancel = null,
    string ConfirmLabel = "Да",
    string CancelLabel = "Отмена",
    bool IsDanger = false);
