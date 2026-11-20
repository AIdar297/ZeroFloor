using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ZeroFloor.Services;

namespace ZeroFloor.Services.Presentation.World3D;

/// <summary>
/// Глава 6 «Шахты»: погоня по техническим стволам 140–160 этажей.
/// Участок 1 — начало преследования (кабели, ящики, низкая труба, траншея);
/// Участок 2 — обрушение настила (провал, падающая балка, узкая балка над пустотой);
/// Участок 3 — физическая развилка: дверь «ЖИЛОЙ СЕКТОР» (короче, Выбор 6 → H−10) или люк
/// «ВЕНТИЛЯЦИЯ» (длиннее, в присяде, Выбор 6 → H+10, теряется гаечный ключ); выбор фиксируется
/// в момент входа в проход; Участок 4 — финальный прыжок, рубильник технической двери и тёмный
/// коридор. Ошибки (падение, удар балкой, захват патрулём) — урон, потеря равновесия, патруль
/// ближе, но никогда не конец игры. Итог — PERFECT / NORMAL / ROUGH (только mg_ch6_chase_result).
/// </summary>
public sealed class ShaftsChaseEpisode : EpisodeRuntime
{
    private readonly Dictionary<Material, MeshBuilder> _batches = new();
    private readonly Dictionary<Material, MeshBuilder> _decals = new();
    private readonly Random _rng = new(6);
    private readonly List<(double z, Point3D pos)> _checkpoints = new();
    private readonly List<Collider> _lowBeams = new();
    private readonly List<Point3D> _sparkPoints = new();
    private readonly List<Point3D> _steamJets = new();
    private readonly List<Npc> _residents = new();
    private readonly List<Guard> _guards = new();

    private ParticleSystem3D _sparks = null!;
    private ParticleSystem3D _steam = null!;
    private ParticleSystem3D _dust = null!;
    private ParticleSystem3D _debris = null!;

    // обрушающийся настил
    private Model3DGroup _collapseDeck = null!;
    private readonly TranslateTransform3D _collapseMove = new();
    private readonly AxisAngleRotation3D _collapseTilt = new(new Vector3D(1, 0, 0), 0);
    private Collider _collapseCollider = null!;
    private double _collapseT = -1;

    // падающая балка
    private readonly TranslateTransform3D _beamMove = new();
    private readonly AxisAngleRotation3D _beamSwing = new(new Vector3D(0, 0, 1), 0);
    private Collider _beamCollider = null!;
    private double _beamT = -1;
    private GeometryModel3D _beamWarning = null!;

    // развилка, дверь, финал
    private string? _route;
    private Collider _resDoor = null!, _ventDoor = null!;
    private readonly TranslateTransform3D _resDoorMove = new();
    private readonly TranslateTransform3D _ventDoorMove = new();
    private Collider _finalDoor = null!;
    private readonly TranslateTransform3D _finalDoorMove = new();
    private double _finalDoorOpen = -1;
    private Collider _finalDoorBehind = null!;
    private readonly TranslateTransform3D _finalDoorBehindMove = new();
    private bool _wrenchDropped;
    private Model3DGroup? _fallingWrench;
    private readonly TranslateTransform3D _wrenchMove = new();
    private readonly AxisAngleRotation3D _wrenchSpin = new(new Vector3D(1, 0.3, 0.2), 0);
    private double _wrenchT;
    private Vector3D _wrenchVel;
    private bool _vendorKnocked;
    private Point3D _vendorPos;
    private readonly TranslateTransform3D _elevatorMove = new();
    private readonly List<AxisAngleRotation3D> _fans = new();

    // путь патруля
    private List<Point3D> _commonPath = null!;
    private List<Point3D> _resPath = null!;
    private List<Point3D> _ventPath = null!;
    private double _playerProgress;

    // состояние
    private double _health = 100;
    private int _hits;
    private double _minGuardDistance = double.MaxValue;
    private double _hitCooldown;
    private double _guardsAppearAt = 1.6;
    private bool _guardsActive;
    private double _guardStun;
    private Point3D _respawn;
    private double _respawnYaw;
    private string? _lastWarning;
    private double _mouseIdle;
    private SpotLight _guardLight = null!;
    private AmbientLight _ambient = null!;
    private PointLight _residentialLight = null!;

    public ShaftsChaseEpisode(Viewport3D viewport, IEpisodeHud hud, IEpisodeStoryBridge story, ISettingsStore settings)
        : base(viewport, hud, story, settings)
    {
    }

    private sealed class Guard
    {
        public required HumanoidRig Rig { get; init; }
        public double Lag;
        public double Lateral;
        public double Speed;
    }

    // ================================================================== уровень

    private MeshBuilder B(Material m)
    {
        if (!_batches.TryGetValue(m, out var b)) _batches[m] = b = new MeshBuilder();
        return b;
    }

    private MeshBuilder D(Material m)
    {
        if (!_decals.TryGetValue(m, out var b)) _decals[m] = b = new MeshBuilder();
        return b;
    }

    private static readonly Color Teal = Color.FromRgb(70, 190, 200);
    private static readonly Color Red = Color.FromRgb(255, 40, 30);

    private Material Grate => Materials3D.Grating(Color.FromRgb(78, 84, 86));
    private Material Steel => Materials3D.MetalPanel(Color.FromRgb(58, 66, 70), 0.45, 0.35);
    private Material DarkSteel => Materials3D.MetalPanel(Color.FromRgb(34, 40, 44), 0.3, 0.3);

    protected override void BuildLevel()
    {
        Player.WalkSpeed = 3.2;
        Player.RunSpeed = 5.9;
        Player.CrouchSpeed = 2.4;
        Player.JumpVelocity = 6.8;
        Player.SlideEnabled = true;
        Player.Teleport(new Point3D(0, 0, -1.5), 0);
        _respawn = Player.Position;
        Cam.Yaw = 0;
        Cam.Pitch = 0.2;
        Cam.Distance = 3.3;
        Cam.ShoulderOffset = 0.25;
        Cam.PivotHeight = 1.65;

        _ambient = new AmbientLight(Color.FromRgb(46, 74, 80));
        Lights.Children.Add(_ambient);
        Lights.Children.Add(new DirectionalLight(Color.FromRgb(90, 150, 160), new Vector3D(0.25, -1, 0.35)));
        SetupLightPool(3, Color.FromRgb(255, 60, 40), 10);
        _residentialLight = new PointLight(Color.FromRgb(255, 170, 100), new Point3D(-4.5, 2.6, 127)) { Range = 22, ConstantAttenuation = 0.5, LinearAttenuation = 0.08 };
        Lights.Children.Add(_residentialLight);
        _guardLight = new SpotLight(Color.FromRgb(255, 245, 220), new Point3D(0, -50, 0), new Vector3D(0, 0, 1), 40, 22) { Range = 18 };
        Lights.Children.Add(_guardLight);

        BuildShaftEnvironment();
        BuildSection1();
        BuildSection2();
        BuildFork();
        BuildResidential();
        BuildVent();
        BuildFinal();

        foreach (var (material, builder) in _batches)
        {
            var m = builder.ToModel(material);
            if (m is not null) Static.Children.Add(m);
        }
        foreach (var (material, builder) in _decals)
        {
            var m = builder.ToModel(material, material);
            if (m is not null) Transparent.Children.Add(m);
        }

        BuildPaths();
        for (var i = 0; i < 3; i++)
        {
            var rig = new HumanoidRig(HumanoidAppearance.Guard(), 40 + i);
            Dynamic.Children.Add(rig.Root);
            rig.Position = new Point3D(0, -60, 0);
            _guards.Add(new Guard { Rig = rig, Lag = i * 2.1, Lateral = i switch { 0 => 0.0, 1 => -0.7, _ => 0.7 }, Speed = 5.0 });
        }

        _sparks = AddParticles(a => Materials3D.Emissive(Color.FromRgb(255, (byte)(140 + 100 * a), 60), 1.0), 200);
        _steam = AddParticles(a => Materials3D.Smoke(Color.FromRgb(170, 190, 195), 0.3 * a), 160, fadeIn: true);
        _dust = AddParticles(a => Materials3D.Smoke(Color.FromRgb(110, 120, 120), 0.35 * a), 120, fadeIn: true);
        _debris = AddParticles(a => Materials3D.Matte(Color.FromRgb(60, 64, 66)), 80);

        Hud.SetControls(BaseControls(slide: true));
        Hud.SetObjective("Шахты", "Бегите! Доберитесь до безопасного технического коридора");
        Hud.SetDetection(null, MeterLevel.Calm, "");
        Hud.SetPursuit(0, "Патруль");
        Hud.SetVitals(1);
        Audio.SetLoop("shaft_amb", 0.75, ambience: true);
    }

    private void Deck(double x0, double z0, double x1, double z1, double y = 0, bool railLeft = true, bool railRight = true, Model3DGroup? into = null)
    {
        var mb = into is null ? B(Grate) : new MeshBuilder();
        mb.AddFloorGrid(x0, z0, x1, z1, y, 1.0, 1.0);
        var side = into is null ? B(DarkSteel) : mb;
        side.AddBoxMinMax(new Point3D(x0, y - 0.25, z0), new Point3D(x1, y - 0.02, z1), 1.0, BoxFaces.Sides | BoxFaces.Bottom);
        // продольные балки под настилом
        side.AddBoxMinMax(new Point3D(x0 + 0.1, y - 0.55, z0), new Point3D(x0 + 0.35, y - 0.25, z1));
        side.AddBoxMinMax(new Point3D(x1 - 0.35, y - 0.55, z0), new Point3D(x1 - 0.1, y - 0.25, z1));
        if (into is not null)
        {
            into.Children.Add(mb.ToModel(Grate)!);
        }
        else
        {
            World.Add(new Point3D(x0, y - 0.3, z0), new Point3D(x1, y, z1), ColliderFlags.Floor);
        }
        if (railLeft) Rail(x1, z0, z1, y, into);
        if (railRight) Rail(x0, z0, z1, y, into);
    }

    private void Rail(double x, double z0, double z1, double y, Model3DGroup? into = null)
    {
        var mb = into is null ? B(Materials3D.Matte(Color.FromRgb(180, 140, 40), 0.4, 30)) : new MeshBuilder();
        mb.AddTube(new Point3D(x, y + 1.0, z0), new Point3D(x, y + 1.0, z1), 0.035, 6);
        mb.AddTube(new Point3D(x, y + 0.5, z0), new Point3D(x, y + 0.5, z1), 0.025, 6);
        for (var z = z0 + 0.05; z <= z1; z += 2.0)
            mb.AddTube(new Point3D(x, y, z), new Point3D(x, y + 1.0, z), 0.03, 6);
        if (into is not null)
            into.Children.Add(mb.ToModel(Materials3D.Matte(Color.FromRgb(180, 140, 40), 0.4, 30))!);
        else
            World.Add(new Point3D(x - 0.06, y, z0), new Point3D(x + 0.06, y + 1.05, z1), ColliderFlags.Solid);
    }

    private void EmergencyLamp(Point3D at)
    {
        B(DarkSteel).AddBox(at, new Vector3D(0.22, 0.22, 0.16));
        B(Materials3D.Emissive(Red, 1.0)).AddBox(at + new Vector3D(0, 0, 0), new Vector3D(0.16, 0.16, 0.2));
        D(Materials3D.SoftGlow(Red, 0.45)).AddPanel(at, new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), 1.0, 1.0);
        D(Materials3D.SoftGlow(Red, 0.45)).AddPanel(at, new Vector3D(0, 0, 1), new Vector3D(0, 1, 0), 1.0, 1.0);
        AddLamp(at);
    }

    private void Crate(double x, double z, double w, double h, double d, double y = 0)
    {
        var mat = Materials3D.MetalPanel(Color.FromRgb(70, 84, 70), 0.5, 0.25);
        B(mat).AddBoxMinMax(new Point3D(x - w / 2, y, z - d / 2), new Point3D(x + w / 2, y + h, z + d / 2), 0.8);
        B(Materials3D.Hazard()).AddBoxMinMax(new Point3D(x - w / 2 - 0.01, y + h - 0.12, z - d / 2 - 0.01), new Point3D(x + w / 2 + 0.01, y + h - 0.04, z + d / 2 + 0.01));
        World.Add(new Point3D(x - w / 2, y, z - d / 2), new Point3D(x + w / 2, y + h, z + d / 2), ColliderFlags.Block);
    }

    /// <summary>Низкая труба/балка поперёк прохода — проскользнуть (C на бегу) или пригнуться.</summary>
    private void LowPipe(double x0, double x1, double z, double bottom)
    {
        B(Materials3D.MetalPanel(Color.FromRgb(120, 84, 50), 0.7, 0.3)).AddTube(new Point3D(x0 - 1.5, bottom + 0.2, z), new Point3D(x1 + 1.5, bottom + 0.2, z), 0.2, 12);
        B(Materials3D.Hazard()).AddBoxMinMax(new Point3D(x0, bottom + 0.15, z - 0.21), new Point3D(x1, bottom + 0.27, z - 0.19));
        _lowBeams.Add(World.Add(new Point3D(x0 - 1.5, bottom, z - 0.2), new Point3D(x1 + 1.5, bottom + 0.4, z + 0.2), ColliderFlags.Solid | ColliderFlags.BlocksCamera, "lowbeam"));
    }

    private void BuildShaftEnvironment()
    {
        var wall = Materials3D.MetalPanel(Color.FromRgb(30, 52, 56), 0.35, 0.2);
        // дальние стены ствола с окнами-огнями
        foreach (var x in new[] { -26.0, 26.0 })
        {
            B(wall).AddWallGrid(new Point3D(x, -70, -20), new Point3D(x, -70, 200), 150, new Vector3D(-Math.Sign(x), 0, 0), 12, 6);
            for (var i = 0; i < 140; i++)
            {
                var y = -60 + _rng.NextDouble() * 120;
                var z = -15 + _rng.NextDouble() * 210;
                var lit = _rng.NextDouble();
                var c = lit < 0.7 ? Color.FromRgb(90, 200, 210) : lit < 0.9 ? Color.FromRgb(255, 190, 110) : Red;
                B(Materials3D.Emissive(c, 0.7)).AddBox(new Point3D(x - Math.Sign(x) * 0.05, y, z), new Vector3D(0.05, 0.25 + _rng.NextDouble() * 0.4, 0.8 + _rng.NextDouble() * 2));
            }
        }
        B(wall).AddWallGrid(new Point3D(-26, -70, 200), new Point3D(26, -70, 200), 150, new Vector3D(0, 0, -1), 12, 6);
        B(wall).AddWallGrid(new Point3D(-26, -70, -20), new Point3D(26, -70, -20), 150, new Vector3D(0, 0, 1), 12, 6);

        // вертикальные трубы, колонны, направляющие лифтов
        var pipe = Materials3D.MetalPanel(Color.FromRgb(60, 80, 84), 0.4, 0.4);
        foreach (var (x, z, r) in new[] { (-11.0, 10.0, 0.6), (-14.0, 40.0, 0.9), (12.5, 25.0, 0.7), (-12.0, 85.0, 0.8), (14.0, 70.0, 1.0), (-15.0, 130.0, 0.7), (15.0, 120.0, 0.9), (-13.0, 170.0, 0.8) })
        {
            B(pipe).AddCylinder(new Point3D(x, 0, z), r, 140, 12, Matrix3D.Identity, false, 2);
            for (var y = -60.0; y < 60; y += 7)
                B(DarkSteel).AddCylinder(new Point3D(x, y, z), r + 0.12, 0.3, 12);
        }
        var railMat = Materials3D.Matte(Color.FromRgb(70, 80, 84), 0.5, 50);
        foreach (var z in new[] { 30.0, 34.0 })
            B(railMat).AddBoxMinMax(new Point3D(9.8, -70, z), new Point3D(10.0, 70, z + 0.2));

        // движущаяся кабина лифта
        var car = new Model3DGroup();
        var cmb = new MeshBuilder();
        cmb.AddBoxMinMax(new Point3D(10.1, 0, 30.4), new Point3D(13.5, 3.2, 33.8));
        car.Children.Add(cmb.ToModel(Materials3D.MetalPanel(Color.FromRgb(90, 96, 92), 0.4, 0.4))!);
        var cl = new MeshBuilder();
        cl.AddBox(new Point3D(10.08, 2.6, 31.2), new Vector3D(0.02, 0.18, 0.5));
        cl.AddBox(new Point3D(10.08, 2.6, 33.0), new Vector3D(0.02, 0.18, 0.5));
        car.Children.Add(cl.ToModel(Materials3D.Emissive(Color.FromRgb(255, 220, 160), 1))!);
        car.Transform = _elevatorMove;
        Dynamic.Children.Add(car);

        // нижние и верхние ярусы (видимая глубина)
        foreach (var (y, z0, z1, x0, x1) in new[] { (-13.0, 20.0, 34.0, -16.0, 16.0), (-13.0, 90.0, 100.0, -16.0, 6.0), (-27.0, 50.0, 70.0, -8.0, 16.0), (-27.0, 140.0, 160.0, -16.0, 16.0), (-42.0, 5.0, 25.0, -16.0, 10.0), (-42.0, 110.0, 125.0, -4.0, 16.0) })
        {
            B(Grate).AddFloorGrid(x0, z0, x1, z1, y, 3, 1);
            B(DarkSteel).AddBoxMinMax(new Point3D(x0, y - 0.5, z0), new Point3D(x1, y - 0.02, z1), 2, BoxFaces.Sides | BoxFaces.Bottom);
            for (var x = x0 + 2; x < x1; x += 6)
                B(Materials3D.Emissive(Red, 0.8)).AddBox(new Point3D(x, y + 0.6, z0 + 0.2), new Vector3D(0.2, 0.2, 0.2));
        }
        foreach (var (y, z) in new[] { (9.0, 15.0), (12.0, 48.0), (16.0, 78.0), (10.0, 104.0), (20.0, 140.0), (13.0, 165.0) })
        {
            B(Steel).AddBoxMinMax(new Point3D(-26, y, z), new Point3D(26, y + 0.8, z + 0.6), 1.5);
            for (var x = -15.0; x < 15; x += 5.5)
                B(Materials3D.Matte(Color.FromRgb(16, 16, 18))).AddSaggingCable(new Point3D(x, y, z + 0.3), new Point3D(x + 3.2, y - 0.4, z + 0.3), 1.6, 0.03);
        }

        // туман глубины: полупрозрачные слои над бездной и свет сверху
        foreach (var (y, a) in new[] { (-9.0, 0.06), (-20.0, 0.09), (-34.0, 0.13), (-50.0, 0.2) })
            D(Materials3D.Glow(Teal, a)).AddPanel(new Point3D(0, y, 90), new Vector3D(1, 0, 0), new Vector3D(0, 0, -1), 52, 220, doubleSided: true);
        D(Materials3D.SoftGlow(Color.FromRgb(200, 255, 255), 0.6)).AddPanel(new Point3D(0, 70, 90), new Vector3D(1, 0, 0), new Vector3D(0, 0, -1), 60, 160, doubleSided: true);
        D(Materials3D.Glow(Teal, 0.04)).AddPanel(new Point3D(0, 20, 198), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), 52, 120, doubleSided: true);
    }

    private void BuildSection1()
    {
        // выход из служебной двери
        var wall = Materials3D.MetalPanel(Color.FromRgb(50, 60, 62), 0.5, 0.2);
        B(wall).AddBoxMinMax(new Point3D(-5, 0, -3.6), new Point3D(-1.1, 4.5, -3.0));
        B(wall).AddBoxMinMax(new Point3D(1.1, 0, -3.6), new Point3D(5, 4.5, -3.0));
        B(wall).AddBoxMinMax(new Point3D(-1.1, 2.4, -3.6), new Point3D(1.1, 4.5, -3.0));
        B(Materials3D.Matte(Color.FromRgb(8, 10, 12))).AddBoxMinMax(new Point3D(-1.1, 0, -3.7), new Point3D(1.1, 2.4, -3.5));
        B(Materials3D.Stencil("СТВОЛ 7 · ЯРУС 152", Color.FromRgb(230, 230, 220), Color.FromRgb(40, 60, 62))).AddBox(new Point3D(0, 3.0, -2.98), new Vector3D(2.0, 0.45, 0.02));
        World.Add(new Point3D(-5, 0, -4.5), new Point3D(5, 4.5, -3.0), ColliderFlags.Wall);

        Deck(-2, -3, 2, 36);
        _checkpoints.Add((-10, new Point3D(0, 0, 0)));

        // кабели на полу — перешагнуть/перепрыгнуть
        var cable = Materials3D.Matte(Color.FromRgb(20, 20, 22), 0.3, 20);
        for (var i = 0; i < 4; i++)
            B(cable).AddTube(new Point3D(-2.2, 0.08 + i * 0.07, 9.6 + i * 0.12), new Point3D(2.2, 0.08 + (3 - i) * 0.07, 9.8 + i * 0.1), 0.07, 6);
        B(Materials3D.Hazard()).AddBoxMinMax(new Point3D(-1.9, 0.02, 9.2), new Point3D(1.9, 0.04, 9.35));
        World.Add(new Point3D(-2, 0, 9.5), new Point3D(2, 0.42, 10.4), ColliderFlags.Block);

        Crate(-1.1, 16, 1.6, 1.0, 1.2);
        Crate(-1.2, 17.3, 1.2, 1.6, 1.0);
        LowPipe(-2, 2, 22.5, 1.15);
        Crate(1.0, 28.5, 1.8, 0.9, 1.0);
        Crate(-1.4, 31, 1.0, 0.6, 0.9);

        for (var z = 4.0; z < 36; z += 8)
        {
            EmergencyLamp(new Point3D(2.05, 1.6, z));
            B(Steel).AddBoxMinMax(new Point3D(2.05, 0, z - 0.1), new Point3D(2.25, 1.4, z + 0.1));
        }
        _sparkPoints.Add(new Point3D(-2.0, 2.4, 13));
        B(cable).AddSaggingCable(new Point3D(-2.0, 2.4, 13), new Point3D(-6, 9, 15), 1.5, 0.04);

        // траншея кабель-канала — первый прыжок
        _checkpoints.Add((37.6, new Point3D(0, 0, 38.6)));
    }

    private void BuildSection2()
    {
        Deck(-2, 37.4, 2, 53.5);
        B(Materials3D.Hazard()).AddBoxMinMax(new Point3D(-2, 0.005, 35.6), new Point3D(2, 0.02, 36));
        B(Materials3D.Hazard()).AddBoxMinMax(new Point3D(-2, 0.005, 37.4), new Point3D(2, 0.02, 37.8));
        EmergencyLamp(new Point3D(-2.05, 1.6, 40));
        EmergencyLamp(new Point3D(2.05, 1.6, 48));

        // обрушающийся участок 53.5..56.3: трещит, искрит, мигает — потом уходит вниз
        _collapseDeck = new Model3DGroup();
        Deck(-2, 53.5, 2, 56.3, into: _collapseDeck);
        var hz = new MeshBuilder();
        hz.AddBoxMinMax(new Point3D(-2, 0.01, 53.5), new Point3D(2, 0.03, 53.8));
        hz.AddBoxMinMax(new Point3D(-2, 0.01, 56.0), new Point3D(2, 0.03, 56.3));
        _collapseDeck.Children.Add(hz.ToModel(Materials3D.Hazard())!);
        var tg = new Transform3DGroup();
        tg.Children.Add(new TranslateTransform3D(0, 0, -54.9));
        tg.Children.Add(new RotateTransform3D(_collapseTilt));
        tg.Children.Add(new TranslateTransform3D(0, 0, 54.9));
        tg.Children.Add(_collapseMove);
        _collapseDeck.Transform = tg;
        Dynamic.Children.Add(_collapseDeck);
        _collapseCollider = World.Add(new Point3D(-2, -0.3, 53.5), new Point3D(2, 0, 56.3), ColliderFlags.Floor);
        _sparkPoints.Add(new Point3D(0, -0.2, 53.5));
        _sparkPoints.Add(new Point3D(1.5, -0.2, 56.3));
        _checkpoints.Add((56.8, new Point3D(0, 0, 57.4)));

        Deck(-2, 56.3, 2, 70);
        EmergencyLamp(new Point3D(2.05, 1.6, 60));

        // подвешенная балка: качается на цепи, внизу красная зона падения
        var beam = new Model3DGroup();
        var bmb = new MeshBuilder();
        bmb.AddBox(new Point3D(-0.9, 0, 0), new Vector3D(3.0, 0.45, 0.45));
        bmb.AddBox(new Point3D(-0.9, 0.2, 0), new Vector3D(3.0, 0.06, 0.7));
        bmb.AddBox(new Point3D(-0.9, -0.2, 0), new Vector3D(3.0, 0.06, 0.7));
        beam.Children.Add(bmb.ToModel(Materials3D.MetalPanel(Color.FromRgb(150, 90, 40), 0.75, 0.3))!);
        var chain = new MeshBuilder();
        chain.AddTube(new Point3D(-0.9, 0.2, 0), new Point3D(-0.9, 8, 0), 0.03, 4);
        beam.Children.Add(chain.ToModel(Materials3D.Matte(Color.FromRgb(30, 30, 30)))!);
        var btg = new Transform3DGroup();
        btg.Children.Add(new RotateTransform3D(_beamSwing, -0.9, 8, 0));
        btg.Children.Add(new TranslateTransform3D(0, 5.2, 63.5));
        btg.Children.Add(_beamMove);
        beam.Transform = btg;
        Dynamic.Children.Add(beam);
        _beamCollider = World.Add(new Point3D(-2.4, 0, 63.2), new Point3D(0.6, 0.45, 63.8), ColliderFlags.Block, "beam");
        _beamCollider.Enabled = false;
        var warn = new MeshBuilder();
        warn.AddPanel(new Point3D(-0.9, 0.03, 63.5), new Vector3D(1, 0, 0), new Vector3D(0, 0, -1), 3.2, 1.4, doubleSided: false);
        _beamWarning = new GeometryModel3D(warn.Build(), Materials3D.SoftGlow(Red, 0.7));
        Transparent.Children.Add(_beamWarning);
        _checkpoints.Add((66, new Point3D(0, 0, 68.5)));

        // узкая балка над пустотой 70..80
        var ibeam = Materials3D.MetalPanel(Color.FromRgb(96, 104, 108), 0.4, 0.4);
        B(ibeam).AddBoxMinMax(new Point3D(-0.35, -0.12, 70), new Point3D(0.35, 0, 80));
        B(ibeam).AddBoxMinMax(new Point3D(-0.06, -0.8, 70), new Point3D(0.06, -0.12, 80));
        B(ibeam).AddBoxMinMax(new Point3D(-0.3, -0.9, 70), new Point3D(0.3, -0.8, 80));
        B(Materials3D.Hazard()).AddBoxMinMax(new Point3D(-0.36, -0.1, 70), new Point3D(-0.3, 0.005, 80));
        B(Materials3D.Hazard()).AddBoxMinMax(new Point3D(0.3, -0.1, 70), new Point3D(0.36, 0.005, 80));
        World.Add(new Point3D(-0.35, -0.3, 70), new Point3D(0.35, 0, 80), ColliderFlags.Floor);
        B(Materials3D.Matte(Color.FromRgb(20, 20, 22))).AddSaggingCable(new Point3D(-1.4, 2.4, 70), new Point3D(-1.4, 2.4, 80), 0.6, 0.05);
        B(Materials3D.Matte(Color.FromRgb(20, 20, 22))).AddSaggingCable(new Point3D(1.4, 2.2, 70), new Point3D(1.4, 2.6, 80), 0.8, 0.05);
        _sparkPoints.Add(new Point3D(1.4, 2.0, 75));
        _checkpoints.Add((81, new Point3D(0, 0, 82)));

        Deck(-2, 80, 2, 96);
        LowPipe(-2, 2, 88.5, 1.1);
        Crate(1.1, 92.5, 1.4, 0.85, 1.0);
        EmergencyLamp(new Point3D(-2.05, 1.6, 84));
        EmergencyLamp(new Point3D(2.05, 1.6, 94));
        _steamJets.Add(new Point3D(-2.2, 0.6, 85));
        _steamJets.Add(new Point3D(2.2, 0.8, 91));
        B(Materials3D.MetalPanel(Color.FromRgb(90, 96, 96), 0.5, 0.4)).AddTube(new Point3D(-2.6, 0.6, 80), new Point3D(-2.6, 0.6, 96), 0.25, 10);
    }

    private void BuildFork()
    {
        Deck(-7, 96, 7, 108, railLeft: true, railRight: true);
        Rail96(-7, -2);
        Rail96(2, 7);
        // стена с двумя проходами
        var wall = Materials3D.MetalPanel(Color.FromRgb(56, 66, 68), 0.5, 0.2);
        void Seg(double x0, double x1, double y0, double y1)
        {
            B(wall).AddBoxMinMax(new Point3D(x0, y0, 108), new Point3D(x1, y1, 108.4), 1.5);
            World.Add(new Point3D(x0, y0, 108), new Point3D(x1, y1, 108.4), ColliderFlags.Wall);
        }
        Seg(-8, -6, 0, 6);
        Seg(-3, 3.8, 0, 6);
        Seg(5.2, 8, 0, 6);
        Seg(-6, -3, 3, 6);
        Seg(3.8, 5.2, 1.25, 6);

        Neon("ЖИЛОЙ СЕКТОР", new Point3D(-4.5, 3.6, 107.95), new Vector3D(-1, 0, 0), 2.8, 0.6, Color.FromRgb(255, 180, 90));
        Neon("ВЕНТИЛЯЦИЯ", new Point3D(4.5, 2.0, 107.95), new Vector3D(-1, 0, 0), 2.4, 0.55, Color.FromRgb(80, 220, 255));
        B(Materials3D.Stencil("← КОРОЧЕ · ЛЮДИ", Color.FromRgb(250, 200, 120), Color.FromRgb(30, 34, 36), 44)).AddBox(new Point3D(-1.6, 1.6, 107.97), new Vector3D(1.6, 0.4, 0.02));
        B(Materials3D.Stencil("ДЛИННЕЕ · ТЕСНО →", Color.FromRgb(140, 220, 255), Color.FromRgb(30, 34, 36), 44)).AddBox(new Point3D(1.6, 1.0, 107.97), new Vector3D(1.6, 0.4, 0.02));
        EmergencyLamp(new Point3D(0, 2.4, 107.8));

        // двери-заслонки, которые захлопываются за игроком после выбора маршрута
        _resDoor = AddShutter(-6, -3, 3.0, _resDoorMove, Materials3D.MetalPanel(Color.FromRgb(110, 84, 50), 0.6, 0.3));
        _ventDoor = AddShutter(3.8, 5.2, 1.25, _ventDoorMove, Materials3D.Grating(Color.FromRgb(100, 110, 114)));
        _checkpoints.Add((97, new Point3D(0, 0, 99)));
    }

    private void Rail96(double x0, double x1)
    {
        var mat = Materials3D.Matte(Color.FromRgb(180, 140, 40), 0.4, 30);
        B(mat).AddTube(new Point3D(x0, 1.0, 96), new Point3D(x1, 1.0, 96), 0.035, 6);
        B(mat).AddTube(new Point3D(x0, 0.5, 96), new Point3D(x1, 0.5, 96), 0.025, 6);
        World.Add(new Point3D(x0, 0, 95.94), new Point3D(x1, 1.05, 96.06), ColliderFlags.Solid);
    }

    private Collider AddShutter(double x0, double x1, double h, TranslateTransform3D move, Material mat)
    {
        var mb = new MeshBuilder();
        mb.AddBoxMinMax(new Point3D(x0, 0, 108.1), new Point3D(x1, h, 108.3));
        var model = mb.ToModel(mat)!;
        var g = new Model3DGroup();
        g.Children.Add(model);
        move.OffsetY = h;
        g.Transform = move;
        Dynamic.Children.Add(g);
        var c = World.Add(new Point3D(x0, 0, 108.05), new Point3D(x1, h, 108.35), ColliderFlags.Wall);
        c.Enabled = false;
        return c;
    }

    private void Neon(string text, Point3D center, Vector3D right, double w, double h, Color color)
    {
        var mat = Materials3D.NeonSign(text, color, Color.FromRgb(10, 10, 12));
        var mb = new MeshBuilder();
        mb.AddPanel(center, right, new Vector3D(0, 1, 0), w, h);
        Static.Children.Add(new GeometryModel3D(mb.Build(), mat) { BackMaterial = mat });
        D(Materials3D.SoftGlow(color, 0.25)).AddPanel(center + new Vector3D(0, 0, -0.05), right, new Vector3D(0, 1, 0), w * 1.4, h * 2.4);
    }

    private void BuildResidential()
    {
        var floor = Materials3D.Concrete(Color.FromRgb(70, 64, 58));
        var wall = Materials3D.Concrete(Color.FromRgb(86, 76, 66));
        B(floor).AddFloorGrid(-6, 108, -3, 146, 0, 1, 2);
        World.Add(new Point3D(-6, -0.3, 108), new Point3D(-3, 0, 146), ColliderFlags.Floor);
        B(wall).AddWallGrid(new Point3D(-6, 0, 108.4), new Point3D(-6, 0, 146), 3, new Vector3D(1, 0, 0), 1.5, 2);
        B(wall).AddWallGrid(new Point3D(-3, 0, 108.4), new Point3D(-3, 0, 146), 3, new Vector3D(-1, 0, 0), 1.5, 2);
        B(Materials3D.Concrete(Color.FromRgb(50, 46, 42))).AddFloorGrid(-6, 108.4, -3, 146, 3, 2, 2, faceDown: true);
        World.Add(new Point3D(-6.3, 0, 108.4), new Point3D(-6, 3, 146), ColliderFlags.Wall);
        World.Add(new Point3D(-3, 0, 108.4), new Point3D(-2.7, 3, 146), ColliderFlags.Wall);
        World.Add(new Point3D(-6.3, 3, 108.4), new Point3D(-2.7, 3.3, 146), ColliderFlags.Wall);

        // двери квартир, бельё, хлам
        var door = Materials3D.MetalPanel(Color.FromRgb(90, 70, 52), 0.5, 0.2);
        for (var z = 111.0; z < 145; z += 5.2)
        {
            var side = (int)z % 2 == 0 ? -5.98 : -3.02;
            B(door).AddBox(new Point3D(side, 1.0, z), new Vector3D(0.04, 2.0, 0.9));
            B(Materials3D.Emissive(Color.FromRgb(255, 200, 130), 0.8)).AddBox(new Point3D(side, 2.2, z), new Vector3D(0.05, 0.08, 0.3));
            D(Materials3D.SoftGlow(Color.FromRgb(255, 180, 100), 0.3)).AddPanel(new Point3D(side + (side < -4 ? 0.06 : -0.06), 2.2, z), new Vector3D(0, 0, 1), new Vector3D(0, 1, 0), 1.2, 0.8);
        }
        var cloth = new[] { Materials3D.Fabric(Color.FromRgb(160, 60, 50)), Materials3D.Fabric(Color.FromRgb(70, 110, 150)), Materials3D.Fabric(Color.FromRgb(200, 190, 160)) };
        for (var z = 113.0; z < 144; z += 7)
        {
            B(Materials3D.Matte(Color.FromRgb(30, 30, 30))).AddTube(new Point3D(-6, 2.9, z), new Point3D(-3, 2.9, z + 0.6), 0.01, 4);
            for (var x = -5.7; x < -5.0; x += 0.45)
                B(cloth[_rng.Next(cloth.Length)]).AddPanel(new Point3D(x, 2.66, z + (x + 6) * 0.2), new Vector3D(1, 0, 0.2), new Vector3D(0, 1, 0), 0.35, 0.45);
            for (var x = -3.9; x < -3.2; x += 0.45)
                B(cloth[_rng.Next(cloth.Length)]).AddPanel(new Point3D(x, 2.66, z + (x + 6) * 0.2), new Vector3D(1, 0, 0.2), new Vector3D(0, 1, 0), 0.35, 0.45);
        }
        Crate(-5.5, 116, 0.8, 0.8, 1.0);
        Crate(-3.5, 121, 0.8, 1.1, 0.8);
        Crate(-5.4, 135, 1.0, 0.7, 0.8);
        Crate(-3.6, 140.5, 0.8, 0.9, 1.0);
        Crate(-5.5, 142.5, 0.7, 0.6, 0.7);

        // лоток уличного торговца посреди прохода
        _vendorPos = new Point3D(-4.4, 0, 128);
        B(Materials3D.Wood(Color.FromRgb(110, 80, 50))).AddBoxMinMax(new Point3D(-5.6, 0, 127.6), new Point3D(-4.9, 0.8, 128.4));
        World.Add(new Point3D(-5.6, 0, 127.6), new Point3D(-4.9, 0.8, 128.4), ColliderFlags.Block);

        // жители идут навстречу
        Color[] tops = { Materials3D.Hex("#6B5A44"), Materials3D.Hex("#3D4A55"), Materials3D.Hex("#5E3B34"), Materials3D.Hex("#4A5340") };
        for (var i = 0; i < 4; i++)
        {
            var look = new HumanoidAppearance { Top = tops[i], Bottom = Materials3D.Hex("#2E2C2A"), Hood = i == 1, Cap = i == 3, Backpack = i == 2, Height = 0.94 + i * 0.03 };
            var x = i % 2 == 0 ? -4.0 : -5.0;
            var z0 = 118 + i * 6.5;
            var npc = new Npc(new HumanoidRig(look, 60 + i), new Point3D(x, 0, z0 + 6), 180,
                new[] { new Point3D(x, 0, z0 + 6), new Point3D(x + (i % 2 == 0 ? -0.6 : 0.6), 0, z0 - 4), new Point3D(x, 0, z0 + 6) }, 0.9 + i * 0.1);
            _residents.Add(npc);
            Dynamic.Children.Add(npc.Rig.Root);
        }
        _checkpoints.Add((110, new Point3D(-4.5, 0, 111)));
        D(Materials3D.SoftGlow(Color.FromRgb(255, 170, 100), 0.18)).AddPanel(new Point3D(-4.5, 0.02, 127), new Vector3D(1, 0, 0), new Vector3D(0, 0, -1), 3, 30, doubleSided: false);
    }

    private void BuildVent()
    {
        var duct = Materials3D.MetalPanel(Color.FromRgb(120, 128, 128), 0.25, 0.45);
        const double h = 1.25;

        // пол и потолок короба по прямоугольникам
        foreach (var (x0, z0, x1, z1) in new[]
                 {
                     (3.8, 108.0, 5.2, 119.3), (3.8, 119.3, 9.7, 120.7), (8.3, 120.7, 9.7, 133.3),
                     (3.8, 133.3, 9.7, 134.7), (3.8, 134.7, 5.2, 146.0)
                 })
        {
            B(Grate).AddFloorGrid(x0, z0, x1, z1, 0, 0.7, 1);
            World.Add(new Point3D(x0, -0.3, z0), new Point3D(x1, 0, z1), ColliderFlags.Floor);
            B(duct).AddFloorGrid(x0, z0, x1, z1, h, 0.7, 1, faceDown: true);
            B(duct).AddBoxMinMax(new Point3D(x0 - 0.1, h, z0), new Point3D(x1 + 0.1, h + 0.1, z1), 1, BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            World.Add(new Point3D(x0 - 0.1, h, z0), new Point3D(x1 + 0.1, h + 0.35, z1), ColliderFlags.Solid | ColliderFlags.BlocksCamera);
            B(DarkSteel).AddBoxMinMax(new Point3D(x0, -0.6, z0), new Point3D(x1, -0.02, z1), 1, BoxFaces.Sides | BoxFaces.Bottom);
        }

        // стенки: тонкие боксы (видимы с обеих сторон), повороты открыты
        foreach (var (x0, z0, x1, z1) in new[]
                 {
                     (3.7, 108.4, 3.8, 120.7), (5.2, 108.4, 5.3, 119.3), (3.8, 120.7, 8.3, 120.8), (5.2, 119.2, 9.7, 119.3),
                     (9.7, 119.3, 9.8, 134.7), (8.2, 120.7, 8.3, 133.3), (3.8, 133.2, 8.3, 133.3), (5.2, 134.7, 9.7, 134.8),
                     (3.7, 133.3, 3.8, 146.0), (5.2, 134.7, 5.3, 146.0)
                 })
        {
            B(duct).AddBoxMinMax(new Point3D(x0, 0, z0), new Point3D(x1, h, z1), 1);
            World.Add(new Point3D(x0, 0, z0), new Point3D(x1, h, z1), ColliderFlags.Wall);
        }
        // рёбра жёсткости снаружи
        for (var z = 110.0; z < 146; z += 2.5)
        {
            if (z is > 120 and < 134)
                B(DarkSteel).AddBoxMinMax(new Point3D(9.8, -0.05, z), new Point3D(9.9, h + 0.15, z + 0.12));
            else
                B(DarkSteel).AddBoxMinMax(new Point3D(3.6, -0.05, z), new Point3D(3.7, h + 0.15, z + 0.12));
        }

        // тусклые лампы и вентиляторы в стенах короба
        for (var z = 112.0; z < 146; z += 7)
        {
            var x = z is > 121 and < 133 ? 9.0 : 4.5;
            B(Materials3D.Emissive(Color.FromRgb(120, 220, 255), 0.6)).AddBox(new Point3D(x, 1.2, z), new Vector3D(0.3, 0.03, 0.1));
        }
        foreach (var (x, z) in new[] { (8.35, 126.0), (3.85, 140.0) })
        {
            var spin = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
            _fans.Add(spin);
            var fmb = new MeshBuilder();
            for (var i = 0; i < 4; i++)
                fmb.AddBox(new Point3D(0, 0.22, 0), new Vector3D(0.02, 0.4, 0.1), MeshBuilder.Trs(new Vector3D(), pitchDeg: i * 90));
            var fan = new GeometryModel3D(fmb.Build(), Materials3D.Matte(Color.FromRgb(50, 52, 54), 0.4, 30));
            var tg = new Transform3DGroup();
            tg.Children.Add(new RotateTransform3D(spin));
            tg.Children.Add(new TranslateTransform3D(x, 0.62, z));
            fan.Transform = tg;
            Dynamic.Children.Add(fan);
        }
        _checkpoints.Add((110.2, new Point3D(4.5, 0, 111)));
    }

    private void BuildFinal()
    {
        var wall = Materials3D.MetalPanel(Color.FromRgb(56, 66, 68), 0.5, 0.2);
        // стена выхода маршрутов z = 146
        void Seg(double x0, double x1, double y0, double y1)
        {
            B(wall).AddBoxMinMax(new Point3D(x0, y0, 145.8), new Point3D(x1, y1, 146.2), 1.5);
            World.Add(new Point3D(x0, y0, 145.8), new Point3D(x1, y1, 146.2), ColliderFlags.Wall);
        }
        Seg(-8, -6, 0, 5);
        Seg(-3, 3.8, 0, 5);
        Seg(5.2, 8, 0, 5);
        Seg(-6, -3, 3, 5);
        Seg(3.8, 5.2, 1.25, 5);

        Deck(-7, 146, 7, 156);
        EmergencyLamp(new Point3D(-6.9, 1.6, 150));
        EmergencyLamp(new Point3D(6.9, 1.6, 150));
        B(Materials3D.Hazard()).AddBoxMinMax(new Point3D(-3, 0.005, 155.6), new Point3D(3, 0.02, 156));
        _checkpoints.Add((147, new Point3D(0, 0, 150)));

        // последний провал 156..158.8 и площадка у технической двери
        Deck(-3, 158.8, 3, 173);
        B(Materials3D.Hazard()).AddBoxMinMax(new Point3D(-3, 0.005, 158.8), new Point3D(3, 0.02, 159.2));
        _checkpoints.Add((159.3, new Point3D(0, 0, 160.5)));
        EmergencyLamp(new Point3D(-2.9, 1.6, 165));

        // стена с технической дверью
        B(wall).AddBoxMinMax(new Point3D(-5, 0, 173), new Point3D(-1.3, 5, 173.4), 1.5);
        B(wall).AddBoxMinMax(new Point3D(1.3, 0, 173), new Point3D(5, 5, 173.4), 1.5);
        B(wall).AddBoxMinMax(new Point3D(-1.3, 2.5, 173), new Point3D(1.3, 5, 173.4), 1.5);
        World.Add(new Point3D(-5, 0, 173), new Point3D(-1.3, 5, 173.4), ColliderFlags.Wall);
        World.Add(new Point3D(1.3, 0, 173), new Point3D(5, 5, 173.4), ColliderFlags.Wall);
        World.Add(new Point3D(-1.3, 2.5, 173), new Point3D(1.3, 5, 173.4), ColliderFlags.Wall);
        B(Materials3D.Stencil("ТЕХ. КОРИДОР 7-Б", Color.FromRgb(230, 230, 220), Color.FromRgb(40, 50, 52), 48)).AddBox(new Point3D(0, 3.0, 172.98), new Vector3D(2.0, 0.4, 0.02));

        var dmb = new MeshBuilder();
        dmb.AddBoxMinMax(new Point3D(-1.3, 0, 173.05), new Point3D(1.3, 2.5, 173.3));
        var dg = new Model3DGroup();
        dg.Children.Add(dmb.ToModel(Materials3D.MetalPanel(Color.FromRgb(100, 108, 110), 0.4, 0.4))!);
        var stripe = new MeshBuilder();
        stripe.AddBoxMinMax(new Point3D(-1.3, 1.1, 173.0), new Point3D(1.3, 1.3, 173.05));
        dg.Children.Add(stripe.ToModel(Materials3D.Hazard())!);
        dg.Transform = _finalDoorMove;
        Dynamic.Children.Add(dg);
        _finalDoor = World.Add(new Point3D(-1.3, 0, 173.0), new Point3D(1.3, 2.5, 173.35), ColliderFlags.Wall);

        // рубильник
        B(DarkSteel).AddBox(new Point3D(2.2, 1.3, 172.85), new Vector3D(0.5, 0.7, 0.2));
        B(Materials3D.Emissive(Color.FromRgb(255, 190, 40), 1)).AddBox(new Point3D(2.2, 1.45, 172.73), new Vector3D(0.12, 0.2, 0.05));
        D(Materials3D.SoftGlow(Color.FromRgb(255, 190, 40), 0.5)).AddPanel(new Point3D(2.2, 1.45, 172.7), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), 0.9, 0.9);

        // тёмный безопасный коридор
        B(Materials3D.Concrete(Color.FromRgb(36, 40, 42))).AddFloorGrid(-1.6, 173.4, 1.6, 192, 0, 1, 2);
        World.Add(new Point3D(-1.6, -0.3, 173.4), new Point3D(1.6, 0, 192), ColliderFlags.Floor);
        B(Materials3D.MetalPanel(Color.FromRgb(36, 42, 44), 0.4, 0.1)).AddWallGrid(new Point3D(-1.6, 0, 173.4), new Point3D(-1.6, 0, 192), 2.6, new Vector3D(1, 0, 0), 1.5, 2);
        B(Materials3D.MetalPanel(Color.FromRgb(36, 42, 44), 0.4, 0.1)).AddWallGrid(new Point3D(1.6, 0, 173.4), new Point3D(1.6, 0, 192), 2.6, new Vector3D(-1, 0, 0), 1.5, 2);
        B(Materials3D.Concrete(Color.FromRgb(24, 26, 28))).AddFloorGrid(-1.6, 173.4, 1.6, 192, 2.6, 2, 2, faceDown: true);
        B(Materials3D.Concrete(Color.FromRgb(24, 26, 28))).AddWallGrid(new Point3D(-1.6, 0, 192), new Point3D(1.6, 0, 192), 2.6, new Vector3D(0, 0, -1), 1.5, 2);
        World.Add(new Point3D(-1.9, 0, 173.4), new Point3D(-1.6, 2.6, 192), ColliderFlags.Wall);
        World.Add(new Point3D(1.6, 0, 173.4), new Point3D(1.9, 2.6, 192), ColliderFlags.Wall);
        World.Add(new Point3D(-1.9, 2.6, 173.4), new Point3D(1.9, 2.9, 192), ColliderFlags.Wall);
        World.Add(new Point3D(-1.9, 0, 192), new Point3D(1.9, 2.6, 192.3), ColliderFlags.Wall);
        for (var z = 178.0; z < 192; z += 5)
            B(Materials3D.Emissive(Color.FromRgb(60, 140, 150), 0.5)).AddBox(new Point3D(0, 2.55, z), new Vector3D(0.6, 0.04, 0.12));

        // дверь, которая закрывается за Максом
        var bmb = new MeshBuilder();
        bmb.AddBoxMinMax(new Point3D(-1.3, 0, 173.45), new Point3D(1.3, 2.5, 173.6));
        var bg = new Model3DGroup();
        bg.Children.Add(bmb.ToModel(Materials3D.MetalPanel(Color.FromRgb(80, 86, 88), 0.4, 0.3))!);
        _finalDoorBehindMove.OffsetY = 2.5;
        bg.Transform = _finalDoorBehindMove;
        Dynamic.Children.Add(bg);
        _finalDoorBehind = World.Add(new Point3D(-1.3, 0, 173.45), new Point3D(1.3, 2.5, 173.6), ColliderFlags.Wall);
        _finalDoorBehind.Enabled = false;
    }

    private void BuildPaths()
    {
        _commonPath = new() { new(0, 0, -6), new(0, 0, 70), new(0, 0, 80), new(0, 0, 100) };
        _resPath = new(_commonPath) { new(-4.5, 0, 106), new(-4.5, 0, 146), new(0, 0, 150), new(0, 0, 173), new(0, 0, 186) };
        _ventPath = new(_commonPath) { new(4.5, 0, 106), new(4.5, 0, 120), new(9, 0, 120), new(9, 0, 134), new(4.5, 0, 134), new(4.5, 0, 146), new(0, 0, 150), new(0, 0, 173), new(0, 0, 186) };
    }

    private List<Point3D> ActivePath => _route == "residential" ? _resPath : _route == "ventilation" ? _ventPath
        : (Player.Position.X > 0.5 && Player.Position.Z > 100 ? _ventPath : _resPath);

    private static double PathLength(List<Point3D> path, int upTo)
    {
        var s = 0.0;
        for (var i = 0; i < upTo; i++) s += (path[i + 1] - path[i]).Length;
        return s;
    }

    private static (double progress, Point3D closest) Project(List<Point3D> path, Point3D p, double hint)
    {
        var best = double.MaxValue;
        var bestProgress = 0.0;
        var bestPoint = path[0];
        var acc = 0.0;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var a = path[i];
            var seg = path[i + 1] - a;
            var len = seg.Length;
            var t = Math.Clamp(((p.X - a.X) * seg.X + (p.Z - a.Z) * seg.Z) / (len * len), 0, 1);
            var c = a + seg * t;
            var d = Math.Sqrt((p.X - c.X) * (p.X - c.X) + (p.Z - c.Z) * (p.Z - c.Z));
            var prog = acc + t * len;
            // предпочитаем сегменты рядом с прошлым прогрессом (повороты короба)
            var score = d + Math.Max(0, Math.Abs(prog - hint) - 8) * 0.5;
            if (score < best)
            {
                best = score;
                bestProgress = prog;
                bestPoint = c;
            }
            acc += len;
        }
        return (bestProgress, bestPoint);
    }

    private static (Point3D pos, Vector3D dir) PointAt(List<Point3D> path, double progress)
    {
        var acc = 0.0;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var seg = path[i + 1] - path[i];
            var len = seg.Length;
            if (progress <= acc + len || i == path.Count - 2)
            {
                var t = Math.Clamp((progress - acc) / len, 0, 1);
                seg.Normalize();
                return (path[i] + (path[i + 1] - path[i]) * t, seg);
            }
            acc += len;
        }
        return (path[^1], new Vector3D(0, 0, 1));
    }

    // ================================================================== игровой цикл

    protected override void Update(double dt)
    {
        switch (CurrentPhase)
        {
            case Phase.Intro: UpdateIntro(dt); break;
            case Phase.Play: UpdatePlay(dt); break;
            case Phase.Outro: UpdateOutro(dt); break;
        }
        UpdateHazards(dt);
        UpdateGuards(dt);
        UpdateEnvironment(dt);
        foreach (var r in _residents)
        {
            r.Update(dt, Player.Position);
            if (CurrentPhase == Phase.Play && Dist2D(r.Position, Player.Position) < 0.62 && Player.HorizontalSpeed > 3 && Player.Stumble <= 0)
            {
                Player.Stumble = 0.7;
                Audio.Play("impact", 0.4);
                Hud.Toast("Макс врезается в прохожего и теряет темп");
            }
            r.PushPlayer(Player);
        }
    }

    private void UpdateIntro(double dt)
    {
        if (PhaseTime < 0.01)
        {
            Cam.SnapCinematic(new Point3D(1.6, 1.4, 4.5), new Point3D(0, 1.3, -2.5));
            Hud.Banner("ГЛАВА 6 · ШАХТЫ", "Технические стволы, ярусы 140–160");
            Audio.Play("radio", 0.8);
            Audio.Play("door", 0.7);
        }
        Hud.SetFade(Math.Max(0, 1 - PhaseTime / 0.8));
        var scripted = new EpisodeInput { Scripted = true, Forward = PhaseTime > 0.6 };
        scripted.Poll(null, false);
        Player.Update(dt, scripted, new Vector3D(0, 0, 1), new Vector3D(-1, 0, 0));
        if (PhaseTime > 1.4) Cam.ReleaseCinematic(2.5);
        if (PhaseTime > 2.2)
        {
            EnterPhase(Phase.Play);
            _guardsAppearAt = 1.2;
        }
    }

    private void UpdatePlay(double dt)
    {
        _hitCooldown = Math.Max(0, _hitCooldown - dt);
        Player.ForceCrouch = InVent(Player.Position, margin: 0.9);
        Player.Update(dt, Input, Cam.Forward, Cam.Right);

        // камера сама доворачивает за бегом, если мышь не трогают
        _mouseIdle = Math.Abs(Input.MouseDx) + Math.Abs(Input.MouseDy) > 0.5 ? 0 : _mouseIdle + dt;
        if (_mouseIdle > 0.8 && Player.HorizontalSpeed > 2)
            Cam.EaseYawTowards(Math.Atan2(Player.Velocity.X, Player.Velocity.Z), 1.6, dt);
        var runK = Math.Clamp((Player.HorizontalSpeed - 3) / 3.3, 0, 1);
        Cam.FovKick += (runK * 6 - Cam.FovKick) * Math.Min(1, dt * 3);
        // в тесноте камера ниже и ближе, без смещения к плечу — иначе упирается в потолок короба
        var inVent = InVent(Player.Position, 0.3);
        Cam.Distance = inVent ? 2.7 : InResidential(Player.Position) ? 2.6 : 3.3;
        Cam.ShoulderOffset = inVent ? 0 : 0.25;
        if (inVent) Cam.Pitch += (0.02 - Cam.Pitch) * Math.Min(1, dt * 6);

        // прогресс по маршруту
        var path = ActivePath;
        (_playerProgress, _) = Project(path, Player.Position, _playerProgress);

        // чекпоинты
        foreach (var (z, pos) in _checkpoints)
        {
            var routeOk = pos.X < -1 ? _route == "residential" : pos.X > 1 ? _route == "ventilation" : true;
            if (Player.Position.Z > z && Player.Grounded && routeOk && pos.Z > _respawn.Z)
            {
                _respawn = pos;
                _respawnYaw = 0;
            }
        }

        // падение в пустоту
        if (Player.Position.Y < -7)
        {
            Damage(20, "Сорвался! Макс цепляется за кабель и выбирается обратно", hard: true);
            Player.Teleport(_respawn, _respawnYaw);
            Cam.Yaw = 0;
            // патруль за время падения подтягивается ближе, но не вплотную — игра продолжается
            var respawnProgress = Project(ActivePath, _respawn, _playerProgress).progress;
            _playerProgress = respawnProgress;
            _guardProgress = Math.Min(_guardProgress + 3, respawnProgress - 7);
            _guardStun = 1.2;
        }

        // удар головой о низкую трубу на бегу
        if (Player.BumpedThisFrame && !Player.Crouching && !Player.Sliding && _hitCooldown <= 0)
        {
            foreach (var c in _lowBeams)
            {
                var p = Player.Position;
                if (p.X > c.Min.X - 0.5 && p.X < c.Max.X + 0.5 && p.Z > c.Min.Z - 0.6 && p.Z < c.Max.Z + 0.6)
                {
                    Damage(12, "Удар о трубу! (C на бегу — проскользнуть)");
                    Player.TakeHit(new Vector3D(0, 0, -2.5));
                    break;
                }
            }
        }

        UpdateRouteChoice();
        UpdateWarnings();
        UpdateVendor();
        UpdateWrench(dt);
        UpdateFinalDoor(dt);

        if (Player.Position.Z > 179)
            StartOutro();
    }


    private bool InVent(Point3D p, double margin) =>
        p.Y < 1 && ((p.X > 3.8 - 0.1 && p.X < 5.2 + 0.1 && p.Z > 108.4 - margin && p.Z < 120.7) ||
                    (p.X > 3.8 && p.X < 9.7 && p.Z > 119.3 && p.Z < 120.7) ||
                    (p.X > 8.3 && p.X < 9.7 && p.Z > 119.3 && p.Z < 134.7) ||
                    (p.X > 3.8 && p.X < 9.7 && p.Z > 133.3 && p.Z < 134.7) ||
                    (p.X > 3.7 && p.X < 5.3 && p.Z > 133.3 && p.Z < 146 + margin));

    private static bool InResidential(Point3D p) => p.X < -2.9 && p.Z > 108.3 && p.Z < 146;

    private void UpdateRouteChoice()
    {
        if (_route is not null) return;
        var p = Player.Position;
        string? chosen = null;
        if (p.Z > 109.2 && p.X < -2.9) chosen = "residential";
        else if (p.Z > 109.2 && p.X > 3.7) chosen = "ventilation";
        if (chosen is null) return;

        _route = chosen;
        Story.CommitRoute(chosen);
        _resDoor.Enabled = true;
        _ventDoor.Enabled = true;
        Audio.Play("door", 0.8);
        Audio.Play("clank", 0.6);
        _playerProgress = Project(ActivePath, p, _playerProgress).progress;
        if (chosen == "residential")
        {
            Hud.Banner("ЖИЛОЙ СЕКТОР", "Короткий путь — через людей");
            Hud.SetObjective("Через жилой сектор", "Пробегите коридор, не сбавляя хода. Обходите жителей");
        }
        else
        {
            Hud.Banner("ВЕНТИЛЯЦИЯ", "Долгий путь — тесно, но безопасно для других");
            Hud.SetObjective("Через вентиляцию", "Пробирайтесь по коробу пригнувшись. Повороты узкие");
        }
    }

    private void UpdateWarnings()
    {
        var z = Player.Position.Z;
        string? w = null;
        if (z is > 4 and < 10.4) w = "Кабели на полу — Space, чтобы перепрыгнуть";
        else if (z is > 17.5 and < 22.8) w = "Низкая труба — C на бегу, чтобы проскользнуть";
        else if (z is > 30 and < 36.2) w = "Кабельная траншея — прыжок с разбега";
        else if (z is > 44 and < 53.6 && _collapseT >= 0) w = "Настил рушится! Прыгайте через провал";
        else if (z is > 56.5 and < 63.6) w = "Балка падает! Уходите вправо";
        else if (z is > 67 and < 80) w = "Узкая балка над бездной — без резких движений";
        else if (z is > 84 and < 88.8) w = "Низкая труба впереди";
        else if (z is > 100 and < 109 && _route is null) w = "Развилка: дверь слева или вентиляция справа";
        else if (z is > 151 and < 156.2) w = "Последний провал — разбег и прыжок!";
        else if (z is > 166 && _finalDoorOpen < 0) w = "Дверь заперта — рубильник справа (E)";
        if (w != _lastWarning)
        {
            _lastWarning = w;
            Hud.SetWarning(w);
        }
    }

    private void Damage(double amount, string message, bool hard = false)
    {
        if (_hitCooldown > 0 && !hard) return;
        _hitCooldown = 1.0;
        _hits++;
        _health = Math.Max(15, _health - amount);
        Hud.SetVitals(_health / 100);
        Hud.FlashDamage();
        Hud.Toast(message);
        Audio.Play("impact", 0.8);
        Cam.AddTrauma(0.45);
        Player.Stumble = Math.Max(Player.Stumble, 0.9);
    }

    // ------------------------------------------------------------------ опасности

    private void UpdateHazards(double dt)
    {
        var z = Player.Position.Z;

        // обрушение настила
        if (_collapseT < 0 && z > 41 && CurrentPhase == Phase.Play)
        {
            _collapseT = 0;
            Audio.Play("creak", 0.9);
            Hud.Toast("Настил впереди трещит…");
        }
        if (_collapseT >= 0)
        {
            _collapseT += dt;
            if (_collapseT < 1.3)
            {
                // тряска-предупреждение
                _collapseMove.OffsetY = Math.Sin(_collapseT * 60) * 0.03;
                if (_rng.NextDouble() < dt * 20)
                    _sparks.Emit(new Point3D(-2 + _rng.NextDouble() * 4, -0.1, 53.5 + (_rng.NextDouble() < 0.5 ? 0 : 2.8)), new Vector3D((_rng.NextDouble() - 0.5) * 3, 1 + _rng.NextDouble() * 2, (_rng.NextDouble() - 0.5) * 3), 0.6, 0.05, 0, 9, 0.3, 0.03);
            }
            else
            {
                if (_collapseCollider.Enabled)
                {
                    _collapseCollider.Enabled = false;
                    Audio.Play("collapse", 1.0);
                    Cam.AddTrauma(0.55);
                    for (var i = 0; i < 30; i++)
                        _dust.Emit(new Point3D(-2 + _rng.NextDouble() * 4, 0, 53.5 + _rng.NextDouble() * 2.8), new Vector3D((_rng.NextDouble() - 0.5) * 2, _rng.NextDouble() * 1.5, (_rng.NextDouble() - 0.5) * 2), 2, 0.6, 1.2, 0.2, 0.8);
                    for (var i = 0; i < 20; i++)
                        _debris.Emit(new Point3D(-2 + _rng.NextDouble() * 4, 0, 53.5 + _rng.NextDouble() * 2.8), new Vector3D((_rng.NextDouble() - 0.5) * 2, _rng.NextDouble() * 2, (_rng.NextDouble() - 0.5) * 2), 2.5, 0.12, 0, 18);
                }
                var t = _collapseT - 1.3;
                _collapseTilt.Angle = Math.Min(35, t * 60);
                _collapseMove.OffsetY = -4.9 * t * t;
                if (_collapseMove.OffsetY < -80) _collapseMove.OffsetY = -80;
            }
        }

        // падающая балка
        var swing = _beamT < 0 ? Math.Sin(Time * 1.6) * 4 : 0;
        _beamSwing.Angle = swing;
        var warnAlpha = _beamT < 0 ? 0.35 + 0.35 * Math.Abs(Math.Sin(Time * 5)) : 0.15;
        _beamWarning.Material = Materials3D.SoftGlow(Red, Math.Round(warnAlpha * 10) / 10);
        if (_beamT < 0 && z > 56.5 && CurrentPhase == Phase.Play)
        {
            _beamT = 0;
            Audio.Play("creak", 1.0);
        }
        if (_beamT >= 0 && _beamT < 0.8)
        {
            _beamT += dt;
            var t = Math.Min(_beamT / 0.8, 1);
            _beamMove.OffsetY = -4.75 * t * t;
            if (_beamT >= 0.8)
            {
                _beamMove.OffsetY = -4.75;
                _beamCollider.Enabled = true;
                Audio.Play("clank", 1.0);
                Audio.Play("impact", 0.9);
                Cam.AddTrauma(0.5);
                for (var i = 0; i < 25; i++)
                    _sparks.Emit(new Point3D(-2 + _rng.NextDouble() * 2.6, 0.3, 63.5), new Vector3D((_rng.NextDouble() - 0.5) * 5, _rng.NextDouble() * 3, (_rng.NextDouble() - 0.5) * 5), 0.7, 0.05, 0, 9, 0.3, 0.03);
                var p = Player.Position;
                if (p.X < 0.9 && p.Z > 62.7 && p.Z < 64.3 && p.Y < 1.5 && CurrentPhase == Phase.Play)
                {
                    Damage(18, "Балка задела Макса!", hard: true);
                    Player.TakeHit(new Vector3D(2.5, 2, 0), 0.5, 1.1);
                }
            }
        }

    }

    private void UpdateVendor()
    {
        if (_vendorKnocked) return;
        var p = Player.Position;
        if (Math.Abs(p.X - (_vendorPos.X - 0.85)) < 1.3 && Math.Abs(p.Z - _vendorPos.Z) < 0.9)
        {
            _vendorKnocked = true;
            Audio.Play("clank", 0.9);
            Audio.Play("wrench", 0.4);
            for (var i = 0; i < 30; i++)
                _debris.Emit(new Point3D(-5.25, 0.85, 128), new Vector3D(1 + _rng.NextDouble() * 2.5, 1 + _rng.NextDouble() * 2, (_rng.NextDouble() - 0.5) * 3), 1.6, 0.07, 0, 12);
            Hud.Toast("Лоток торговца летит на пол — кто-то кричит вслед");
        }
    }

    private void UpdateWrench(double dt)
    {
        if (_route == "ventilation" && !_wrenchDropped && Player.Position.X > 8 && Player.Position.Z > 125.5)
        {
            _wrenchDropped = true;
            var from = Max.HipPosition(1);
            Max.DetachWrench();
            var mb = new MeshBuilder();
            mb.AddBox(new Point3D(0, 0, 0), new Vector3D(0.025, 0.24, 0.012));
            mb.AddBox(new Point3D(0, 0.13, 0), new Vector3D(0.06, 0.04, 0.014));
            mb.AddBox(new Point3D(0, -0.13, 0), new Vector3D(0.05, 0.035, 0.014));
            _fallingWrench = new Model3DGroup();
            _fallingWrench.Children.Add(mb.ToModel(Materials3D.Matte(Materials3D.Hex("#8E9398"), 0.8, 70))!);
            var tg = new Transform3DGroup();
            tg.Children.Add(new RotateTransform3D(_wrenchSpin));
            tg.Children.Add(_wrenchMove);
            _fallingWrench.Transform = tg;
            _wrenchMove.OffsetX = from.X; _wrenchMove.OffsetY = from.Y; _wrenchMove.OffsetZ = from.Z;
            _wrenchVel = new Vector3D(0.3, 0.8, -0.6);
            Dynamic.Children.Add(_fallingWrench);
            Audio.Play("wrench", 0.9);
            Hud.Toast("Старый гаечный ключ выскальзывает из кармана и пропадает в щели решётки");
        }

        if (_fallingWrench is not null)
        {
            _wrenchT += dt;
            _wrenchVel.Y -= 12 * dt;
            _wrenchMove.OffsetX += _wrenchVel.X * dt;
            _wrenchMove.OffsetY += _wrenchVel.Y * dt;
            _wrenchMove.OffsetZ += _wrenchVel.Z * dt;
            _wrenchSpin.Angle += dt * 720;
            // отскок от решётки, потом проваливается в щель
            if (_wrenchMove.OffsetY < 0.03 && _wrenchT < 0.7)
            {
                _wrenchMove.OffsetY = 0.03;
                _wrenchVel = new Vector3D(_wrenchVel.X * 0.4, Math.Abs(_wrenchVel.Y) * 0.4, _wrenchVel.Z * 0.4);
            }
            if (_wrenchT > 2.5)
            {
                Dynamic.Children.Remove(_fallingWrench);
                _fallingWrench = null;
            }
        }
    }

    private void UpdateFinalDoor(double dt)
    {
        var p = Player.Position;
        var nearSwitch = _finalDoorOpen < 0 && Dist2D(p, new Point3D(2.2, 0, 172.3)) < 1.4;
        Hud.SetInteractPrompt(nearSwitch ? "E — дёрнуть рубильник двери" : null);
        if (nearSwitch && Input.InteractPressed)
        {
            _finalDoorOpen = 0;
            Audio.Play("switch", 1.0);
            Audio.Play("door", 1.0);
            Hud.Toast("Рубильник поддаётся — дверь ползёт вверх");
        }
        if (_finalDoorOpen >= 0 && _finalDoorOpen < 1)
        {
            _finalDoorOpen = Math.Min(1, _finalDoorOpen + dt / 1.1);
            _finalDoorMove.OffsetY = 2.3 * _finalDoorOpen;
            if (_finalDoorOpen > 0.75) _finalDoor.Enabled = false;
        }
    }

    // ------------------------------------------------------------------ патруль

    private void UpdateGuards(double dt)
    {
        var path = ActivePath;
        if (!_guardsActive)
        {
            if (CurrentPhase == Phase.Play && PhaseTime > _guardsAppearAt)
            {
                _guardsActive = true;
                _guardProgress = -2;
                Audio.Play("radio", 0.9);
                Hud.Toast("Патруль Администрации — за спиной!");
            }
            return;
        }

        var playing = CurrentPhase == Phase.Play;
        var gap = _playerProgress - _guardProgress;
        _guardStun = Math.Max(0, _guardStun - dt);

        var inVent = _route == "ventilation" && _guardProgress > PathLength(_ventPath, 4);
        double speed = gap > 18 ? 6.1 : gap < 5 ? 4.9 : 5.3;
        if (inVent) speed = 1.8;
        if (!playing) speed = CurrentPhase == Phase.Outro ? 3 : 0;
        if (_guardStun > 0) speed = 0.5;
        _guardProgress += speed * dt;

        // патруль не проходит сквозь закрытую дверь финала
        var doorProgress = PathLength(path, path.Count - 2) - 0.8;
        if (_finalDoorBehind.Enabled || _finalDoorOpen < 0.75)
            _guardProgress = Math.Min(_guardProgress, doorProgress);

        gap = _playerProgress - _guardProgress;
        if (playing)
        {
            _minGuardDistance = Math.Min(_minGuardDistance, gap);
            Hud.SetPursuit(1 - Math.Clamp((gap - 1.3) / 20, 0, 1), gap < 5 ? "Патруль вплотную!" : gap < 11 ? "Патруль близко" : "Патруль");
            if (gap < 1.3 && _guardStun <= 0)
            {
                // догнали: схватка — Макс вырывается, но теряет силы и темп
                Damage(15, "Патрульный хватает Макса — он вырывается!", hard: true);
                Player.TakeHit(new Vector3D(0, 1.5, 0) + PointAt(path, _playerProgress).dir * 3, 0.3, 0.8);
                _guardProgress -= 7;
                _guardStun = 1.6;
                Audio.Play("radio", 0.7);
            }
            if (gap < 9)
                Cam.AddTrauma(dt * 0.25);
        }

        for (var i = 0; i < _guards.Count; i++)
        {
            var g = _guards[i];
            var prog = _guardProgress - g.Lag;
            var (pos, dir) = PointAt(path, prog);
            var narrow = pos.Z is > 69 and < 81 || inVent || (pos.X < -2.9 && pos.Z > 108);
            var lateral = narrow ? 0 : g.Lateral;
            var right = new Vector3D(-dir.Z, 0, dir.X);
            var y = GroundAlongPath(pos);
            var world = new Point3D(pos.X + right.X * lateral, y, pos.Z + right.Z * lateral);
            g.Rig.Position = world;
            g.Rig.YawDegrees = Math.Atan2(dir.X, dir.Z) * 180 / Math.PI;
            var moving = speed > 1 && prog > -1;
            g.Rig.Update(dt, new LocomotionState
            {
                Speed = moving ? speed : 0,
                RunSpeed = 5.9,
                Grounded = y <= 0.01,
                VerticalVelocity = y > 0.01 ? 1 : 0,
                Crouch = inVent ? 1 : 0,
                Searching = !moving || _guardStun > 0
            });
            if (i == 0)
            {
                var (o, d) = g.Rig.FlashlightRay();
                _guardLight.Position = o;
                _guardLight.Direction = d;
            }
        }
        Audio.SetLoop("guard_steps", playing ? Math.Clamp(0.7 - gap / 30, 0, 0.6) : 0);
    }

    private double _guardProgress = -10;

    /// <summary>Над провалами патрульные перепрыгивают — дуга по высоте.</summary>
    private double GroundAlongPath(Point3D p)
    {
        double Arc(double z0, double z1) => p.Z > z0 && p.Z < z1 ? Math.Sin((p.Z - z0) / (z1 - z0) * Math.PI) * 0.9 : 0;
        return Arc(35.6, 37.8) + (_collapseCollider.Enabled ? 0 : Arc(53.1, 56.7)) + Arc(155.7, 159.1) + (_beamCollider.Enabled && p.X < 0.6 ? Arc(62.6, 64.4) * 0.8 : 0);
    }

    // ------------------------------------------------------------------ окружение

    private void UpdateEnvironment(double dt)
    {
        _elevatorMove.OffsetY = Math.Sin(Time * 0.12) * 40;
        foreach (var f in _fans) f.Angle = (f.Angle + dt * 600) % 360;

        foreach (var sp in _sparkPoints)
        {
            if (_rng.NextDouble() < dt * 1.2 && Dist2D(sp, Player.Position) < 35)
            {
                for (var i = 0; i < 12; i++)
                    _sparks.Emit(sp, new Vector3D((_rng.NextDouble() - 0.5) * 3, _rng.NextDouble() * 2, (_rng.NextDouble() - 0.5) * 3), 0.5 + _rng.NextDouble() * 0.5, 0.05, 0, 9, 0.3, 0.03);
                if (Dist2D(sp, Player.Position) < 14) Audio.Play("spark", 0.4);
            }
        }
        foreach (var j in _steamJets)
        {
            if (_rng.NextDouble() < dt * 14 && Dist2D(j, Player.Position) < 30)
            {
                var dir = j.X < 0 ? 1 : -1;
                _steam.Emit(j, new Vector3D(dir * (1.5 + _rng.NextDouble()), 0.5 + _rng.NextDouble() * 0.4, (_rng.NextDouble() - 0.5) * 0.4), 1.4, 0.3, 0.9, 0, 0.6);
            }
        }
        Audio.SetLoop("steam", Math.Clamp(0.5 - Math.Abs(Player.Position.Z - 88) / 20, 0, 0.5));

        // аварийная пульсация
        var pulse = 0.55 + 0.45 * Math.Max(0, Math.Sin(Time * 4));
        _ambient.Color = Color.FromRgb((byte)(46 + 14 * pulse), 74, 80);
        _residentialLight.Color = InResidential(Player.Position) || Player.Position.Z > 100 ? Color.FromRgb(255, 170, 100) : Color.FromRgb(0, 0, 0);
    }

    protected override double LampIntensity(Point3D anchor) => 0.35 + 0.65 * Math.Max(0, Math.Sin(Time * 4 + anchor.Z * 0.15));

    // ------------------------------------------------------------------ финал

    private bool _outroStarted;

    private void StartOutro()
    {
        if (_outroStarted) return;
        _outroStarted = true;
        EnterPhase(Phase.Outro);
        Hud.SetWarning(null);
        Hud.SetInteractPrompt(null);
        _finalDoorBehind.Enabled = true;
        Audio.Play("door", 0.9);
        Audio.SetLoop("guard_steps", 0);
        Cam.SetCinematic(new Point3D(1.2, 1.5, 184.2), new Point3D(0, 1.1, 181.5), 1.0);
    }

    private void UpdateOutro(double dt)
    {
        // дверь опускается за спиной, Макс по инерции пробегает несколько шагов и останавливается
        _finalDoorBehindMove.OffsetY = Math.Max(0, 2.5 - PhaseTime * 3);
        var scripted = new EpisodeInput { Scripted = true, Forward = PhaseTime < 0.5 };
        scripted.Poll(null, false);
        Player.ForceCrouch = false;
        Player.Update(dt, scripted, new Vector3D(0, 0, 1), new Vector3D(-1, 0, 0));
        Cam.FovKick += (0 - Cam.FovKick) * Math.Min(1, dt * 2);
        if (PhaseTime > 1.0 && PhaseTime - dt <= 1.0)
            Audio.Play("breath", 0.8);
        Hud.SetFade(Math.Clamp((PhaseTime - 3.6) / 0.9, 0, 1));
        if (PhaseTime > 4.6)
        {
            var outcome = _hits == 0 && _minGuardDistance > 3 ? "PERFECT" : _hits >= 3 || _health < 50 ? "ROUGH" : "NORMAL";
            FinishEpisode(outcome, _route ?? "residential");
        }
    }

    protected override LocomotionState CharacterState() =>
        Player.Locomotion(breathless: CurrentPhase == Phase.Outro && PhaseTime > 0.8);

    protected override string FootstepSound() =>
        InResidential(Player.Position) ? "step_concrete" : (Time % 2 < 1 ? "step_metal" : "step_metal2");

    protected override double CameraPivotHeight() =>
        InVent(Player.Position, 0.3) ? 0.82 : Player.Crouching || Player.Sliding ? 1.0 : 1.65;

    // ------------------------------------------------------------------ автопроверка

    /// <summary>Для автопрогона: текущие показатели эпизода.</summary>
    public (double progress, double guardGap, int hits, string? route, double z) Debug =>
        (_playerProgress, _playerProgress - _guardProgress, _hits, _route, Player.Position.Z);
}
