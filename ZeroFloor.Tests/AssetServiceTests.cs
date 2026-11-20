using System.IO;
using System.Linq;
using System.Text.Json;
using ZeroFloor.Models;
using ZeroFloor.Services;
using ZeroFloor.Services.Json;

namespace ZeroFloor.Tests;

/// <summary>
/// Проверяет подключение изображений персонажей (Data/Characters/characters.json +
/// Assets/Characters/*.png) и фонов локаций (Data/Backgrounds/backgrounds.json +
/// Assets/Backgrounds/*) через AssetService: все зарегистрированные id резолвятся в читаемое
/// изображение разумного размера, отсутствующий id не роняет сервис. Данные берутся из
/// РЕАЛЬНОГО реестра и реальных файлов проекта (копируются в выход тестов — см.
/// ZeroFloor.Tests.csproj), не из тестовых фикстур — это и есть требование "проверить
/// корректность отображения каждого изображения".
/// </summary>
public sealed class AssetServiceTests
{
    private static readonly string CharactersJsonPath =
        Path.Combine(AppContext.BaseDirectory, "ProjectData", "Characters", "characters.json");

    private static readonly string BackgroundsJsonPath =
        Path.Combine(AppContext.BaseDirectory, "ProjectData", "Backgrounds", "backgrounds.json");

    private static readonly string AssetsRootDirectory =
        Path.Combine(AppContext.BaseDirectory, "ProjectData");

    private static readonly string[] ExpectedCharacterIds =
    {
        "max_orlov", "ania_levitskaya", "viktor_savchuk", "irina_wessel",
        "gosha_kovalchuk", "dan_kovalski", "leonid_arsenev_kron", "sonya_reis", "orlov_senior"
    };

    private static readonly string[] ExpectedBackgroundIds =
    {
        "main_menu", "dispatch_room_6", "shadow_market_approach", "gosha_stall", "elevator_shaft",
        "ventilation_station", "administration_corridor", "interrogation_hall", "technical_shafts",
        "dark_technical_corridor", "ania_apartment", "keepers_archive", "archive_terminal",
        "shadow_market_after_raid", "wessel_office", "crown_corridors", "crown_reception",
        "server_cathedral", "core_hidden_corridor", "keeper_post", "core_corridors",
        "three_terminals_hall", "ending_a_escape", "ending_b_collapse", "ending_c_successor",
        "ending_d_balance"
    };

    private static AssetService CreateService() => new(CharactersJsonPath, BackgroundsJsonPath, AssetsRootDirectory);

    [Fact]
    public void Registry_ContainsAllNineCanonicalCharacters()
    {
        var service = CreateService();

        foreach (var id in ExpectedCharacterIds)
            Assert.NotNull(service.GetCharacterDefinition(id));
    }

    [Fact]
    public void ResolveCharacterImage_EachCanonicalCharacter_DecodesToReasonableFullFrameImage()
    {
        var service = CreateService();

        foreach (var id in ExpectedCharacterIds)
        {
            var image = service.ResolveCharacterImage(id);

            Assert.True(image is not null, $"Изображение для '{id}' не резолвится — проверь Assets/Characters и characters.json.");
            Assert.True(image!.PixelWidth > 100 && image.PixelHeight > 100,
                $"Подозрительно маленькое декодированное изображение для '{id}': {image.PixelWidth}x{image.PixelHeight}.");
        }
    }

    [Fact]
    public void ResolveCharacterImage_UnknownCharacterId_ReturnsNull_NoCrash()
    {
        var service = CreateService();

        Assert.Null(service.ResolveCharacterImage("no_such_character"));
        Assert.Null(service.GetCharacterDefinition("no_such_character"));
    }

    [Fact]
    public void CharacterRegistry_NoTwoCharactersShareTheSameImageFile()
    {
        var characters = LoadRealCharacterRegistry();

        var distinctPaths = characters.Select(c => c.ImagePath).Distinct().Count();
        Assert.Equal(characters.Length, distinctPaths);
    }

    [Fact]
    public void CharacterRegistry_HasExactlyTheNineCanonicalIds_NoMoreNoFewer()
    {
        var characters = LoadRealCharacterRegistry();

        var actualIds = characters.Select(c => c.CharacterId).OrderBy(id => id).ToArray();
        var expectedIds = ExpectedCharacterIds.OrderBy(id => id).ToArray();

        Assert.Equal(expectedIds, actualIds);
    }

    [Fact]
    public void ResolveCharacterImage_IsCachedAcrossCalls_ReturnsSameInstance()
    {
        var service = CreateService();

        var first = service.ResolveCharacterImage("max_orlov");
        var second = service.ResolveCharacterImage("max_orlov");

        Assert.Same(first, second);
    }

    [Fact]
    public void MissingCharacterRegistryFile_DoesNotThrow_AllCharactersUnresolved()
    {
        var service = new AssetService(
            Path.Combine(AppContext.BaseDirectory, "does_not_exist_characters.json"),
            BackgroundsJsonPath,
            AssetsRootDirectory);

        Assert.Null(service.ResolveCharacterImage("max_orlov"));
        Assert.Null(service.GetCharacterDefinition("max_orlov"));
    }

    [Fact]
    public void MissingImageFileForRegisteredCharacter_ReturnsNull_NoCrash()
    {
        var definition = new CharacterDefinition
        {
            CharacterId = "ghost_character",
            DisplayName = "Призрак для теста",
            ImagePath = "Assets/Characters/does_not_exist.png"
        };

        var service = new AssetService(new[] { definition }, Array.Empty<BackgroundDefinition>(), AssetsRootDirectory);

        Assert.NotNull(service.GetCharacterDefinition("ghost_character"));
        Assert.Null(service.ResolveCharacterImage("ghost_character"));
    }

    [Fact]
    public void BackgroundRegistry_ContainsAllTwentyLocations()
    {
        var service = CreateService();

        foreach (var id in ExpectedBackgroundIds)
            Assert.NotNull(service.GetBackgroundDefinition(id));
    }

    [Fact]
    public void ResolveBackgroundImage_EachRegisteredLocation_DecodesToReasonableFullFrameImage()
    {
        var service = CreateService();

        foreach (var id in ExpectedBackgroundIds)
        {
            var image = service.ResolveBackgroundImage(id);

            Assert.True(image is not null, $"Фон '{id}' не резолвится — проверь Assets/Backgrounds и backgrounds.json.");
            Assert.True(image!.PixelWidth > 200 && image.PixelHeight > 200,
                $"Подозрительно маленькое декодированное изображение фона '{id}': {image.PixelWidth}x{image.PixelHeight}.");
        }
    }

    [Fact]
    public void BackgroundRegistry_HasExactlyTheTwentyLocationIds_NoMoreNoFewer()
    {
        var backgrounds = LoadRealBackgroundRegistry();

        var actualIds = backgrounds.Select(b => b.BackgroundId).OrderBy(id => id).ToArray();
        var expectedIds = ExpectedBackgroundIds.OrderBy(id => id).ToArray();

        Assert.Equal(expectedIds, actualIds);
    }

    [Fact]
    public void ResolveBackgroundImage_UnknownBackgroundId_ReturnsNull_NoCrash()
    {
        var service = CreateService();

        Assert.Null(service.ResolveBackgroundImage("no_such_location"));
        Assert.Null(service.GetBackgroundDefinition("no_such_location"));
    }

    [Fact]
    public void ResolveBackgroundImage_IsCachedAcrossCalls_ReturnsSameInstance()
    {
        var service = CreateService();

        var first = service.ResolveBackgroundImage("dispatch_room_6");
        var second = service.ResolveBackgroundImage("dispatch_room_6");

        Assert.Same(first, second);
    }

    [Fact]
    public void ResolveBackgroundImage_PreservesNativeAspectRatio_ForNon16x9Source()
    {
        // crown_reception.jpg — исходник 1147x860 (не 16:9); декодер не должен ни обрезать,
        // ни растянуть пиксельные данные при загрузке — именно это и означает "сохранить
        // пропорции" на уровне файла (растяжение под экран делает ImageBrush при показе,
        // а не AssetService при загрузке).
        var service = CreateService();

        var image = service.ResolveBackgroundImage("crown_reception");

        Assert.NotNull(image);
        var ratio = (double)image!.PixelWidth / image.PixelHeight;
        Assert.InRange(ratio, 1.33, 1.34);
    }

    [Fact]
    public void CharactersAndBackgrounds_UseIndependentIdNamespaces_NoCrossResolution()
    {
        // Требование "раздельные реестры" — character-id не должен случайно резолвиться
        // через ResolveBackgroundImage и наоборот (разные словари/кэши внутри AssetService).
        var service = CreateService();

        Assert.Null(service.ResolveBackgroundImage("max_orlov"));
        Assert.Null(service.ResolveCharacterImage("main_menu"));
    }

    private static CharacterDefinition[] LoadRealCharacterRegistry()
    {
        var json = File.ReadAllText(CharactersJsonPath);
        return JsonSerializer.Deserialize<CharacterDefinition[]>(json, AssetJsonOptions.Create())!;
    }

    private static BackgroundDefinition[] LoadRealBackgroundRegistry()
    {
        var json = File.ReadAllText(BackgroundsJsonPath);
        return JsonSerializer.Deserialize<BackgroundDefinition[]>(json, AssetJsonOptions.Create())!;
    }
}
