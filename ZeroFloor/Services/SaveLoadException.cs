namespace ZeroFloor.Services;

/// <summary>Общее исключение всех ошибок чтения сейва (повреждённый/нечитаемый файл, пустой
/// слот) — требование "защита от повреждённых файлов сохранения": вызывающий код (ViewModel)
/// ловит именно его и показывает понятное сообщение, вместо падения игры.</summary>
public class SaveLoadException : Exception
{
    public SaveLoadException(string message) : base(message)
    {
    }

    public SaveLoadException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>Несовпадение версии формата сейва — отдельный тип, чтобы ViewModel мог показать
/// именно "сохранение сделано другой версией игры", а не общее "файл повреждён"
/// (TECHNICAL_ARCHITECTURE.md, раздел 9: "несовпадение saveVersion — явное сообщение
/// пользователю, а не молчаливая порча прогресса").</summary>
public sealed class SaveVersionMismatchException : SaveLoadException
{
    public int FoundVersion { get; }
    public int ExpectedVersion { get; }

    public SaveVersionMismatchException(int foundVersion, int expectedVersion)
        : base($"Сохранение сделано другой версией игры (версия сейва: {foundVersion}, ожидается: {expectedVersion}).")
    {
        FoundVersion = foundVersion;
        ExpectedVersion = expectedVersion;
    }
}
