using ZeroFloor.Engine;
using ZeroFloor.Models;
using ZeroFloor.Services;

namespace ZeroFloor.Tests;

/// <summary>
/// Сквозной (интеграционный) прогон движка на тестовом сценарии из Fixtures/sample_scenario.json:
/// сцена → узел → выбор с эффектами → переход в следующую сцену → узел со скрытой проверкой
/// флага → терминальный узел; отдельно — фиксация концовки через EndingManager.
/// </summary>
public class GameSessionControllerTests
{
    private static GameSessionController CreateController(GameState? state = null)
    {
        var repository = new ScenarioRepository(new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample_scenario.json")
        });

        var sceneManager = new SceneManager(repository);
        var effectApplier = new EffectApplier();
        var dialogueManager = new DialogueManager(sceneManager, effectApplier);
        var conditionEvaluator = new ConditionEvaluator();
        var choiceProcessor = new ChoiceProcessor(conditionEvaluator, effectApplier);
        var endingManager = new EndingManager(conditionEvaluator);

        return new GameSessionController(dialogueManager, choiceProcessor, endingManager, state ?? new GameState());
    }

    [Fact]
    public void Start_EntersFirstSceneAtEntryNode()
    {
        var controller = CreateController();

        controller.Start("TEST_SCENE_1");

        Assert.Equal("N001", controller.CurrentNode.NodeId);
        Assert.Equal("TEST_SCENE_1", controller.CurrentScene.SceneId);
        Assert.False(controller.HasEnded);
    }

    [Fact]
    public void Resume_EntersAtSpecificNode()
    {
        var controller = CreateController();

        controller.Resume("TEST_SCENE_1", "N002");

        Assert.Equal("N002", controller.CurrentNode.NodeId);
        Assert.Equal("TEST_SCENE_1", controller.State.CurrentSceneId);
    }

    [Fact]
    public void Advance_OnNodeWithoutChoices_MovesToNextNode()
    {
        var controller = CreateController();
        controller.Start("TEST_SCENE_1");

        controller.Advance();

        Assert.Equal("N002", controller.CurrentNode.NodeId);
    }

    [Fact]
    public void Advance_OnNodeWithChoices_Throws()
    {
        var controller = CreateController();
        controller.Start("TEST_SCENE_1");
        controller.Advance(); // N002 — узел с выбором

        Assert.Throws<ScenarioValidationException>(() => controller.Advance());
    }

    [Fact]
    public void FullWalkthrough_BraveChoice_UnlocksConditionalChoiceInNextScene()
    {
        var controller = CreateController();
        controller.Start("TEST_SCENE_1");
        controller.Advance(); // N001 -> N002 (узел с выбором)

        controller.SelectChoice("C001_BRAVE"); // +5 H, flag test_brave_chosen=true, -> N003
        Assert.Equal(65, controller.State.GetStat(StatId.H));

        controller.Advance(); // N003 -> смена сцены TEST_SCENE_2

        Assert.Equal("TEST_SCENE_2", controller.State.CurrentSceneId);

        var available = controller.GetAvailableChoices();
        Assert.Equal(2, available.Count); // скрытая проверка флага пропустила оба варианта

        controller.SelectChoice("C002_ONLY_IF_BRAVE");

        Assert.Equal(FlagValue.FromBoolean(true), controller.State.GetFlag("test_scene2_done"));
        Assert.Equal("N002", controller.CurrentNode.NodeId);
    }

    [Fact]
    public void FullWalkthrough_CautiousChoice_HidesConditionalChoiceInNextScene()
    {
        var controller = CreateController();
        controller.Start("TEST_SCENE_1");
        controller.Advance();

        controller.SelectChoice("C001_CAUTIOUS"); // -5 H, флаг НЕ ставится
        Assert.Equal(55, controller.State.GetStat(StatId.H));

        controller.Advance();

        var available = controller.GetAvailableChoices();

        Assert.Single(available);
        Assert.Equal("C002_ALWAYS", available[0].ChoiceId);
    }

    [Fact]
    public void SelectChoice_UnavailableDueToHiddenCheck_Throws()
    {
        var controller = CreateController();
        controller.Start("TEST_SCENE_1");
        controller.Advance();
        controller.SelectChoice("C001_CAUTIOUS"); // test_brave_chosen остаётся не установлен
        controller.Advance();

        Assert.Throws<ScenarioValidationException>(() => controller.SelectChoice("C002_ONLY_IF_BRAVE"));
    }

    [Fact]
    public void Ending_ResolvedThroughController_SetsHasEndedAndStopsFurtherActions()
    {
        var state = new GameState();
        state.SetStat(StatId.K, 100);
        var controller = CreateController(state);
        controller.Start("TEST_SCENE_1");

        var resolved = controller.ResolveEnding("B");

        Assert.Equal("END_01_COLLAPSE", resolved);
        Assert.True(controller.HasEnded);
        Assert.Throws<ScenarioValidationException>(() => controller.Advance());
        Assert.Throws<ScenarioValidationException>(() => controller.ResolveEnding("A"));
    }

    [Fact]
    public void Ending_AvailableBranches_ReflectCurrentState()
    {
        var state = new GameState(); // K=50 по умолчанию — ветка B (K>=40) доступна
        var controller = CreateController(state);
        controller.Start("TEST_SCENE_1");

        var branches = controller.GetAvailableEndingBranches();

        Assert.Contains("A", branches);
        Assert.Contains("B", branches);
        Assert.DoesNotContain("C", branches);
        Assert.DoesNotContain("D", branches);
    }

    [Fact]
    public void SelectChoice_RecordsChoiceHistoryEntry_OnlyOncePerIdempotentReplay()
    {
        var controller = CreateController();
        controller.Start("TEST_SCENE_1");
        controller.Advance();

        controller.SelectChoice("C001_BRAVE");

        var entry = Assert.Single(controller.State.ChoiceHistory);
        Assert.Equal("C001_BRAVE", entry.ChoiceId);
        Assert.Equal("TEST_SCENE_1", entry.SceneId);
        Assert.Equal("N002", entry.NodeId);
        Assert.Equal("[Тест] Рискованный вариант", entry.ChoiceText);
        Assert.Equal(65, controller.State.GetStat(StatId.H));

        // Имитация повторной загрузки сейва, сделанного НА узле N002 ПОСЛЕ применения выбора
        // (appliedChoiceIds/история в GameState уже восстановлены из сейва ровно в этом виде) —
        // повторный выбор того же варианта на том же узле не должен ни начислить эффект
        // повторно, ни задвоить запись истории (PROJECT_ANALYSIS, T12).
        var resumedController = CreateController(controller.State);
        resumedController.Resume("TEST_SCENE_1", "N002");
        resumedController.SelectChoice("C001_BRAVE");

        Assert.Equal(65, resumedController.State.GetStat(StatId.H));
        Assert.Single(resumedController.State.ChoiceHistory);
    }

    [Fact]
    public void Start_And_SceneTransition_RaiseAutoSaveRequested_WithChapterEnteredTrigger()
    {
        var controller = CreateController();
        var triggers = new List<AutoSaveTrigger>();
        controller.AutoSaveRequested += triggers.Add;

        controller.Start("TEST_SCENE_1");
        Assert.Equal(new[] { AutoSaveTrigger.ChapterEntered }, triggers);

        controller.Advance();
        controller.SelectChoice("C001_BRAVE");
        controller.Advance(); // N003 -> переход в TEST_SCENE_2

        Assert.Equal(new[] { AutoSaveTrigger.ChapterEntered, AutoSaveTrigger.ChapterEntered }, triggers);
    }

    [Fact]
    public void GetAvailableEndingBranches_RaisesAutoSaveRequested_WithBeforeFinalChoiceTrigger()
    {
        var controller = CreateController();
        var triggers = new List<AutoSaveTrigger>();
        controller.Start("TEST_SCENE_1");
        controller.AutoSaveRequested += triggers.Add;

        controller.GetAvailableEndingBranches();

        Assert.Equal(new[] { AutoSaveTrigger.BeforeFinalChoice }, triggers);
    }
}
