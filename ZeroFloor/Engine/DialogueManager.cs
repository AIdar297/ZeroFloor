using ZeroFloor.Models;

namespace ZeroFloor.Engine;

/// <summary>
/// Система диалоговых реплик и переходов внутри сцены (requirements 2 и 4). Держит указатель
/// на текущую сцену/узел, применяет узловые эффекты при входе и защищает от перехода
/// на несуществующий узел или узел без текста реплики.
/// </summary>
public sealed class DialogueManager
{
    private readonly SceneManager _sceneManager;
    private readonly EffectApplier _effectApplier;

    public DialogueManager(SceneManager sceneManager, EffectApplier effectApplier)
    {
        _sceneManager = sceneManager;
        _effectApplier = effectApplier;
    }

    public SceneDefinition CurrentScene { get; private set; } = null!;
    public DialogueNode CurrentNode { get; private set; } = null!;

    public void EnterScene(string sceneId, GameState state)
    {
        CurrentScene = _sceneManager.GetScene(sceneId);
        state.CurrentChapterId = CurrentScene.SceneId;
        EnterNode(_sceneManager.GetEntryNode(CurrentScene), state);
    }

    /// <summary>Входит в сцену сразу на указанном узле, минуя входной узел — для возобновления
    /// сохранённой позиции (повторный показ входного узла и повторное применение его эффектов
    /// в этом случае было бы некорректным дублированием).</summary>
    public void EnterSceneAt(string sceneId, string nodeId, GameState state)
    {
        CurrentScene = _sceneManager.GetScene(sceneId);
        state.CurrentChapterId = CurrentScene.SceneId;

        var node = CurrentScene.Nodes.FirstOrDefault(n => n.NodeId == nodeId)
            ?? throw new ScenarioValidationException(
                $"Узел '{nodeId}' не найден в сцене '{CurrentScene.SceneId}' (возобновление сохранения).");

        EnterNode(node, state);
    }

    public void GoToNode(string nodeId, GameState state)
    {
        if (CurrentScene is null)
            throw new ScenarioValidationException("Нет текущей сцены — сначала нужно войти в сцену (EnterScene).");

        var node = CurrentScene.Nodes.FirstOrDefault(n => n.NodeId == nodeId)
            ?? throw new ScenarioValidationException(
                $"Узел '{nodeId}' не найден в сцене '{CurrentScene.SceneId}'.");

        EnterNode(node, state);
    }

    private void EnterNode(DialogueNode node, GameState state)
    {
        if (string.IsNullOrWhiteSpace(node.Text))
            throw new ScenarioValidationException(
                $"Узел '{node.NodeId}' сцены '{CurrentScene.SceneId}' не содержит текста реплики.");

        if (string.IsNullOrWhiteSpace(node.Speaker))
            throw new ScenarioValidationException(
                $"Узел '{node.NodeId}' сцены '{CurrentScene.SceneId}' не содержит говорящего (speaker).");

        CurrentNode = node;
        state.CurrentSceneId = CurrentScene.SceneId;
        state.CurrentNodeId = node.NodeId;
        state.MarkNodeViewed(node.NodeId);

        // Узловые эффекты применяются при каждом входе в узел (например, побочные события
        // главы) — в отличие от эффектов выбора, они не защищены appliedChoiceIds: узел
        // сам по себе не является "выбором игрока", он не должен повторно начисляться только
        // если сценарные данные сами по себе идемпотентны (setFlag/setStat) либо узел не
        // посещается повторно в рамках одного прохождения.
        _effectApplier.Apply(node.Effects, state);
    }
}
