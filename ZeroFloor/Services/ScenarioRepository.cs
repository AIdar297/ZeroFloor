using System.IO;
using System.Text.Json;
using ZeroFloor.Engine;
using ZeroFloor.Models;
using ZeroFloor.Services.Json;

namespace ZeroFloor.Services;

/// <summary>
/// Загружает JSON-файлы сценария из каталога, разбирает их в <see cref="SceneDefinition"/>
/// и сразу (эагерно) прогоняет через <see cref="ScenarioValidator"/> — так ошибка сценарных
/// данных обнаруживается один раз при запуске, а не посреди прохождения игроком. Каждый файл —
/// JSON-массив сцен (см. TECHNICAL_ARCHITECTURE.md, раздел 3: "один файл = одна глава,
/// содержащая одну или несколько сцен").
/// </summary>
public sealed class ScenarioRepository : IScenarioRepository
{
    private readonly Dictionary<string, SceneDefinition> _scenesById;

    public ScenarioRepository(IEnumerable<string> scenarioFilePaths)
        : this(LoadFiles(scenarioFilePaths))
    {
    }

    /// <summary>Конструктор для тестов/программной сборки сценария без файлов на диске.</summary>
    public ScenarioRepository(IEnumerable<SceneDefinition> scenes)
    {
        var sceneList = scenes.ToList();
        new ScenarioValidator().Validate(sceneList);
        _scenesById = sceneList.ToDictionary(scene => scene.SceneId);
    }

    public IReadOnlyCollection<string> SceneIds => _scenesById.Keys;

    public SceneDefinition? GetScene(string sceneId) =>
        _scenesById.TryGetValue(sceneId, out var scene) ? scene : null;

    public static ScenarioRepository LoadFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            throw new ScenarioValidationException($"Каталог сценария не найден: '{directoryPath}'.");

        var files = Directory.EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories);
        return new ScenarioRepository(files);
    }

    private static IEnumerable<SceneDefinition> LoadFiles(IEnumerable<string> scenarioFilePaths)
    {
        var options = ScenarioJsonOptions.Create();
        var scenes = new List<SceneDefinition>();

        foreach (var path in scenarioFilePaths)
        {
            if (!File.Exists(path))
                throw new ScenarioValidationException($"Файл сценария не найден: '{path}'.");

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (IOException ex)
            {
                throw new ScenarioValidationException($"Не удалось прочитать файл сценария '{path}'.", ex);
            }

            SceneDefinition[]? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<SceneDefinition[]>(json, options);
            }
            catch (JsonException ex)
            {
                throw new ScenarioValidationException($"Файл сценария '{path}' повреждён или не соответствует формату: {ex.Message}", ex);
            }

            if (parsed is null || parsed.Length == 0)
                throw new ScenarioValidationException($"Файл сценария '{path}' не содержит ни одной сцены.");

            scenes.AddRange(parsed);
        }

        return scenes;
    }
}
