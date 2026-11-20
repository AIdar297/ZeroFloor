using System.IO;

namespace ZeroFloor.Services;

public sealed class FileLoggingService : ILoggingService
{
    private readonly string _logFilePath;
    private readonly object _syncRoot = new();

    public FileLoggingService()
    {
        var logsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZeroFloor",
            "logs");

        Directory.CreateDirectory(logsDirectory);
        _logFilePath = Path.Combine(logsDirectory, $"log-{DateTime.Now:yyyy-MM-dd}.txt");
    }

    public void LogInfo(string message) => WriteLine($"[INFO]  {DateTime.Now:HH:mm:ss} | {message}");

    public void LogError(Exception exception, string context) =>
        WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss} | {context} | {exception}");

    private void WriteLine(string line)
    {
        lock (_syncRoot)
        {
            try
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
            catch
            {
                // Логирование не должно само становиться источником падения приложения, которое пытается защитить.
            }
        }
    }
}
