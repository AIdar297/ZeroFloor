namespace ZeroFloor.Engine;

/// <summary>
/// Единое исключение для всех нарушений целостности сценария: несуществующие
/// идентификаторы сцен/узлов/выборов, некорректные переходы, отсутствующий текст
/// реплики, некорректно описанные условия/эффекты, попытка пересчитать уже
/// зафиксированную концовку. Выбрасывается как при загрузке сценария
/// (ScenarioRepository), так и во время выполнения (Engine) — это один и тот же
/// класс проблем: данные сценария (или обращение к ним) не соответствуют контракту.
/// </summary>
public sealed class ScenarioValidationException : Exception
{
    public ScenarioValidationException(string message) : base(message)
    {
    }

    public ScenarioValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
