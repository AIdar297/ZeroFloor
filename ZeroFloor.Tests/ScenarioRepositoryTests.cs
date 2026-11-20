using ZeroFloor.Engine;
using ZeroFloor.Services;

namespace ZeroFloor.Tests;

public class ScenarioRepositoryTests
{
    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);

    [Fact]
    public void Load_ValidScenario_ExposesAllScenes()
    {
        var repository = new ScenarioRepository(new[] { FixturePath("sample_scenario.json") });

        Assert.Contains("TEST_SCENE_1", repository.SceneIds);
        Assert.Contains("TEST_SCENE_2", repository.SceneIds);

        var scene1 = repository.GetScene("TEST_SCENE_1");
        Assert.NotNull(scene1);
        Assert.Equal(3, scene1!.Nodes.Count);
    }

    [Fact]
    public void GetScene_UnknownId_ReturnsNull()
    {
        var repository = new ScenarioRepository(new[] { FixturePath("sample_scenario.json") });

        Assert.Null(repository.GetScene("NO_SUCH_SCENE"));
    }

    [Fact]
    public void Load_ScenarioWithDanglingReference_Throws()
    {
        Assert.Throws<ScenarioValidationException>(() =>
            new ScenarioRepository(new[] { FixturePath("broken_scenario_dangling_reference.json") }));
    }

    [Fact]
    public void Load_MissingFile_Throws()
    {
        Assert.Throws<ScenarioValidationException>(() =>
            new ScenarioRepository(new[] { FixturePath("does_not_exist.json") }));
    }

    [Fact]
    public void LoadFromDirectory_MissingDirectory_Throws()
    {
        Assert.Throws<ScenarioValidationException>(() =>
            ScenarioRepository.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "NoSuchDirectory")));
    }

    [Fact]
    public void LoadFromDirectory_LoadsAllJsonFilesInFixtures()
    {
        // В каталоге Fixtures лежит и валидный, и заведомо битый сценарий — загрузка каталога
        // целиком должна обнаружить проблему одного из файлов, а не молча её проглотить.
        Assert.Throws<ScenarioValidationException>(() =>
            ScenarioRepository.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Fixtures")));
    }
}
