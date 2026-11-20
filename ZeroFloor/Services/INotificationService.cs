namespace ZeroFloor.Services;

public interface INotificationService
{
    event EventHandler<string>? NotificationRequested;

    void Publish(string message);
}
