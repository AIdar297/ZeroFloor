using ZeroFloor.Engine;
using ZeroFloor.Models;
using ZeroFloor.Services;

namespace ZeroFloor.Tests;

/// <summary>
/// Проверяет, что реальный демонстрационный сценарий (Data/Demo/animation_showcase.json,
/// используемый NovelViewModel по умолчанию) целиком проходим движком — обе ветки выбора,
/// переходы между всеми тремя сценами и возврат к началу (сценарий зациклен, концовок не
/// содержит). Дополняет статическую валидацию при загрузке живым прогоном через движок.
/// </summary>
public class AnimationShowcaseScenarioTests
{
    private static string ScenarioDirectory => Path.Combine(AppContext.BaseDirectory, "DemoScenario");

    private static GameSessionController CreateController()
    {
        var repository = ScenarioRepository.LoadFromDirectory(ScenarioDirectory);
        var sceneManager = new SceneManager(repository);
        var effectApplier = new EffectApplier();
        var dialogueManager = new DialogueManager(sceneManager, effectApplier);
        var conditionEvaluator = new ConditionEvaluator();
        var choiceProcessor = new ChoiceProcessor(conditionEvaluator, effectApplier);
        var endingManager = new EndingManager(conditionEvaluator);

        return new GameSessionController(dialogueManager, choiceProcessor, endingManager, new GameState());
    }

    [Fact]
    public void Repository_LoadsThreeScenes()
    {
        var repository = ScenarioRepository.LoadFromDirectory(ScenarioDirectory);

        Assert.Contains("DEMO_ANIM_1_ARRIVAL", repository.SceneIds);
        Assert.Contains("DEMO_ANIM_2_CAMERA", repository.SceneIds);
        Assert.Contains("DEMO_ANIM_3_FINALE", repository.SceneIds);
    }

    [Theory]
    [InlineData("DEMO1_MOVE", "DEMO3_RESTART")]
    [InlineData("DEMO1_MOVE", "DEMO3_STAY")]
    [InlineData("DEMO1_SCALE", "DEMO3_RESTART")]
    [InlineData("DEMO1_SCALE", "DEMO3_STAY")]
    public void FullWalkthrough_AllBranchCombinations_LoopBackToStartWithoutErrors(
        string scene1ChoiceId, string scene3ChoiceId)
    {
        var controller = CreateController();
        controller.Start(NovelViewModel_DefaultStartSceneId);

        // Сцена 1: N001 -> N002 -> N003 (выбор) -> N004_* (узел с фидбеком) -> смена сцены
        controller.Advance();
        controller.Advance();
        Assert.True(controller.GetAvailableChoices().Count == 2);
        controller.SelectChoice(scene1ChoiceId);
        controller.Advance(); // N004_* -> DEMO_ANIM_2_CAMERA

        Assert.Equal("DEMO_ANIM_2_CAMERA", controller.State.CurrentSceneId);

        // Сцена 2: N001 -> N002 -> N003 -> N004 -> N005 -> смена сцены
        controller.Advance();
        controller.Advance();
        controller.Advance();
        controller.Advance();
        controller.Advance();

        Assert.Equal("DEMO_ANIM_3_FINALE", controller.State.CurrentSceneId);

        // Сцена 3: N001 -> N002 -> N003 (выбор)
        controller.Advance();
        controller.Advance();
        Assert.Equal(2, controller.GetAvailableChoices().Count);
        controller.SelectChoice(scene3ChoiceId);

        // Обе ветки в итоге возвращают в начало — сценарий зациклен, без концовок.
        if (scene3ChoiceId == "DEMO3_STAY")
            controller.Advance(); // N004 -> снова DEMO_ANIM_1_ARRIVAL

        Assert.Equal("DEMO_ANIM_1_ARRIVAL", controller.State.CurrentSceneId);
        Assert.Equal("N001", controller.CurrentNode.NodeId);
        Assert.False(controller.HasEnded);
    }

    private const string NovelViewModel_DefaultStartSceneId = "DEMO_ANIM_1_ARRIVAL";
}
