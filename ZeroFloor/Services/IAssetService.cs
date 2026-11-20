using System.Windows.Media.Imaging;
using ZeroFloor.Models;

namespace ZeroFloor.Services;

/// <summary>
/// Резолвит characterId/backgroundId → изображение (TECHNICAL_ARCHITECTURE.md, раздел 5/8).
/// Возвращает null, если ресурс не зарегистрирован в соответствующем реестре или файл
/// изображения отсутствует/повреждён — НИКОГДА не подставляет случайное изображение вместо
/// отсутствующего (CLAUDE.md, правило 6); вызывающий код (CharacterLayerControl, NovelView)
/// сам показывает явно помеченную заглушку "ресурс не найден".
/// </summary>
public interface IAssetService
{
    /// <summary>Запись реестра для персонажа (имя, базовый масштаб и т.д.) — null, если id
    /// не зарегистрирован в Data/Characters/characters.json.</summary>
    CharacterDefinition? GetCharacterDefinition(string characterId);

    /// <summary>Запись реестра для фона (название, описание, главы) — null, если id
    /// не зарегистрирован в Data/Backgrounds/backgrounds.json.</summary>
    BackgroundDefinition? GetBackgroundDefinition(string backgroundId);

    /// <summary>Загруженное (и закэшированное по characterId) изображение персонажа — null,
    /// если персонаж не зарегистрирован или его файл не найден/не читается.</summary>
    BitmapImage? ResolveCharacterImage(string characterId);

    /// <summary>Загруженное (и закэшированное по backgroundId) изображение фона — null,
    /// если фон не зарегистрирован или его файл не найден/не читается.</summary>
    BitmapImage? ResolveBackgroundImage(string backgroundId);
}
