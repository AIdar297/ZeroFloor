using ZeroFloor.Engine;
using ZeroFloor.Models;

namespace ZeroFloor.Tests;

/// <summary>
/// Исчерпывающая проверка развилок Глав 1–5, перенесённых дословно из
/// Zero_Floor_Full_Script.docx (редакция 2.2): все три варианта тона Главы 1, оба исхода
/// Выбора 1, оба маршрута мимо дрона, полная таблица gosha_trust (0/1/2 попыток торга ×
/// заплатить/украсть), оба способа работы с панелью в Главе 3, оба исхода Выбора 3, все три
/// входа в разговор с Виктором (Глава 4), помощь/отказ сокамернику, и — главное — точная
/// формула скрытого индикатора suspicion из ПРИЛОЖЕНИЯ 2.2 документа (старт 3, логичный −1,
/// нелогичный +2, Выбор 5: отрицать +1 / сотрудничать −2, порог показа CH05_SC05 — suspicion ≥ 6).
/// Использует внутренние хелперы RealScenarioContentTests.
/// </summary>
public class Chapters1To5ExhaustiveTests
{
    [Theory]
    [InlineData("CH01_SC01_C001_A")]
    [InlineData("CH01_SC01_C001_B")]
    [InlineData("CH01_SC01_C001_C")]
    public void Chapter1_AllThreeToneChoices_AreCosmeticAndLeadToTheSameDecision(string toneChoiceId)
    {
        var controller = RealScenarioContentTests.CreateController();
        controller.Start("CH01_SC01");
        RealScenarioContentTests.AdvanceToChoice(controller);

        controller.SelectChoice(toneChoiceId);
        RealScenarioContentTests.AdvanceToChoice(controller);

        var available = controller.GetAvailableChoices();
        Assert.Equal(2, available.Count);
        Assert.Contains(available, c => c.ChoiceId == "CH01_SC03_C001_A");
        Assert.Contains(available, c => c.ChoiceId == "CH01_SC03_C001_B");
        Assert.Equal(50, controller.State.GetStat(StatId.K));
        Assert.Equal(50, controller.State.GetStat(StatId.L));
        Assert.Equal(50, controller.State.GetStat(StatId.R));
        Assert.Equal(60, controller.State.GetStat(StatId.H));
    }

    [Fact]
    public void Chapter1_HackNow_AddsFiveHumanity_WaitQueue_SubtractsFive()
    {
        var hackController = RealScenarioContentTests.CreateController();
        hackController.Start("CH01_SC01");
        RealScenarioContentTests.SelectAndAdvance(hackController, "CH01_SC01_C001_A");
        RealScenarioContentTests.SelectAndAdvance(hackController, "CH01_SC03_C001_A");
        Assert.Equal(65, hackController.State.GetStat(StatId.H));

        var waitController = RealScenarioContentTests.CreateController();
        waitController.Start("CH01_SC01");
        RealScenarioContentTests.SelectAndAdvance(waitController, "CH01_SC01_C001_B");
        RealScenarioContentTests.SelectAndAdvance(waitController, "CH01_SC03_C001_B");
        Assert.Equal(55, waitController.State.GetStat(StatId.H));
    }

    [Theory]
    [InlineData(55, "CH02_SC01_C_KLAVDIA_A")] // ждал очереди -> повторное сообщение от тётки Клавдии
    [InlineData(65, "CH02_SC01_C_KLAVDIA_B")] // взломал сразу -> без повторного сообщения
    public void Chapter2_KlavdiaMessageGate_FollowsChapter1Humanity(int startingH, string expectedVisibleChoice)
    {
        var state = new GameState();
        state.SetStat(StatId.H, startingH);
        var controller = RealScenarioContentTests.CreateController(state);
        controller.Start("CH02_SC01");

        var available = controller.GetAvailableChoices();
        Assert.Single(available);
        Assert.Equal(expectedVisibleChoice, available[0].ChoiceId);
    }

    [Fact]
    public void Chapter2_Drone_BothRoutes_ReachGoshaStallWithNoStatChange()
    {
        foreach (var droneChoice in new[] { "CH02_SC01_C001_A", "CH02_SC01_C001_B" })
        {
            var state = new GameState();
            state.SetStat(StatId.H, 65); // минуя недетерминированный гейт тётки Клавдии
            var controller = RealScenarioContentTests.CreateController(state);
            controller.Start("CH02_SC01");
            RealScenarioContentTests.SelectAndAdvance(controller, "CH02_SC01_C_KLAVDIA_B");
            RealScenarioContentTests.SelectAndAdvance(controller, droneChoice);
            RealScenarioContentTests.AdvanceToChoice(controller);

            Assert.Equal("CH02_SC02", controller.State.CurrentSceneId);
            Assert.Equal(65, controller.State.GetStat(StatId.H));
        }
    }

    /// <summary>
    /// Полная таблица из документа (Глава 2): gosha_trust = true, если заплачено честно И
    /// сбито не более одного раза; false — при краже или при агрессивном торге (два и более раз).
    /// </summary>
    [Theory]
    [InlineData(0, "PAY", true, 65, 50)]
    [InlineData(1, "PAY", true, 65, 50)]
    [InlineData(2, "PAY", false, 65, 50)]
    [InlineData(0, "STEAL", false, 50, 65)]
    [InlineData(1, "STEAL", false, 50, 65)]
    [InlineData(2, "STEAL", false, 50, 65)]
    public void Chapter2_GoshaTrust_FollowsHaggleCountAndPaymentMethod(
        int haggleAttempts, string finalAction, bool expectedGoshaTrust, int expectedH, int expectedR)
    {
        var controller = RealScenarioContentTests.CreateController();
        controller.Start("CH02_SC02");
        RealScenarioContentTests.AdvanceToChoice(controller);

        if (haggleAttempts == 0)
        {
            controller.SelectChoice("CH02_SC02_C001_A");
        }
        else
        {
            controller.SelectChoice("CH02_SC02_C001_B");
            RealScenarioContentTests.AdvanceToChoice(controller);
            controller.SelectChoice(haggleAttempts == 1 ? "CH02_SC02_C002_B" : "CH02_SC02_C002_A");
        }

        var finalChoiceId = finalAction == "PAY" ? "CH02_SC02_C003D_PAY" : "CH02_SC02_C003D_STEAL";
        if (haggleAttempts == 1)
            finalChoiceId = finalAction == "PAY" ? "CH02_SC02_C003O_PAY" : "CH02_SC02_C003O_STEAL";
        else if (haggleAttempts == 2)
            finalChoiceId = finalAction == "PAY" ? "CH02_SC02_C003T_PAY" : "CH02_SC02_C003T_STEAL";

        RealScenarioContentTests.AdvanceToChoice(controller);
        controller.SelectChoice(finalChoiceId);

        Assert.Equal(expectedGoshaTrust, controller.State.GetFlag("gosha_trust")!.Value.BooleanValue);
        Assert.Equal(expectedH, controller.State.GetStat(StatId.H));
        Assert.Equal(expectedR, controller.State.GetStat(StatId.R));

        RealScenarioContentTests.AdvanceToChoice(controller);
        Assert.Equal("CH02_SC03", controller.State.CurrentSceneId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Chapter2_DanWarning_SetsDanWarnedHeededFlagExactly(bool heeded)
    {
        var controller = RealScenarioContentTests.CreateController();
        controller.Start("CH02_SC03");
        RealScenarioContentTests.AdvanceToChoice(controller);
        controller.SelectChoice(heeded ? "CH02_SC03_C001_A" : "CH02_SC03_C001_B");

        Assert.Equal(heeded, controller.State.GetFlag("dan_warned_heeded")!.Value.BooleanValue);
    }

    [Fact]
    public void Chapter2_NeighborSideEvent_HelpAddsFiveHumanity_PassByDoesNotChangeAnything()
    {
        var helpController = RealScenarioContentTests.CreateController();
        helpController.Start("CH02_SC_SIDE01");
        helpController.SelectChoice("CH02_SC_SIDE01_C001_A");
        Assert.Equal(65, helpController.State.GetStat(StatId.H));
        helpController.Advance();
        Assert.Equal("CH03_SC01", helpController.State.CurrentSceneId);

        var passController = RealScenarioContentTests.CreateController();
        passController.Start("CH02_SC_SIDE01");
        passController.SelectChoice("CH02_SC_SIDE01_C001_B");
        Assert.Equal(60, passController.State.GetStat(StatId.H));
        passController.Advance();
        Assert.Equal("CH03_SC01", passController.State.CurrentSceneId);
    }

    [Fact]
    public void Chapter3_BreachPanel_ReachesLogsWithoutStatPenalty()
    {
        // Текстовый выбор "работать быстро/аккуратно" заменён мини-игрой "Взлом лифтового
        // узла" (презентационный слой, см. WirePuzzleViewModel/MinigameCue) — сам движок её
        // не видит: узел с minigameCue не содержит выборов, Advance() проходит сквозь него
        // как через обычную реплику. Успех мини-игры не меняет K/L/R/H — ровно как было
        // с прежним флейвор-выбором.
        var controller = RealScenarioContentTests.CreateController();
        controller.Start("CH03_SC02");
        RealScenarioContentTests.AdvanceToChoice(controller);

        Assert.Equal(50, controller.State.GetStat(StatId.K));
        Assert.Equal(50, controller.State.GetStat(StatId.L));
        Assert.Equal(50, controller.State.GetStat(StatId.R));
        Assert.Equal(60, controller.State.GetStat(StatId.H));

        // Следующая реальная развилка — Выбор 3 в CH03_SC04.
        Assert.Equal("CH03_SC04", controller.State.CurrentSceneId);
    }

    [Theory]
    [InlineData("CH03_SC04_C001_A", true)]
    [InlineData("CH03_SC04_C001_B", false)]
    public void Chapter3_ShardChoice_SetsShardObtainedFlagExactly(string choiceId, bool expectedShardObtained)
    {
        var controller = RealScenarioContentTests.CreateController();
        controller.Start("CH03_SC04");
        controller.SelectChoice(choiceId);

        Assert.Equal(expectedShardObtained, controller.State.GetFlag("shard_obtained")!.Value.BooleanValue);

        // Побочное событие 5 (журналист) — гейт по shard_obtained; всегда требует явного
        // продолжения, независимо от того, доступна ли встреча в этом прохождении.
        RealScenarioContentTests.AdvanceToChoice(controller);
        if (expectedShardObtained)
        {
            controller.SelectChoice("C_JOURNALIST_AVAILABLE_GATE");
            RealScenarioContentTests.AdvanceToChoice(controller);
            controller.SelectChoice("C_JOURNALIST_DECLINE");
        }
        else
        {
            controller.SelectChoice("C_JOURNALIST_UNAVAILABLE_GATE");
        }

        RealScenarioContentTests.AdvanceToChoice(controller);
        Assert.Equal("CH04_SC01", controller.State.CurrentSceneId);
    }

    [Theory]
    [InlineData("CH04_SC01_C001_A")] // «Кто ты вообще такой?»
    [InlineData("CH04_SC01_C001_B")] // «Что такое «коэффициент стабилизации»?»
    [InlineData("CH04_SC01_C001_C")] // «Зачем тебе я?»
    public void Chapter4_AllThreeOpeningQuestions_EventuallyReachChoice4WithNoStatChangeYet(string openingChoiceId)
    {
        var controller = RealScenarioContentTests.CreateController();
        controller.Start("CH04_SC01");
        RealScenarioContentTests.AdvanceToChoice(controller);
        controller.SelectChoice(openingChoiceId);
        RealScenarioContentTests.AdvanceToChoice(controller);

        // Независимо от того, в каком порядке Макс расспрашивает Виктора, ни одна из трёх
        // веток узла 1 сама по себе не меняет K/L/R/H — шкалы трогает только финальный Выбор 4.
        Assert.Equal(50, controller.State.GetStat(StatId.K));
        Assert.Equal(50, controller.State.GetStat(StatId.L));
    }

    [Theory]
    [InlineData("CH04_SC01_C005_A", true, 65, 45)] // открыто: +15K/-5L, korni_open=true
    [InlineData("CH04_SC01_C005_B", false, 55, 50)] // дистанция: +5K
    public void Chapter4_Choice4_SetsKorniOpenAndStats(string choiceId, bool expectedKorniOpen, int expectedK, int expectedL)
    {
        var controller = RealScenarioContentTests.CreateController();
        controller.Start("CH04_SC01");
        RealScenarioContentTests.SelectAndAdvance(controller, "CH04_SC01_C001_A");
        RealScenarioContentTests.SelectAndAdvance(controller, "CH04_SC01_C002_A");
        RealScenarioContentTests.SelectAndAdvance(controller, "CH04_SC01_C003_B");
        RealScenarioContentTests.SelectAndAdvance(controller, "CH04_SC01_C004_A");
        RealScenarioContentTests.SelectAndAdvance(controller, choiceId);

        Assert.Equal(expectedKorniOpen, controller.State.GetFlag("korni_open")!.Value.BooleanValue);
        Assert.Equal(expectedK, controller.State.GetStat(StatId.K));
        Assert.Equal(expectedL, controller.State.GetStat(StatId.L));
        RealScenarioContentTests.AdvanceToChoice(controller);
        Assert.Equal("CH05_SC01", controller.State.CurrentSceneId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Chapter5_CellmateNote_SetsCellmateHelpedFlagExactly(bool helped)
    {
        var controller = RealScenarioContentTests.CreateController();
        controller.Start("CH05_SC02");
        RealScenarioContentTests.AdvanceToChoice(controller);
        controller.SelectChoice(helped ? "CH05_SC02_C001_A" : "CH05_SC02_C001_B");

        Assert.Equal(helped, controller.State.GetFlag("cellmate_helped")!.Value.BooleanValue);
        RealScenarioContentTests.AdvanceToChoice(controller);
        Assert.Equal("CH05_SC03", controller.State.CurrentSceneId);
    }

    /// <summary>
    /// Прямая проверка формулы ПРИЛОЖЕНИЯ 2.2: suspicion стартует с 3, логичный ответ -1,
    /// нелогичный +2; после Выбора 5 — отрицать +1, сотрудничать -2. CH05_SC05 показывается
    /// только при итоговом suspicion ≥ 6. Три комбинации из самого документа (раздел 2.2,
    /// "Проверка примеров") воспроизведены буквально.
    /// </summary>
    [Fact]
    public void Chapter5_Suspicion_ThreeLogicalAnswersPlusDeny_Equals1_Sc05DoesNotAppear()
    {
        var controller = PlayInterrogation(logical1: true, logical2: true, logical3: true, deny: true);
        Assert.Equal(1, controller.State.GetFlag("suspicion")!.Value.IntegerValue);
        Assert.Equal("CH06_SHAFTS_CHASE", controller.State.CurrentSceneId);
    }

    [Fact]
    public void Chapter5_Suspicion_TwoIllogicalOneLogicalPlusDeny_Equals7_Sc05Appears()
    {
        // Документ (раздел 2.2): "два нелогичных и один логичный ответ плюс отрицание дают 7 —
        // сцена появляется". Порядок логичный/нелогичный/нелогичный даёт ту же сумму (3+2-1+2=6,
        // +1 за отрицание = 7), поскольку сумма не зависит от порядка ответов.
        var controller = PlayInterrogation(logical1: false, logical2: true, logical3: false, deny: true);
        Assert.Equal(7, controller.State.GetFlag("suspicion")!.Value.IntegerValue);
        Assert.Equal("CH05_SC05", controller.State.CurrentSceneId);
    }

    [Fact]
    public void Chapter5_Suspicion_ThreeLogicalAnswersPlusPartialCooperation_Equals_Negative2_Sc05DoesNotAppear()
    {
        // ОТКРЫТЫЙ ВОПРОС (см. список противоречий): документ (раздел 2.2) по формуле
        // (старт 3, три логичных по −1 = 0, частичное сотрудничество −2) должен давать −2, но
        // его собственный проверочный пример в том же разделе утверждает "дают 0". Остальные
        // два примера того же абзаца формуле не противоречат. Код следует формуле (она
        // самосогласованна для 2 из 3 примеров); расхождение вынесено в список вопросов.
        var controller = PlayInterrogation(logical1: true, logical2: true, logical3: true, deny: false);
        Assert.Equal(-2, controller.State.GetFlag("suspicion")!.Value.IntegerValue);
        Assert.Equal("CH06_SHAFTS_CHASE", controller.State.CurrentSceneId);
    }

    [Fact]
    public void Chapter5_Suspicion_ThreeIllogicalAnswersPlusDeny_Equals10_Sc05Appears()
    {
        var controller = PlayInterrogation(logical1: false, logical2: false, logical3: false, deny: true);
        Assert.Equal(10, controller.State.GetFlag("suspicion")!.Value.IntegerValue);
        Assert.Equal("CH05_SC05", controller.State.CurrentSceneId);
    }

    [Fact]
    public void Chapter5_SonyaExposed_SetByNamingInformant_OnlyWhenSc05Shown()
    {
        var controller = PlayInterrogation(logical1: false, logical2: false, logical3: false, deny: true);
        Assert.Equal("CH05_SC05", controller.State.CurrentSceneId);

        controller.SelectChoice("CH05_SC05_C001_A"); // назвать информатора
        Assert.True(controller.State.GetFlag("sonya_exposed")!.Value.BooleanValue);
        RealScenarioContentTests.AdvanceToChoice(controller);
        Assert.Equal("CH06_SHAFTS_CHASE", controller.State.CurrentSceneId);
    }

    private static GameSessionController PlayInterrogation(bool logical1, bool logical2, bool logical3, bool deny)
    {
        var controller = RealScenarioContentTests.CreateController();
        controller.Start("CH05_SC03");
        RealScenarioContentTests.AdvanceToChoice(controller);

        controller.SelectChoice(logical1 ? "CH05_SC03_C001_A" : "CH05_SC03_C001_B");
        RealScenarioContentTests.AdvanceToChoice(controller);

        var q2Choices = controller.GetAvailableChoices();
        var q2ChoiceId = logical2
            ? q2Choices.First(c => c.ChoiceId.StartsWith("CH05_SC03_C002_A")).ChoiceId
            : q2Choices.First(c => c.ChoiceId.StartsWith("CH05_SC03_C002_B")).ChoiceId;
        controller.SelectChoice(q2ChoiceId);

        var q3Choices = controller.GetAvailableChoices();
        var q3ChoiceId = logical3
            ? q3Choices.First(c => c.ChoiceId.StartsWith("CH05_SC03_C003_A")).ChoiceId
            : q3Choices.First(c => c.ChoiceId.StartsWith("CH05_SC03_C003_B")).ChoiceId;
        controller.SelectChoice(q3ChoiceId);
        RealScenarioContentTests.AdvanceToChoice(controller);

        var v5Choices = controller.GetAvailableChoices();
        var v5ChoiceId = deny
            ? v5Choices.First(c => c.ChoiceId.Contains("DENY")).ChoiceId
            : v5Choices.First(c => c.ChoiceId.Contains("PARTIAL")).ChoiceId;
        controller.SelectChoice(v5ChoiceId);

        RealScenarioContentTests.AdvanceToChoice(controller);
        return controller;
    }
}
