using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ZeroFloor.Services;

namespace ZeroFloor.Services.Presentation.World3D;

public enum MeterLevel
{
    Calm,
    Suspicious,
    Danger
}

/// <summary>Минималистичный игровой HUD эпизода — реализует Episode3DOverlayControl (XAML).
/// Runtime только сообщает данные, разметка/стиль/анимации — во View.</summary>
public interface IEpisodeHud
{
    void SetObjective(string title, string detail);
    void SetControls(IReadOnlyList<(string Key, string Label)> controls);
    void SetInteractPrompt(string? text);
    void SetDetection(double? value, MeterLevel level, string label);
    void SetPursuit(double? closeness, string label);
    void SetVitals(double? health);
    void SetWarning(string? text);
    void Toast(string text);
    void Banner(string title, string subtitle);
    void SetAlarm(double intensity);
    void FlashDamage();
    void SetFade(double opacity);
    void SetHudVisible(bool visible);
    void ShowPause(bool visible);
}

/// <summary>Связь эпизода со сценарием (выдаёт Episode3DViewModel): маршрут → выбор узла,
/// исход → mg_*-флаг. Runtime не знает id выборов — только ключи маршрутов/исходов.</summary>
public interface IEpisodeStoryBridge
{
    void CommitRoute(string routeKey);
    void Complete(string outcomeKey, string routeKey);
}

/// <summary>
/// Общая основа 3D-эпизода: Viewport3D со слоями (свет → статика → динамика → прозрачное),
/// цикл кадров на CompositionTarget.Rendering с ограничением dt, пауза (Esc), фазы
/// Intro → Play → Outro, камера от третьего лица, Макс (одна модель для обеих глав),
/// звук шагов/приземлений, частицы. Конкретные локации/правила — в наследниках.
/// </summary>
public abstract class EpisodeRuntime : IDisposable
{
    protected enum Phase
    {
        Intro,
        Play,
        Outro,
        Done
    }

    private readonly Stopwatch _clock = new();
    private double _lastTime;
    private bool _running;
    private bool _disposed;
    private readonly List<ParticleSystem3D> _particleSystems = new();
    private readonly List<Point3D> _lampAnchors = new();
    private PointLight[] _pooledLights = Array.Empty<PointLight>();
    private Color _pooledColor;

    protected EpisodeRuntime(Viewport3D viewport, IEpisodeHud hud, IEpisodeStoryBridge story, ISettingsStore settings)
    {
        Viewport = viewport;
        Hud = hud;
        Story = story;
        Settings = settings;
        Audio = new EpisodeAudio(settings);
        Input = new EpisodeInput();
        World = new CollisionWorld();
        Cam = new CameraRig();

        Viewport.Children.Clear();
        Viewport.Camera = Cam.Camera;
        foreach (var layer in new[] { Lights, Static, Dynamic, Transparent })
            Viewport.Children.Add(new ModelVisual3D { Content = layer });

        Max = new HumanoidRig(HumanoidAppearance.MaxOrlov());
        Dynamic.Children.Add(Max.Root);
        Player = new PlayerController(World, new Point3D(), 0);
        Player.Footstep += running => Audio.Play(FootstepSound(), running ? 0.55 : 0.35);
        Player.Landed += impact =>
        {
            Audio.Play("land", Math.Clamp(0.3 + impact / 14, 0.3, 1));
            if (impact > 7) Cam.AddTrauma(Math.Min(0.35, impact / 40));
            OnLanded(impact);
        };
        Player.Jumped += () => Audio.Play("jump", 0.5);

        ApplySettings();
        settings.Changed += OnSettingsChanged;
    }

    protected Viewport3D Viewport { get; }
    protected IEpisodeHud Hud { get; }
    protected IEpisodeStoryBridge Story { get; }
    protected ISettingsStore Settings { get; }
    public EpisodeAudio Audio { get; }
    public EpisodeInput Input { get; }
    protected CollisionWorld World { get; }
    protected CameraRig Cam { get; }
    protected HumanoidRig Max { get; }
    protected PlayerController Player { get; }

    protected Model3DGroup Lights { get; } = new();
    protected Model3DGroup Static { get; } = new();
    protected Model3DGroup Dynamic { get; } = new();
    protected Model3DGroup Transparent { get; } = new();

    protected Phase CurrentPhase { get; private set; } = Phase.Intro;
    protected double PhaseTime { get; private set; }
    protected double Time { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsFinished => CurrentPhase == Phase.Done;

    /// <summary>Окно для захвата мыши (null в автотестах).</summary>
    public Window? HostWindow { get; set; }

    /// <summary>Множитель скорости симуляции (только для автопроверки, обычно 1).</summary>
    public double TimeScale { get; set; } = 1.0;

    public event Action? Finished;

    // Доступ для автоматического прогона эпизода (скриптовый ввод вместо клавиатуры): позиция
    // Макса и направление камеры — ввод WASD трактуется относительно камеры.
    public Point3D PlayerPosition => Player.Position;
    public bool PlayerGrounded => Player.Grounded;
    public double CameraYaw { get => Cam.Yaw; set => Cam.Yaw = value; }
    public string PhaseName => CurrentPhase.ToString();

    // ------------------------------------------------------------------ жизненный цикл

    public void Start()
    {
        BuildLevel();
        Player.Teleport(Player.Position, Player.Yaw);
        Max.Position = Player.Position;
        Max.YawDegrees = Player.Yaw * 180 / Math.PI;
        Hud.SetFade(1);
        EnterPhase(Phase.Intro);
        _clock.Start();
        _lastTime = 0;
        _running = true;
        CompositionTarget.Rendering += OnRendering;
    }

    /// <summary>Шаг симуляции вручную (автопроверка без окна).</summary>
    public void Step(double dt) => Tick(dt);

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_running) return;
        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastTime, 0, 0.05);
        _lastTime = now;
        Tick(dt * TimeScale);
    }

    private void Tick(double dt)
    {
        if (_disposed || CurrentPhase == Phase.Done) return;

        Input.Poll(HostWindow, captureMouse: !IsPaused && CurrentPhase == Phase.Play);

        // окно потеряло фокус (Alt+Tab) — автоматическая пауза, мышь освобождается
        if (HostWindow is { IsActive: false } && !IsPaused && CurrentPhase == Phase.Play && !Input.Scripted)
        {
            SetPaused(true);
            return;
        }

        if (Input.PausePressed && CurrentPhase != Phase.Done)
        {
            SetPaused(!IsPaused);
            return;
        }
        if (IsPaused)
            return;

        Time += dt;
        PhaseTime += dt;

        if (CurrentPhase == Phase.Play)
        {
            var sens = 0.0028 * Settings.Current.EpisodeMouseSensitivity;
            var invert = Settings.Current.EpisodeInvertY ? -1 : 1;
            Cam.Rotate(-Input.MouseDx * sens, Input.MouseDy * sens * invert);
        }

        Update(dt);

        Max.Position = Player.Position;
        Max.YawDegrees = Player.Yaw * 180 / Math.PI;
        Max.Update(dt, CharacterState());

        Cam.Update(dt, Player.Position, World, CameraPivotHeight());
        UpdatePooledLights();

        var look = Cam.Camera.LookDirection;
        foreach (var ps in _particleSystems)
            ps.Update(dt, Cam.Camera.Position, look);
    }

    public void SetPaused(bool paused)
    {
        if (IsPaused == paused) return;
        IsPaused = paused;
        Hud.ShowPause(paused);
        Audio.SetPaused(paused);
        Input.ResetEdges();
        if (paused) Input.ReleaseMouse();
    }

    protected void EnterPhase(Phase phase)
    {
        CurrentPhase = phase;
        PhaseTime = 0;
        Hud.SetHudVisible(phase == Phase.Play);
        if (phase == Phase.Done)
        {
            Input.ReleaseMouse();
            Finished?.Invoke();
        }
    }

    /// <summary>Завершить эпизод: затемнение и передача исхода в сценарий.</summary>
    protected void FinishEpisode(string outcome, string route)
    {
        if (CurrentPhase == Phase.Done) return;
        EnterPhase(Phase.Done);
        _running = false;
        CompositionTarget.Rendering -= OnRendering;
        Audio.StopAll();
        Story.Complete(outcome, route);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _running = false;
        CompositionTarget.Rendering -= OnRendering;
        Settings.Changed -= OnSettingsChanged;
        Input.ReleaseMouse();
        Audio.Dispose();
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => ApplySettings();

    private void ApplySettings()
    {
        var shake = Settings.Current.EpisodeCameraShake && Settings.Current.AnimationLevel != Models.AnimationLevel.Off;
        Cam.ShakeScale = shake ? (Settings.Current.AnimationLevel == Models.AnimationLevel.Reduced ? 0.4 : 1.0) : 0;
    }

    // ------------------------------------------------------------------ для наследников

    protected abstract void BuildLevel();
    protected abstract void Update(double dt);
    protected virtual LocomotionState CharacterState() => Player.Locomotion();
    protected virtual double CameraPivotHeight() => Player.Crouching || Player.Sliding ? 1.05 : 1.6;
    protected virtual string FootstepSound() => Time % 2 < 1 ? "step_metal" : "step_metal2";
    protected virtual void OnLanded(double impact) { }

    protected ParticleSystem3D AddParticles(Func<double, Material> material, int max = 160, bool fadeIn = false)
    {
        var ps = new ParticleSystem3D(material, max, fadeIn);
        _particleSystems.Add(ps);
        Transparent.Children.Add(ps.Root);
        return ps;
    }

    protected static GeometryModel3D? Model(Material material, Action<MeshBuilder> build)
    {
        var mb = new MeshBuilder();
        build(mb);
        return mb.ToModel(material);
    }

    protected void AddStatic(Material material, Action<MeshBuilder> build)
    {
        var m = Model(material, build);
        if (m is not null) Static.Children.Add(m);
    }

    protected void AddTransparent(Material material, Action<MeshBuilder> build, bool doubleSided = true)
    {
        var mb = new MeshBuilder();
        build(mb);
        var m = mb.ToModel(material, doubleSided ? material : null);
        if (m is not null) Transparent.Children.Add(m);
    }

    /// <summary>Пул точечных источников: N реальных PointLight автоматически переезжают к N
    /// ближайшим к игроку «лампам» — в WPF освещение по вершинам и число источников надо
    /// держать малым, а ламп на уровне десятки.</summary>
    protected void SetupLightPool(int count, Color color, double range)
    {
        _pooledColor = color;
        _pooledLights = new PointLight[count];
        for (var i = 0; i < count; i++)
        {
            _pooledLights[i] = new PointLight(color, new Point3D(0, -100, 0))
            {
                Range = range,
                ConstantAttenuation = 0.4,
                LinearAttenuation = 0.12,
                QuadraticAttenuation = 0.06
            };
            Lights.Children.Add(_pooledLights[i]);
        }
    }

    protected void AddLamp(Point3D anchor) => _lampAnchors.Add(anchor);

    private void UpdatePooledLights()
    {
        if (_pooledLights.Length == 0 || _lampAnchors.Count == 0) return;
        var p = Player.Position;
        var nearest = _lampAnchors
            .OrderBy(a => (a - p).LengthSquared)
            .Take(_pooledLights.Length)
            .ToList();
        for (var i = 0; i < _pooledLights.Length; i++)
        {
            if (i < nearest.Count)
            {
                var flicker = 0.92 + 0.08 * Math.Sin(Time * 13 + nearest[i].X * 3.1 + nearest[i].Z);
                _pooledLights[i].Position = nearest[i];
                _pooledLights[i].Color = Scale(_pooledColor, flicker * LampIntensity(nearest[i]));
            }
        }
    }

    /// <summary>Яркость лампы (мерцание/аварийное мигание) — переопределяется уровнем.</summary>
    protected virtual double LampIntensity(Point3D anchor) => 1.0;

    protected static Color Scale(Color c, double k) =>
        Color.FromRgb((byte)Math.Clamp(c.R * k, 0, 255), (byte)Math.Clamp(c.G * k, 0, 255), (byte)Math.Clamp(c.B * k, 0, 255));

    protected static double Dist2D(Point3D a, Point3D b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    protected static double Approach(double current, double target, double maxDelta) =>
        current < target ? Math.Min(target, current + maxDelta) : Math.Max(target, current - maxDelta);

    /// <summary>Подсказки управления (общие).</summary>
    protected static (string, string)[] BaseControls(bool slide) => slide
        ? new[] { ("WASD", "движение"), ("Shift", "бег"), ("Space", "прыжок"), ("C", "присед / скольжение"), ("E", "действие"), ("Esc", "пауза") }
        : new[] { ("WASD", "движение"), ("Shift", "бег"), ("Space", "прыжок"), ("C", "присесть"), ("E", "действие"), ("Esc", "пауза") };
}
