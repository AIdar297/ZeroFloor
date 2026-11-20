namespace ZeroFloor.Models;

/// <summary>Запись реестра фонов (Data/Backgrounds/backgrounds.json) — требование "подключить
/// изображения к системе локаций игры". BackgroundId — ровно то значение, которое указывается
/// в SceneDefinition.Background конкретной сцены; одна и та же локация может встречаться
/// в нескольких главах (Chapters) при переиспользовании одного файла — см. CLAUDE.md про
/// "не заменять реальные ресурсы случайными файлами" и правило переиспользования.</summary>
public sealed class BackgroundDefinition
{
    public required string BackgroundId { get; init; }
    public required string Title { get; init; }
    public required string ImagePath { get; init; }
    public string? Description { get; init; }

    /// <summary>Номера глав, где используется этот фон — чисто описательное поле для реестра,
    /// не используется движком для принятия решений (ту же локацию сцена выбирает по
    /// SceneDefinition.Background конкретного JSON, не по этому списку).</summary>
    public IReadOnlyList<int> Chapters { get; init; } = Array.Empty<int>();
}
