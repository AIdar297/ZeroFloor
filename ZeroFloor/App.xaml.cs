using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ZeroFloor.Engine;
using ZeroFloor.Services;
using ZeroFloor.Services.Presentation;
using ZeroFloor.ViewModels;

namespace ZeroFloor;

public partial class App : Application
{
    private ILoggingService? _loggingService;

    /// <summary>
    /// Presentation-сервисы анимации — WPF-зависимые (принимают FrameworkElement), поэтому
    /// намеренно не идут через AppServices (который держат ViewModel'и). Views, созданные
    /// через DataTemplate без DI, достают их отсюда — см. NovelView.xaml.cs.
    /// </summary>
    public ICharacterAnimationService CharacterAnimation { get; private set; } = null!;
    public ISceneTransitionService StageTransition { get; private set; } = null!;
    public IUiRevealService UiReveal { get; private set; } = null!;

    private AudioService? _audioService;

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _loggingService = new FileLoggingService();
        RegisterGlobalExceptionHandlers();
        _loggingService.LogInfo("Приложение запускается.");

        // Композиционный корень: вручную связываем сервисы и стартовую ViewModel,
        // без DI-контейнера — проект сейчас достаточно мал, чтобы обойтись без него
        // (см. TECHNICAL_ARCHITECTURE.md, раздел 6).
        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZeroFloor");

        var settingsStore = new FileSettingsStore(Path.Combine(appDataDirectory, "settings.json"));
        _audioService = new AudioService(settingsStore);

        Settings = settingsStore;

        var services = new AppServices
        {
            Navigation = new NavigationService(),
            ScreenTransition = new ScreenTransitionService(),
            Notification = new NotificationService(),
            Confirmation = new ConfirmationService(),
            SaveService = new SaveService(Path.Combine(appDataDirectory, "Saves")),
            Settings = settingsStore,
            ScenarioRepository = LoadScenarioRepository(),
            Audio = _audioService,
            Asset = new AssetService(
                Path.Combine(AppContext.BaseDirectory, "Data", "Characters", "characters.json"),
                Path.Combine(AppContext.BaseDirectory, "Data", "Backgrounds", "backgrounds.json"),
                AppContext.BaseDirectory)
        };

        CharacterAnimation = new CharacterAnimationService(settingsStore);
        StageTransition = new SceneTransitionService(settingsStore);
        UiReveal = new UiRevealService(settingsStore);

        var shellViewModel = new ShellViewModel(services);
        services.Navigation.NavigateTo(new MainMenuViewModel(services));

        var mainWindow = new MainWindow
        {
            DataContext = shellViewModel
        };

        MainWindow = mainWindow;
        mainWindow.Show();
    }

    /// <summary>Настройки — доступны WPF-специфичному коду, которому не положено идти через
    /// AppServices (напр. MainWindow, применяющий полноэкранный режим/разрешение до того, как
    /// у него появляется ViewModel с AppServices) — тот же паттерн, что CharacterAnimation/
    /// StageTransition/UiReveal выше.</summary>
    public ISettingsStore Settings { get; private set; } = null!;

    protected override void OnExit(ExitEventArgs e)
    {
        _audioService?.Dispose();
        base.OnExit(e);
    }

    private IScenarioRepository LoadScenarioRepository()
    {
        // Реальный сюжет (Главы 1+) — Data/Scenario. Data/Demo (витрина анимаций) больше не
        // грузится здесь: она намеренно не является сюжетом (см. CLAUDE.md) и не должна
        // попадать в то, что видит игрок при "Новой игре"; её сценарий по-прежнему проверяется
        // отдельно тестами (ZeroFloor.Tests/AnimationShowcaseScenarioTests.cs).
        var scenarioDirectory = Path.Combine(AppContext.BaseDirectory, "Data", "Scenario");

        try
        {
            return ScenarioRepository.LoadFromDirectory(scenarioDirectory);
        }
        catch (ScenarioValidationException ex)
        {
            // Ошибка сценарных данных на старте — не повод падать с нечитаемым стеком:
            // показываем понятное сообщение и продолжаем с пустым репозиторием, чтобы хотя бы
            // главное меню и остальной интерфейс оставались рабочими для диагностики.
            _loggingService?.LogError(ex, "Не удалось загрузить сценарные данные при старте");

            MessageBox.Show(
                $"Не удалось загрузить сценарные данные:\n{ex.Message}\n\nПодробности записаны в журнал.",
                "Нулевой этаж — ошибка сценария",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return new ScenarioRepository(Array.Empty<Models.SceneDefinition>());
        }
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _loggingService?.LogError(e.Exception, "DispatcherUnhandledException");

        MessageBox.Show(
            $"Произошла непредвиденная ошибка:\n{e.Exception.Message}\n\nПодробности записаны в журнал.",
            "Нулевой этаж — ошибка",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // Приложение остаётся открытым: для визуальной новеллы потеря прогресса
        // из-за падения на одной необработанной ошибке хуже, чем попытка продолжить.
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _loggingService?.LogError(exception, "AppDomain.UnhandledException");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _loggingService?.LogError(e.Exception, "TaskScheduler.UnobservedTaskException");
        e.SetObserved();
    }
}
