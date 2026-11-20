namespace ZeroFloor.Models;

/// <summary>Запись реестра персонажей (Data/Characters/characters.json) — требование
/// TECHNICAL_ARCHITECTURE §3: id, отображаемое имя, путь к единственному изображению.
/// Один персонаж = одно статичное изображение (CLAUDE.md) — здесь нет полей для эмоций/поз.</summary>
public sealed class CharacterDefinition
{
    public required string CharacterId { get; init; }
    public required string DisplayName { get; init; }
    public required string ImagePath { get; init; }

    /// <summary>Базовый масштаб изображения этого персонажа (1.0 — как есть). Нужен, когда
    /// исходные изображения разных персонажей сняты в разном кадрировании (например, один —
    /// в полный рост, другой — только по пояс) и визуально нуждаются в индивидуальной поправке
    /// масштаба, чтобы выглядеть соразмерно на одной сцене — без обрезки/растяжения самого
    /// файла. Сценарный CharacterCue.Scale (для конкретного узла) умножается на это значение,
    /// не заменяет его.</summary>
    public double DefaultScale { get; init; } = 1.0;

    /// <summary>Позиция персонажа на сцене, когда он становится текущим говорящим (см.
    /// NovelView.ApplySpeakerVisibilityAsync — единственный текущий механизм появления
    /// персонажа на экране; ручные CharacterCue.Position из сценарного JSON эту позицию уже не
    /// определяют, см. его комментарий). Данные, не код — чтобы расстановку персонажей менял
    /// сценарист правкой characters.json, а не разработчик правкой C#.</summary>
    public StagePosition DefaultPosition { get; init; } = StagePosition.Center;
}
