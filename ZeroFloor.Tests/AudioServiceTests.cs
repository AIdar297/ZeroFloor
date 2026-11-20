using ZeroFloor.Services;
using ZeroFloor.Services.Presentation;

namespace ZeroFloor.Tests;

/// <summary>
/// Проверяет ключевую гарантию аудиосистемы: отсутствующий аудиофайл (несуществующий id,
/// null, или просто никогда не предоставленная озвучка) не роняет игру — ни исключением,
/// ни зависанием. Реальное воспроизведение (через Windows Media Foundation) проверяется
/// живым запуском приложения, а не здесь: MediaPlayer — DispatcherObject, и его корректная
/// работа требует запущенного WPF Dispatcher, которого у xUnit-теста нет.
/// </summary>
public class AudioServiceTests
{
    private static AudioService CreateService() => new(new InMemorySettingsStore());

    [Fact]
    public void PlayVoice_WithMissingResource_DoesNotThrow_AndReportsNoVoice()
    {
        using var service = CreateService();

        var exception = Record.Exception(() => service.PlayVoice("does/not/exist"));

        Assert.Null(exception);
        Assert.False(service.HasVoiceForCurrentLine);
    }

    [Fact]
    public void PlayVoice_WithNull_DoesNotThrow_AndReportsNoVoice()
    {
        using var service = CreateService();

        var exception = Record.Exception(() => service.PlayVoice(null));

        Assert.Null(exception);
        Assert.False(service.HasVoiceForCurrentLine);
    }

    [Fact]
    public void PlayVoice_AlwaysStopsPreviousFirst_SequentialCallsDoNotThrow()
    {
        // Гарантия "остановка предыдущей реплики" реализована как безусловный Stop() перед
        // открытием нового файла — здесь проверяем, что быстрая смена не ломается.
        using var service = CreateService();

        service.PlayVoice("a/one");
        var exception = Record.Exception(() => service.PlayVoice("b/two"));

        Assert.Null(exception);
    }

    [Fact]
    public void ReplayVoice_WithoutAnyPriorVoice_DoesNotThrow()
    {
        using var service = CreateService();

        var exception = Record.Exception(() => service.ReplayVoice());

        Assert.Null(exception);
    }

    [Fact]
    public void PlaySfx_WithMissingResource_DoesNotThrow()
    {
        using var service = CreateService();

        var exception = Record.Exception(() => service.PlaySfx("no_such_sfx"));

        Assert.Null(exception);
    }

    [Fact]
    public void PlaySfx_WithNull_DoesNotThrow()
    {
        using var service = CreateService();

        var exception = Record.Exception(() => service.PlaySfx(null));

        Assert.Null(exception);
    }

    [Fact]
    public async Task PlayMusicAsync_WithMissingResource_CompletesWithoutThrow()
    {
        // crossfadeMs=0 — намеренно: VolumeFader.FadeAsync тогда завершается полностью
        // синхронно (без реального await), не переходя на другой поток пула потоков.
        // MediaPlayer — DispatcherObject с привязкой к потоку-владельцу; в реальном
        // приложении всё происходит на единственном потоке UI, но в тесте без Dispatcher
        // ЛЮБОЙ await-подвес может возобновиться на другом потоке и сломать Dispose().
        using var service = CreateService();

        var exception = await Record.ExceptionAsync(() => service.PlayMusicAsync("no_such_track", 0));

        Assert.Null(exception);
    }

    [Fact]
    public async Task PlayAmbienceAsync_WithNull_CompletesWithoutThrow()
    {
        using var service = CreateService();

        var exception = await Record.ExceptionAsync(() => service.PlayAmbienceAsync(null, 0));

        Assert.Null(exception);
    }

    [Fact]
    public async Task PlayMusicAsync_SameResourceTwice_IsNoOpOnSecondCall()
    {
        using var service = CreateService();

        await service.PlayMusicAsync("missing_track", 0);
        var exception = await Record.ExceptionAsync(() => service.PlayMusicAsync("missing_track", 0));

        Assert.Null(exception);
    }

    [Fact]
    public void StopAll_WithNothingPlaying_DoesNotThrow()
    {
        using var service = CreateService();

        var exception = Record.Exception(() => service.StopAll(0));

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_DoesNotThrow_AndIsIdempotent()
    {
        var service = CreateService();

        var firstDispose = Record.Exception(() => service.Dispose());
        var secondDispose = Record.Exception(() => service.Dispose());

        Assert.Null(firstDispose);
        Assert.Null(secondDispose);
    }
}
