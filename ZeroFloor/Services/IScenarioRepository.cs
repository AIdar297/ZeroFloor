using ZeroFloor.Models;

namespace ZeroFloor.Services;

/// <summary>Загружает и отдаёт провалидированные сцены сценария по их id.</summary>
public interface IScenarioRepository
{
    SceneDefinition? GetScene(string sceneId);
    IReadOnlyCollection<string> SceneIds { get; }
}
