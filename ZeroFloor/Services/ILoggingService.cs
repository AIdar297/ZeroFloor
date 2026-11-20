namespace ZeroFloor.Services;

public interface ILoggingService
{
    void LogInfo(string message);

    void LogError(Exception exception, string context);
}
