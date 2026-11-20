using ZeroFloor.Models;
using ZeroFloor.Services;

namespace ZeroFloor.Engine;

/// <summary>
/// Система сцен и переходов между ними (requirements 1 и 4). Не содержит сюжетных правил —
/// только навигацию по графу сцен, с защитой от обращения к несуществующему id.
/// </summary>
public sealed class SceneManager
{
    private readonly IScenarioRepository _repository;

    public SceneManager(IScenarioRepository repository) => _repository = repository;

    public SceneDefinition GetScene(string sceneId)
    {
        if (string.IsNullOrWhiteSpace(sceneId))
            throw new ScenarioValidationException("Не указан идентификатор сцены.");

        return _repository.GetScene(sceneId)
            ?? throw new ScenarioValidationException($"Сцена с id '{sceneId}' не найдена в загруженном сценарии.");
    }

    /// <summary>Входной узел сцены — по соглашению (PROJECT_STRUCTURE.md) первый узел в списке.</summary>
    public DialogueNode GetEntryNode(SceneDefinition scene)
    {
        if (scene.Nodes.Count == 0)
            throw new ScenarioValidationException($"Сцена '{scene.SceneId}' не содержит ни одного узла.");

        return scene.Nodes[0];
    }
}
