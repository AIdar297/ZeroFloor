namespace ZeroFloor.Engine;

/// <summary>Момент, в который движок просит presentation-слой выполнить автосохранение
/// (TECHNICAL_ARCHITECTURE.md, раздел 9: "на входе в главу, перед Выбором 10"). Сам движок
/// не пишет файлы (не знает про ISaveService/диск) — только сигнализирует через
/// GameSessionController.AutoSaveRequested; запись выполняет NovelViewModel, у которого есть
/// контекст (время игры, краткое описание места), недоступный движку.</summary>
public enum AutoSaveTrigger
{
    /// <summary>Вход в новую сцену/главу.</summary>
    ChapterEntered,

    /// <summary>Доступны варианты финальной ветки (Gate10) — перед тем как игрок их выберет.</summary>
    BeforeFinalChoice
}
