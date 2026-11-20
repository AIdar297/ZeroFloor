namespace ZeroFloor.Services;

public sealed class NotificationService : INotificationService
{
    public event EventHandler<string>? NotificationRequested;

    public void Publish(string message) => NotificationRequested?.Invoke(this, message);
}
