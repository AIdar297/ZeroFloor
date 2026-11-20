using ZeroFloor.Engine;
using ZeroFloor.Models;
using ZeroFloor.Services;
using ZeroFloor.ViewModels;

namespace ZeroFloor.Tests;

/// <summary>
/// Регрессионный тест на конкретный найденный баг: App.xaml.cs грузил сценарий из Data/Demo,
/// а NovelViewModel.DefaultStartSceneId указывал на демонстрационную сцену — в результате
/// "Новая игра" никогда не показывала реальный сюжет (Главы 1+), реальные фоны и реальных
/// персонажей, даже когда все они уже были готовы в Data/Scenario и Assets/. Этот тест
/// воспроизводит РОВНО тот же путь загрузки, что App.xaml.cs.LoadScenarioRepository()
/// (каталог Data/Scenario, относительно AppContext.BaseDirectory) и ровно то же разрешение
/// ассетов, что AssetService в композиционном корне (Data/Characters, Data/Backgrounds,
/// AssetsRootDirectory = AppContext.BaseDirectory) — чтобы поломка именно этой цепочки
/// («данные готовы, но реальная игра их не грузит») не прошла незамеченной снова.
/// </summary>
public class AppStartupIntegrationTests
{
    private static string BaseDirectory => AppContext.BaseDirectory;

    [Fact]
    public void ProductionScenarioDirectory_ContainsDefaultStartScene()
    {
        var repository = ScenarioRepository.LoadFromDirectory(Path.Combine(BaseDirectory, "Data", "Scenario"));

        var entryScene = repository.GetScene(NovelViewModel.DefaultStartSceneId);

        Assert.NotNull(entryScene);
        Assert.NotNull(entryScene!.Background);
        Assert.NotEmpty(entryScene.Nodes);
    }

    [Fact]
    public void DefaultStartScene_IsRealChapterOne_NotTheAnimationDemo()
    {
        // Если это когда-нибудь снова станет DEMO_ANIM_1_ARRIVAL (или любой другой демо-id),
        // "Новая игра" снова покажет витрину анимаций вместо Главы 1 — именно это произошло
        // в найденном баге.
        Assert.Equal("CH01_SC01", NovelViewModel.DefaultStartSceneId);
        Assert.DoesNotContain("DEMO", NovelViewModel.DefaultStartSceneId, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultStartScene_BackgroundAndEverySpeakingCharacter_ResolveToRealImages_UsingProductionPaths()
    {
        var repository = ScenarioRepository.LoadFromDirectory(Path.Combine(BaseDirectory, "Data", "Scenario"));
        var assets = new AssetService(
            Path.Combine(BaseDirectory, "Data", "Characters", "characters.json"),
            Path.Combine(BaseDirectory, "Data", "Backgrounds", "backgrounds.json"),
            BaseDirectory);

        var entryScene = repository.GetScene(NovelViewModel.DefaultStartSceneId)!;

        var backgroundImage = assets.ResolveBackgroundImage(entryScene.Background!);
        Assert.True(backgroundImage is not null,
            $"Фон '{entryScene.Background}' стартовой сцены не резолвится в реальное изображение через производственные пути AssetService.");

        var speakers = entryScene.Nodes
            .Select(n => n.Speaker)
            .Where(speaker => speaker != "narrator")
            .Distinct();

        foreach (var speakerId in speakers)
        {
            var portrait = assets.ResolveCharacterImage(speakerId);
            Assert.True(portrait is not null,
                $"Портрет персонажа '{speakerId}', говорящего в стартовой сцене, не резолвится через производственные пути AssetService.");
        }
    }

    [Fact]
    public void FullProductionScenario_AllTwelveChaptersPlusEndings_LoadTogetherWithoutValidationErrors()
    {
        // Весь Data/Scenario целиком — ровно то, что реально грузит App.xaml.cs при старте
        // (не отдельные файлы по одному, как в прочих тестах) — один общий репозиторий,
        // как это происходит в живом приложении.
        var repository = ScenarioRepository.LoadFromDirectory(Path.Combine(BaseDirectory, "Data", "Scenario"));

        Assert.True(repository.SceneIds.Count >= 28);
    }
}
