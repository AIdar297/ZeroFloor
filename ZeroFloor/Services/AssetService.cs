using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using ZeroFloor.Models;
using ZeroFloor.Services.Json;

namespace ZeroFloor.Services;

/// <summary>
/// Файловая реализация <see cref="IAssetService"/>: загружает реестры персонажей
/// (Data/Characters/characters.json) и фонов (Data/Backgrounds/backgrounds.json), резолвит
/// id → BitmapImage с кэшем по id, отдельным для каждого типа ресурса (TECHNICAL_ARCHITECTURE.md,
/// раздел 5/8 — "кэш загруженных BitmapImage по id, не грузить один и тот же файл повторно").
/// Отсутствующий/повреждённый реестр или файл изображения — не повод падать: сервис просто
/// ничего не резолвит для соответствующего id (CLAUDE.md, правило 6), вызывающий код
/// показывает явную заглушку.
/// </summary>
public sealed class AssetService : IAssetService
{
    private readonly Dictionary<string, CharacterDefinition> _charactersById;
    private readonly Dictionary<string, BackgroundDefinition> _backgroundsById;
    private readonly string _assetsRootDirectory;
    private readonly Dictionary<string, BitmapImage> _characterImageCache = new();
    private readonly Dictionary<string, BitmapImage> _backgroundImageCache = new();

    public AssetService(string charactersJsonPath, string backgroundsJsonPath, string assetsRootDirectory)
    {
        _assetsRootDirectory = assetsRootDirectory;
        _charactersById = LoadRegistry<CharacterDefinition>(charactersJsonPath, c => c.CharacterId);
        _backgroundsById = LoadRegistry<BackgroundDefinition>(backgroundsJsonPath, b => b.BackgroundId);
    }

    /// <summary>Конструктор для тестов — реестры передаются напрямую, без чтения файлов.</summary>
    public AssetService(
        IEnumerable<CharacterDefinition> characters,
        IEnumerable<BackgroundDefinition> backgrounds,
        string assetsRootDirectory)
    {
        _assetsRootDirectory = assetsRootDirectory;
        _charactersById = characters.ToDictionary(c => c.CharacterId);
        _backgroundsById = backgrounds.ToDictionary(b => b.BackgroundId);
    }

    public CharacterDefinition? GetCharacterDefinition(string characterId) =>
        _charactersById.TryGetValue(characterId, out var definition) ? definition : null;

    public BackgroundDefinition? GetBackgroundDefinition(string backgroundId) =>
        _backgroundsById.TryGetValue(backgroundId, out var definition) ? definition : null;

    public BitmapImage? ResolveCharacterImage(string characterId) => ResolveImage(
        characterId, _charactersById, _characterImageCache, d => d.ImagePath);

    public BitmapImage? ResolveBackgroundImage(string backgroundId) => ResolveImage(
        backgroundId, _backgroundsById, _backgroundImageCache, d => d.ImagePath);

    private BitmapImage? ResolveImage<TDefinition>(
        string id,
        Dictionary<string, TDefinition> registry,
        Dictionary<string, BitmapImage> cache,
        Func<TDefinition, string> getImagePath)
    {
        if (cache.TryGetValue(id, out var cached))
            return cached;

        if (!registry.TryGetValue(id, out var definition))
            return null;

        var fullPath = Path.Combine(_assetsRootDirectory, getImagePath(definition));
        if (!File.Exists(fullPath))
            return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            cache[id] = bitmap;
            return bitmap;
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or IOException)
        {
            // Повреждённый файл/неподдерживаемый формат — не подставляем случайную картинку
            // и не роняем игру (CLAUDE.md, правило 6); вызывающий код покажет заглушку.
            return null;
        }
    }

    private static Dictionary<string, TDefinition> LoadRegistry<TDefinition>(
        string jsonPath, Func<TDefinition, string> getId)
    {
        if (!File.Exists(jsonPath))
            return new Dictionary<string, TDefinition>();

        try
        {
            var json = File.ReadAllText(jsonPath);
            var items = JsonSerializer.Deserialize<TDefinition[]>(json, AssetJsonOptions.Create());
            return (items ?? Array.Empty<TDefinition>()).ToDictionary(getId);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Повреждённый реестр не должен мешать запуску игры — просто ни один ресурс этого
            // типа не резолвится в реальное изображение (везде показывается заглушка).
            return new Dictionary<string, TDefinition>();
        }
    }
}
