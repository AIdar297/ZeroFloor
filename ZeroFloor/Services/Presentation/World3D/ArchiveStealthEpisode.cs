using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ZeroFloor.Services;

namespace ZeroFloor.Services.Presentation.World3D;

/// <summary>
/// Глава 8, сцена 8.1 «Проникновение в Архив Смотрителей»: Макс и Виктор пробираются через
/// заброшенный архив к терминалу с досье. Поворотные камеры старого образца вращаются «по двум
/// циклам» (как в тексте главы) — их конусы видны; лазерный барьер перед терминальной требует
/// пригнуться и перепрыгнуть; щитки питания (E) на несколько секунд гасят камеры своего крыла.
/// Виктор идёт следом по пути Макса и тоже может попасть под камеру. Исход: CLEAN или SPOTTED
/// (тревога: красный свет, сирена — но путь к терминалу открыт, game over нет).
/// </summary>
public sealed class ArchiveStealthEpisode : EpisodeRuntime
{
    private const double GoalZ = 59.5;

    private readonly Dictionary<Material, MeshBuilder> _batches = new();
    private readonly Dictionary<Material, MeshBuilder> _decals = new();
    private readonly Random _rng = new(8);
    private readonly List<SecurityCamera> _cameras = new();
    private readonly List<PowerBox> _powerBoxes = new();
    private readonly List<(double z, double y, GeometryModel3D beam)> _lasers = new();
    private readonly List<Point3D> _trail = new();

    private HumanoidRig _viktor = null!;
    private Point3D _viktorPos;
    private double _viktorYaw;
    private double _viktorSpeed;
    private ParticleSystem3D _dust = null!;
    private ParticleSystem3D _sparks = null!;
    private AmbientLight _ambient = null!;
    private DirectionalLight _alarmLight = null!;

    private double _detection;
    private bool _spotted;
    private double _tick;
    private bool _outro;
    private int _section;
    private PowerBox? _focused;

    public ArchiveStealthEpisode(Viewport3D viewport, IEpisodeHud hud, IEpisodeStoryBridge story, ISettingsStore settings)
        : base(viewport, hud, story, settings)
    {
    }

    // ================================================================== модели

    /// <summary>Поворотная камера наблюдения: кронштейн, корпус, объектив, красный диод и конус.</summary>
    private sealed class SecurityCamera
    {
        public required Point3D Mount;
        public required double BaseYaw;
        public required double AmpA;
        public required double AmpB;
        public required double SpeedA;
        public required double SpeedB;
        public double Pitch = 38;
        public double Range = 9.5;
        public double HalfAngle = 19;
        public int Wing;
        public double DisabledFor;
        public double Yaw;
        public readonly AxisAngleRotation3D YawRot = new(new Vector3D(0, 1, 0), 0);
        public readonly MatrixTransform3D ConeTransform = new();
        public GeometryModel3D Cone = null!;
        public GeometryModel3D Led = null!;
        public Material LedOn = null!;
        public Material LedOff = null!;

        public Vector3D Direction
        {
            get
            {
                var y = Yaw * Math.PI / 180;
                var p = Pitch * Math.PI / 180;
                return new Vector3D(Math.Sin(y) * Math.Cos(p), -Math.Sin(p), Math.Cos(y) * Math.Cos(p));
            }
        }

        public Point3D Eye => Mount + new Vector3D(Math.Sin(Yaw * Math.PI / 180) * 0.25, -0.15, Math.Cos(Yaw * Math.PI / 180) * 0.25);
    }

    private sealed class PowerBox
    {
        public required Point3D Spot;
        public required int Wing;
        public required GeometryModel3D Lamp;
        public double Cooldown;
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

    private void Solid(Point3D min, Point3D max, Material mat, ColliderFlags flags = ColliderFlags.Block, double uv = 1.0)
    {
        B(mat).AddBoxMinMax(min, max, uv);
        World.Add(min, max, flags);
    }

    protected override void BuildLevel()
    {
        Player.WalkSpeed = 2.8;
        Player.RunSpeed = 5.2;
        Player.CrouchSpeed = 1.6;
        Player.Teleport(new Point3D(0, 0, 1.2), 0);
        Cam.Yaw = 0;
        Cam.Pitch = 0.24;
        Cam.Distance = 3.3;

        _ambient = new AmbientLight(Color.FromRgb(52, 60, 70));
        Lights.Children.Add(_ambient);
        Lights.Children.Add(new DirectionalLight(Color.FromRgb(120, 130, 150), new Vector3D(-0.4, -1, 0.25)));
        _alarmLight = new DirectionalLight(Colors.Black, new Vector3D(0.2, -1, -0.2));
        Lights.Children.Add(_alarmLight);
        SetupLightPool(3, Color.FromRgb(200, 225, 255), 10);

        var floor = Materials3D.Concrete(Color.FromRgb(58, 58, 60));
        var wall = Materials3D.Concrete(Color.FromRgb(66, 68, 72));
        var steel = Materials3D.MetalPanel(Color.FromRgb(60, 64, 70), 0.25, 0.3);

        // ---- пол, стены, потолок
        B(floor).AddFloorGrid(-9.5, -1, 9.5, 66, 0, 1.2, 2.5);
        World.Add(new Point3D(-9.5, -0.5, -1), new Point3D(9.5, 0, 66), ColliderFlags.Floor);
        void Wall(double x0, double z0, double x1, double z1, double h, Vector3D n)
        {
            B(wall).AddWallGrid(new Point3D(x0, 0, z0), new Point3D(x1, 0, z1), h, n, 1.6, 2.5);
            var t = 0.3;
            World.Add(new Point3D(Math.Min(x0, x1) - (n.X > 0 ? t : 0), 0, Math.Min(z0, z1) - (n.Z > 0 ? t : 0)),
                new Point3D(Math.Max(x0, x1) + (n.X < 0 ? t : 0), h, Math.Max(z0, z1) + (n.Z < 0 ? t : 0)), ColliderFlags.Wall);
        }
        // служебный коридор входа
        Wall(-2, -1, -2, 12, 4, new Vector3D(1, 0, 0));
        Wall(2, -1, 2, 12, 4, new Vector3D(-1, 0, 0));
        Wall(-2, -1, 2, -1, 4, new Vector3D(0, 0, 1));
        B(Materials3D.Concrete(Color.FromRgb(36, 38, 40))).AddFloorGrid(-2, -1, 2, 12, 4, 2, 3, faceDown: true);
        World.Add(new Point3D(-2.2, 4, -1), new Point3D(2.2, 4.4, 12), ColliderFlags.BlocksCamera);
        // главный зал
        Wall(-9, 12, -9, 48, 7, new Vector3D(1, 0, 0));
        Wall(9, 12, 9, 48, 7, new Vector3D(-1, 0, 0));
        Wall(-9, 12, -2, 12, 7, new Vector3D(0, 0, 1));
        Wall(2, 12, 9, 12, 7, new Vector3D(0, 0, 1));
        Wall(-9, 48, -2, 48, 7, new Vector3D(0, 0, -1));
        Wall(2, 48, 9, 48, 7, new Vector3D(0, 0, -1));
        Solid(new Point3D(-2, 4, 11.8), new Point3D(2, 7, 12.2), wall, ColliderFlags.Wall);
        Solid(new Point3D(-2, 3, 47.8), new Point3D(2, 7, 48.2), wall, ColliderFlags.Wall);
        B(Materials3D.Concrete(Color.FromRgb(30, 32, 34))).AddFloorGrid(-9, 12, 9, 48, 7, 3, 4, faceDown: true);
        World.Add(new Point3D(-9.2, 7, 12), new Point3D(9.2, 7.5, 48), ColliderFlags.BlocksCamera);
        // коридор лазеров и терминальная
        Wall(-2, 48, -2, 56, 3, new Vector3D(1, 0, 0));
        Wall(2, 48, 2, 56, 3, new Vector3D(-1, 0, 0));
        B(Materials3D.Concrete(Color.FromRgb(30, 32, 34))).AddFloorGrid(-2, 48, 2, 56, 3, 2, 3, faceDown: true);
        World.Add(new Point3D(-2.2, 3, 48), new Point3D(2.2, 3.4, 56), ColliderFlags.BlocksCamera);
        Wall(-5, 56, -5, 65, 3.6, new Vector3D(1, 0, 0));
        Wall(5, 56, 5, 65, 3.6, new Vector3D(-1, 0, 0));
        Wall(-5, 56, -2, 56, 3.6, new Vector3D(0, 0, 1));
        Wall(2, 56, 5, 56, 3.6, new Vector3D(0, 0, 1));
        Wall(-5, 65, 5, 65, 3.6, new Vector3D(0, 0, -1));
        B(Materials3D.Concrete(Color.FromRgb(28, 30, 32))).AddFloorGrid(-5, 56, 5, 65, 3.6, 2, 3, faceDown: true);
        World.Add(new Point3D(-5.2, 3.6, 56), new Point3D(5.2, 4, 65), ColliderFlags.BlocksCamera);

        BuildHall(steel);
        BuildTerminalRoom();
        BuildCameras();
        BuildLasers();
        BuildPowerBoxes();

        foreach (var (m, b) in _batches) { var model = b.ToModel(m); if (model is not null) Static.Children.Add(model); }
        foreach (var (m, b) in _decals) { var model = b.ToModel(m, m); if (model is not null) Transparent.Children.Add(model); }

        // Виктор
        _viktor = new HumanoidRig(HumanoidAppearance.ViktorSavchuk(), 31);
        _viktorPos = new Point3D(0.6, 0, -0.4);
        _viktor.Position = _viktorPos;
        Dynamic.Children.Add(_viktor.Root);

        _dust = AddParticles(a => Materials3D.SoftGlow(Color.FromRgb(230, 220, 200), 0.3 * a), 140, fadeIn: true);
        _sparks = AddParticles(a => Materials3D.Emissive(Color.FromRgb(255, (byte)(150 + 90 * a), 60), 1.0), 80);

        Hud.SetControls(BaseControls(slide: false));
        Hud.SetObjective("Архив Смотрителей", "Пройдите вместе с Виктором к терминалу досье");
        Hud.SetDetection(0, MeterLevel.Calm, "Камеры");
        Hud.SetPursuit(null, "");
        Hud.SetVitals(null);
        Audio.SetLoop("shaft_amb", 0.35, ambience: true);
    }

    private void BuildHall(Material steel)
    {
        var desk = Materials3D.MetalPanel(Color.FromRgb(70, 72, 74), 0.3, 0.25);
        var deskTop = Materials3D.Matte(Color.FromRgb(86, 84, 80), 0.1, 10);
        var crt = Materials3D.MetalPanel(Color.FromRgb(132, 128, 116), 0.15, 0.2);
        var rack = Materials3D.MetalPanel(Color.FromRgb(30, 32, 36), 0.1, 0.4);
        var cable = Materials3D.Matte(Color.FromRgb(16, 16, 18), 0.3, 20);

        // два ряда столов с мониторами (укрытие — пригнуться)
        foreach (var x in new[] { -4.6, 4.6 })
        {
            foreach (var (z0, z1) in new[] { (15.0, 23.0), (25.5, 33.5), (36.0, 44.0) })
            {
                Solid(new Point3D(x - 0.7, 0, z0), new Point3D(x + 0.7, 0.95, z1), desk);
                B(deskTop).AddBoxMinMax(new Point3D(x - 0.75, 0.95, z0 - 0.05), new Point3D(x + 0.75, 1.0, z1 + 0.05));
                for (var z = z0 + 0.8; z < z1 - 0.4; z += 1.6)
                {
                    var side = x < 0 ? 1 : -1;
                    B(crt).AddBoxMinMax(new Point3D(x - 0.28, 1.0, z - 0.3), new Point3D(x + 0.28, 1.5, z + 0.3));
                    var lit = _rng.NextDouble() < 0.55;
                    B(lit ? Materials3D.Screen(Color.FromRgb(90, 255, 140)) : Materials3D.Matte(Color.FromRgb(20, 26, 24), 0.6, 60))
                        .AddBox(new Point3D(x + side * 0.285, 1.27, z), new Vector3D(0.012, 0.36, 0.44));
                    if (lit)
                        D(Materials3D.SoftGlow(Color.FromRgb(90, 255, 140), 0.25)).AddPanel(new Point3D(x + side * 0.32, 1.27, z), new Vector3D(0, 0, 1), new Vector3D(0, 1, 0), 1.0, 0.8);
                    B(Materials3D.Matte(Color.FromRgb(60, 58, 54))).AddBoxAt(new Point3D(x + side * 0.45, 1.01, z), new Vector3D(0.2, 0.025, 0.45), yawDeg: _rng.Next(-8, 8));
                }
            }
        }

        // серверные стойки вдоль стен (высокое укрытие) с огоньками
        foreach (var x in new[] { -8.3, 8.3 })
        {
            for (var z = 14.0; z < 46; z += 3.4)
            {
                if (x < 0 && (Math.Abs(z - 27.6) < 1 || Math.Abs(z - 14.0) < 1)) continue; // ниши у щитков
                Solid(new Point3D(x - 0.55, 0, z), new Point3D(x + 0.55, 2.3, z + 1.3), rack);
                var face = x < 0 ? x + 0.56 : x - 0.56;
                for (var i = 0; i < 9; i++)
                    B(Materials3D.Emissive(_rng.NextDouble() < 0.7 ? Color.FromRgb(80, 255, 120) : Color.FromRgb(255, 170, 60), 0.9))
                        .AddBox(new Point3D(face, 0.4 + i * 0.2, z + 0.2 + _rng.NextDouble() * 0.9), new Vector3D(0.01, 0.025, 0.05));
            }
        }
        // центральный островок стоек
        Solid(new Point3D(-1.0, 0, 27), new Point3D(1.0, 2.3, 29.4), rack);
        Solid(new Point3D(-1.0, 0, 38.5), new Point3D(1.0, 2.3, 40.6), rack);
        foreach (var z in new[] { 27.0, 29.4, 38.5, 40.6 })
            for (var i = 0; i < 6; i++)
                B(Materials3D.Emissive(Color.FromRgb(80, 255, 120), 0.8)).AddBox(new Point3D(-0.6 + i * 0.24, 0.5 + (i % 3) * 0.5, z + (z % 1 == 0 ? -0.01 : 0.01)), new Vector3D(0.05, 0.025, 0.01));
        // картотечные шкафы — укрытие в проходах
        foreach (var (x, z) in new[] { (-2.4, 19.0), (2.4, 34.5), (-2.6, 44.5) })
            Solid(new Point3D(x - 0.45, 0, z - 0.3), new Point3D(x + 0.45, 1.35, z + 0.3), Materials3D.MetalPanel(Color.FromRgb(96, 92, 80), 0.4, 0.3));

        // кабели на полу, трубы под потолком, окна-фонари со светом
        for (var i = 0; i < 9; i++)
        {
            var z = 14 + i * 3.8;
            B(cable).AddSaggingCable(new Point3D(-7.5 + _rng.NextDouble() * 3, 0.04, z), new Point3D(-1 + _rng.NextDouble() * 5, 0.04, z + 1.5), -0.0, 0.03, 6);
        }
        var pipe = Materials3D.MetalPanel(Color.FromRgb(90, 94, 98), 0.4, 0.35);
        B(pipe).AddTube(new Point3D(-6, 6.0, 12), new Point3D(-6, 6.0, 48), 0.45, 14);
        B(pipe).AddTube(new Point3D(6, 6.2, 12), new Point3D(6, 6.2, 48), 0.38, 14);
        for (var z = 14.0; z < 48; z += 4)
            B(cable).AddSaggingCable(new Point3D(-9, 6.5, z), new Point3D(9, 6.4, z + 1.4), 0.9, 0.03);
        for (var z = 16.0; z < 46; z += 7)
        {
            B(Materials3D.Emissive(Color.FromRgb(170, 190, 220), 0.55)).AddBox(new Point3D(8.97, 4.6, z), new Vector3D(0.04, 2.0, 2.4));
            // косые лучи света из окон (как на фоне сцены)
            D(Materials3D.Glow(Color.FromRgb(200, 210, 230), 0.07)).AddQuad(
                new Point3D(8.9, 5.6, z - 1.2), new Point3D(8.9, 5.6, z + 1.2), new Point3D(3.5, 0.02, z + 1.8), new Point3D(3.5, 0.02, z - 0.6),
                new Vector3D(-1, 0.5, 0), new System.Windows.Point(0, 0), new System.Windows.Point(1, 0), new System.Windows.Point(1, 1), new System.Windows.Point(0, 1));
        }
        // подвесные лампы дневного света
        foreach (var (x, z) in new[] { (0.0, 4.0), (0.0, 18.0), (0.0, 31.0), (0.0, 43.0), (0.0, 52.0), (0.0, 60.0) })
        {
            var y = z < 12 || z > 47 ? (z > 55 ? 3.3 : 2.75) : 5.2;
            B(Materials3D.Matte(Color.FromRgb(40, 40, 40))).AddBoxMinMax(new Point3D(x - 0.7, y + 0.05, z - 0.1), new Point3D(x + 0.7, y + 0.12, z + 0.1));
            B(Materials3D.Emissive(Color.FromRgb(220, 235, 255), 1.0)).AddBoxMinMax(new Point3D(x - 0.65, y, z - 0.06), new Point3D(x + 0.65, y + 0.05, z + 0.06));
            D(Materials3D.SoftGlow(Color.FromRgb(200, 225, 255), 0.18)).AddPanel(new Point3D(x, 0.03, z), new Vector3D(1, 0, 0), new Vector3D(0, 0, -1), 5, 4, doubleSided: false);
            AddLamp(new Point3D(x, y - 0.2, z));
        }

        // вход: решётчатая дверь, пыльная табличка
        B(Materials3D.Stencil("АРХИВ СМОТРИТЕЛЕЙ · ДОСТУП ЗАКРЫТ", Color.FromRgb(210, 200, 170), Color.FromRgb(48, 50, 52), 34)).AddBox(new Point3D(0, 3.3, -0.98), new Vector3D(3.0, 0.4, 0.02));
    }

    private void BuildTerminalRoom()
    {
        var term = Materials3D.MetalPanel(Color.FromRgb(74, 78, 76), 0.25, 0.35);
        Solid(new Point3D(-1.4, 0, 62.4), new Point3D(1.4, 1.0, 63.4), term);
        B(Materials3D.Screen(Color.FromRgb(90, 255, 140))).AddBoxAt(new Point3D(0, 1.45, 62.75), new Vector3D(1.1, 0.75, 0.05), pitchDeg: -10);
        D(Materials3D.SoftGlow(Color.FromRgb(90, 255, 140), 0.35)).AddPanel(new Point3D(0, 1.45, 62.65), new Vector3D(-1, 0, 0), new Vector3D(0, 1, 0), 2.6, 1.8);
        B(Materials3D.Matte(Color.FromRgb(40, 40, 42))).AddBoxMinMax(new Point3D(-0.6, 1.0, 62.0), new Point3D(0.6, 1.05, 62.4));
        // доска с обрывками (сцена 8.2) — видна в финале эпизода
        B(Materials3D.Wood(Color.FromRgb(110, 84, 58))).AddBoxMinMax(new Point3D(-4.9, 0.9, 58), new Point3D(-4.8, 2.6, 62));
        for (var i = 0; i < 14; i++)
            B(Materials3D.Matte(Color.FromRgb(220, 210, 185))).AddBoxAt(new Point3D(-4.78, 1.1 + _rng.NextDouble() * 1.3, 58.3 + _rng.NextDouble() * 3.4), new Vector3D(0.01, 0.18, 0.26), rollDeg: _rng.Next(-15, 15));
        Solid(new Point3D(3.8, 0, 57), new Point3D(4.9, 2.4, 64.5), Materials3D.MetalPanel(Color.FromRgb(30, 32, 36), 0.1, 0.4));
    }

    private void BuildCameras()
    {
        AddCamera(new Point3D(-8.75, 3.4, 20), baseYaw: 90, ampA: 55, ampB: 18, speedA: 0.42, speedB: 1.1, wing: 0);
        AddCamera(new Point3D(8.75, 3.4, 29), baseYaw: -90, ampA: 50, ampB: 20, speedA: 0.37, speedB: 0.9, wing: 0);
        AddCamera(new Point3D(0, 5.6, 35.5), baseYaw: 180, ampA: 80, ampB: 25, speedA: 0.3, speedB: 0.8, wing: 1, pitch: 52, range: 10.5);
        AddCamera(new Point3D(-8.75, 3.4, 42), baseYaw: 110, ampA: 40, ampB: 15, speedA: 0.5, speedB: 1.3, wing: 1);
    }

    private void AddCamera(Point3D mount, double baseYaw, double ampA, double ampB, double speedA, double speedB, int wing, double pitch = 38, double range = 9.5)
    {
        var cam = new SecurityCamera
        {
            Mount = mount, BaseYaw = baseYaw, AmpA = ampA, AmpB = ampB, SpeedA = speedA, SpeedB = speedB, Wing = wing, Pitch = pitch, Range = range,
            LedOn = Materials3D.Emissive(Color.FromRgb(255, 40, 30), 1.3),
            LedOff = Materials3D.Matte(Color.FromRgb(40, 20, 20))
        };
        // кронштейн
        var dark = Materials3D.Matte(Color.FromRgb(36, 38, 42), 0.4, 40);
        B(dark).AddBox(mount + new Vector3D(0, 0.15, 0), new Vector3D(0.12, 0.3, 0.12));
        var head = new Model3DGroup();
        var hmb = new MeshBuilder();
        hmb.AddBox(new Point3D(0, -0.12, 0.15), new Vector3D(0.2, 0.18, 0.42));
        hmb.AddBox(new Point3D(0, -0.02, 0.18), new Vector3D(0.26, 0.03, 0.5));
        head.Children.Add(hmb.ToModel(Materials3D.MetalPanel(Color.FromRgb(200, 196, 186), 0.15, 0.4))!);
        var lens = new MeshBuilder();
        lens.AddCylinder(new Point3D(0, 0, 0), 0.06, 0.04, 10, MeshBuilder.Trs(new Vector3D(0, -0.12, 0.37), pitchDeg: 90), true);
        head.Children.Add(lens.ToModel(Materials3D.Matte(Color.FromRgb(10, 12, 16), 0.8, 80))!);
        var led = new MeshBuilder();
        led.AddBox(new Point3D(0.07, -0.05, 0.36), new Vector3D(0.025, 0.025, 0.02));
        cam.Led = new GeometryModel3D(led.Build(), cam.LedOn);
        head.Children.Add(cam.Led);
        var tg = new Transform3DGroup();
        tg.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), pitch * 0.6)));
        tg.Children.Add(new RotateTransform3D(cam.YawRot));
        tg.Children.Add(new TranslateTransform3D(mount.X, mount.Y, mount.Z));
        head.Transform = tg;
        Dynamic.Children.Add(head);

        var coneMat = Materials3D.Glow(Color.FromRgb(255, 50, 40), 0.12);
        var cmb = new MeshBuilder();
        cmb.AddCone(new Point3D(0, 0, 0), new Vector3D(0, 1, 0), range, Math.Tan(cam.HalfAngle * Math.PI / 180) * range, 22);
        cam.Cone = new GeometryModel3D(cmb.Build(), coneMat) { BackMaterial = coneMat, Transform = cam.ConeTransform };
        Transparent.Children.Add(cam.Cone);
        _cameras.Add(cam);
    }

    private void BuildLasers()
    {
        var emitter = Materials3D.MetalPanel(Color.FromRgb(50, 52, 56), 0.1, 0.4);
        foreach (var (z, y) in new[] { (50.5, 1.25), (50.5, 1.6), (53.2, 0.28) })
        {
            B(emitter).AddBox(new Point3D(-1.93, y, z), new Vector3D(0.12, 0.14, 0.14));
            B(emitter).AddBox(new Point3D(1.93, y, z), new Vector3D(0.12, 0.14, 0.14));
            var mb = new MeshBuilder();
            mb.AddBox(new Point3D(0, y, z), new Vector3D(3.8, 0.02, 0.02));
            var beam = new GeometryModel3D(mb.Build(), Materials3D.Emissive(Color.FromRgb(255, 30, 30), 1.4));
            Static.Children.Add(beam);
            D(Materials3D.Glow(Color.FromRgb(255, 40, 40), 0.18)).AddPanel(new Point3D(0, y, z), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), 3.8, 0.09);
            _lasers.Add((z, y, beam));
        }
        B(Materials3D.Hazard()).AddBoxMinMax(new Point3D(-2, 0.005, 49.6), new Point3D(2, 0.02, 49.9));
    }

    private void BuildPowerBoxes()
    {
        // оба щитка — в нишах левого прохода: первый гасит камеры первого крыла, второй — дальнего
        foreach (var (spot, wing, wallX) in new[] { (new Point3D(-7.9, 0, 14.6), 0, -8.95), (new Point3D(-7.9, 0, 28.2), 1, -8.95) })
        {
            var side = wallX < 0 ? 1 : -1;
            var box = new Point3D(wallX + side * 0.12, 1.35, spot.Z);
            B(Materials3D.MetalPanel(Color.FromRgb(96, 100, 96), 0.5, 0.3)).AddBox(box, new Vector3D(0.2, 0.75, 0.6));
            B(Materials3D.Hazard()).AddBox(box + new Vector3D(side * 0.105, 0.28, 0), new Vector3D(0.01, 0.1, 0.55));
            B(Materials3D.Matte(Color.FromRgb(20, 20, 20))).AddTube(box + new Vector3D(0, 0.38, 0), new Point3D(box.X, 6.5, box.Z), 0.04, 6);
            var lmb = new MeshBuilder();
            lmb.AddBox(box + new Vector3D(side * 0.105, -0.2, 0.18), new Vector3D(0.015, 0.07, 0.07));
            var lamp = new GeometryModel3D(lmb.Build(), Materials3D.Emissive(Color.FromRgb(80, 255, 120), 1.2));
            Static.Children.Add(lamp);
            _powerBoxes.Add(new PowerBox { Spot = spot, Wing = wing, Lamp = lamp });
        }
    }

    // ================================================================== цикл

    protected override void Update(double dt)
    {
        switch (CurrentPhase)
        {
            case Phase.Intro: UpdateIntro(); break;
            case Phase.Play: UpdatePlay(dt); break;
            case Phase.Outro: UpdateOutro(dt); break;
        }
        if (CurrentPhase == Phase.Intro || CurrentPhase == Phase.Outro)
            Player.Update(dt, ScriptedIdle(), Cam.Forward, Cam.Right, allowInput: false);
        UpdateViktor(dt);
        UpdateCameras(dt);
        UpdateAmbient(dt);
    }

    private static EpisodeInput ScriptedIdle()
    {
        var i = new EpisodeInput { Scripted = true };
        i.Poll(null, false);
        return i;
    }

    private void UpdateIntro()
    {
        if (PhaseTime < 0.01)
        {
            Cam.SnapCinematic(new Point3D(1.4, 1.7, 5.2), new Point3D(0.2, 1.3, 0.4));
            Hud.Banner("ГЛАВА 8 · АРХИВ СМОТРИТЕЛЕЙ", "Камеры старого образца вращаются по двум циклам");
        }
        Hud.SetFade(Math.Max(0, 1 - PhaseTime / 0.9));
        if (PhaseTime > 1.8) Cam.ReleaseCinematic(2.2);
        if (PhaseTime > 2.7)
            EnterPhase(Phase.Play);
    }

    private void UpdatePlay(double dt)
    {
        Player.Update(dt, Input, Cam.Forward, Cam.Right);

        // след Макса — по нему идёт Виктор
        if (_trail.Count == 0 || (Player.Position - _trail[^1]).Length > 0.35)
            _trail.Add(Player.Position);

        var section = Player.Position.Z < 12 ? 1 : Player.Position.Z < 48 ? 2 : 3;
        if (section != _section)
        {
            _section = section;
            if (section == 2)
            {
                Hud.Banner("ЗАЛ ХРАНЕНИЯ", "Следите за конусами камер. Щитки (E) гасят своё крыло на время");
                Hud.SetObjective("Зал хранения", "Перебегайте между столами и стойками, пока камеры смотрят в сторону. C — пригнуться");
            }
            else if (section == 3)
            {
                Hud.Banner("ЛАЗЕРНЫЙ БАРЬЕР", "Под верхние лучи — пригнуться, над нижним — прыжок");
                Hud.SetObjective("К терминалу", _spotted ? "Тревога! Быстрее к терминалу досье" : "Пройдите лазерный барьер к терминалу");
            }
            Audio.Play("objective", 0.5);
        }

        // щитки питания
        _focused = null;
        foreach (var pb in _powerBoxes)
        {
            pb.Cooldown = Math.Max(0, pb.Cooldown - dt);
            if (pb.Cooldown <= 0 && Dist2D(pb.Spot, Player.Position) < 1.3)
                _focused = pb;
        }
        Hud.SetInteractPrompt(_focused is null ? null : "E — обесточить камеры этого крыла");
        if (_focused is not null && Input.InteractPressed)
        {
            var pb = _focused;
            pb.Cooldown = 25;
            foreach (var c in _cameras.Where(c => c.Wing == pb.Wing)) c.DisabledFor = 12;
            Audio.Play("switch", 1.0);
            Audio.Play("spark", 0.7);
            for (var i = 0; i < 25; i++)
                _sparks.Emit(pb.Spot + new Vector3D(Math.Sign(-pb.Spot.X) * -0.9, 1.4, 0), new Vector3D((_rng.NextDouble() - 0.5) * 3, _rng.NextDouble() * 3, (_rng.NextDouble() - 0.5) * 3), 0.6, 0.05, 0, 9, 0.4, 0.03);
            Hud.Toast("Щиток щёлкает — камеры крыла гаснут на 12 секунд");
        }

        UpdateDetection(dt);
        CheckLasers();

        if (Player.Position.Z > GoalZ && !_outro)
            StartOutro();
    }

    private void CheckLasers()
    {
        if (_spotted) return;
        // Виктор повторяет движения Макса по его следу, поэтому луч проверяется по Максу
        var pos = Player.Position;
        foreach (var (z, y, _) in _lasers)
        {
            if (Math.Abs(pos.Z - z) < 0.25 && y > pos.Y + 0.05 && y < pos.Y + Player.CurrentHeight)
            {
                Hud.Toast("Луч пересечён — сигнализация архива!");
                Spot();
                return;
            }
        }
    }

    private void UpdateDetection(double dt)
    {
        if (_spotted)
        {
            Hud.SetDetection(1, MeterLevel.Danger, "ТРЕВОГА");
            return;
        }

        var seen = 0.0;
        foreach (var cam in _cameras)
        {
            if (cam.DisabledFor > 0) continue;
            seen = Math.Max(seen, Visibility(cam, Player.Position, Player.Crouching ? new[] { 0.95, 0.5 } : new[] { 1.65, 1.1, 0.4 }));
            seen = Math.Max(seen, Visibility(cam, _viktorPos, _viktorCrouch > 0.5 ? new[] { 0.95, 0.5 } : new[] { 1.7, 1.1, 0.4 }) * 0.85);
        }

        if (seen > 0)
        {
            var rate = 0.9 * seen * (Player.Running ? 1.3 : 1.0);
            _detection = Math.Min(1, _detection + rate * dt);
            _tick -= dt;
            if (_tick <= 0) { Audio.Play("detect_tick", 0.3 + _detection * 0.5); _tick = 0.45 - _detection * 0.3; }
        }
        else
        {
            _detection = Math.Max(0, _detection - 0.3 * dt);
        }

        var level = _detection < 0.35 ? MeterLevel.Calm : _detection < 0.75 ? MeterLevel.Suspicious : MeterLevel.Danger;
        Hud.SetDetection(_detection, level, seen > 0 ? "В объективе камеры" : _detection > 0.01 ? "Камера теряет цель" : "Камеры");
        if (_detection >= 1)
            Spot();
    }

    private double Visibility(SecurityCamera cam, Point3D who, double[] heights)
    {
        var eye = cam.Eye;
        var dir = cam.Direction;
        var cos = Math.Cos(cam.HalfAngle * Math.PI / 180);
        var best = 0.0;
        foreach (var h in heights)
        {
            var target = new Point3D(who.X, who.Y + h, who.Z);
            var to = target - eye;
            var dist = to.Length;
            if (dist > cam.Range || dist < 0.1) continue;
            var c = Vector3D.DotProduct(to / dist, dir);
            if (c < cos) continue;
            if (!World.HasLineOfSight(eye, target)) continue;
            best = Math.Max(best, (1 - dist / cam.Range * 0.5) * (0.6 + 0.4 * (c - cos) / (1 - cos)));
        }
        return best;
    }

    private void Spot()
    {
        if (_spotted) return;
        _spotted = true;
        Audio.Play("radio", 0.6);
        Hud.Banner("ТРЕВОГА", "Архив засёк проникновение — защитный протокол насторожен");
        Hud.SetObjective("Быстрее!", "Бегите к терминалу досье, пока протокол не начал очистку");
        Cam.AddTrauma(0.3);
    }

    private void UpdateCameras(double dt)
    {
        foreach (var cam in _cameras)
        {
            cam.DisabledFor = Math.Max(0, cam.DisabledFor - dt);
            if (cam.DisabledFor <= 0 && !_spotted)
                cam.Yaw = cam.BaseYaw + cam.AmpA * Math.Sin(Time * cam.SpeedA) + cam.AmpB * Math.Sin(Time * cam.SpeedB + 1.3);
            else if (_spotted && cam.DisabledFor <= 0)
            {
                // тревога: камеры поворачиваются к Максу
                var to = Player.Position - cam.Mount;
                var target = Math.Atan2(to.X, to.Z) * 180 / Math.PI;
                cam.Yaw += Math.IEEERemainder(target - cam.Yaw, 360) * Math.Min(1, dt * 2);
            }
            cam.YawRot.Angle = cam.Yaw;

            var off = cam.DisabledFor > 0;
            cam.Led.Material = off ? cam.LedOff : (Time * (_spotted ? 6 : 1.2) % 1) < 0.5 ? cam.LedOn : cam.LedOff;
            if (off)
            {
                cam.ConeTransform.Matrix = new Matrix3D(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -1000, 0, 1);
            }
            else
            {
                var m = MeshBuilder.AlignY(cam.Direction);
                var eye = cam.Eye;
                m.Translate(new Vector3D(eye.X, eye.Y, eye.Z));
                cam.ConeTransform.Matrix = m;
            }
        }
        foreach (var pb in _powerBoxes)
            pb.Lamp.Material = pb.Cooldown > 0 ? Materials3D.Emissive(Color.FromRgb(255, 60, 40), 1.2) : Materials3D.Emissive(Color.FromRgb(80, 255, 120), 1.2);
    }

    // ------------------------------------------------------------------ Виктор

    private double _viktorCrouch;

    private void UpdateViktor(double dt)
    {
        // идёт по следу Макса, держась в ~1.8 м позади; пригибается вместе с ним
        var target = _viktorPos;
        var follow = 1.5;
        for (var i = _trail.Count - 1; i >= 0; i--)
        {
            if ((_trail[i] - Player.Position).Length >= follow)
            {
                target = _trail[i];
                break;
            }
        }
        // чуть левее Макса (относительно камеры), чтобы не заслонять обзор; стены не проходит
        if (target != _viktorPos)
        {
            var side = target - Cam.Right * 0.8;
            var sx = side.X; var sz = side.Z;
            World.ResolveHorizontal(ref sx, ref sz, 0.32, 0, 1.7, 0.38);
            target = new Point3D(sx, 0, sz);
        }
        if (_trail.Count > 400) _trail.RemoveRange(0, 100);

        var to = new Vector3D(target.X - _viktorPos.X, 0, target.Z - _viktorPos.Z);
        var dist = to.Length;
        var desired = dist > 0.2 ? Math.Min(Player.HorizontalSpeed + (dist > 2.5 ? 1.5 : 0.3), 6) : 0;
        _viktorSpeed += (desired - _viktorSpeed) * Math.Min(1, dt * 6);
        if (dist > 0.05)
        {
            var step = Math.Min(dist, _viktorSpeed * dt);
            _viktorPos += to / dist * step;
            var yaw = Math.Atan2(to.X, to.Z) * 180 / Math.PI;
            _viktorYaw += Math.IEEERemainder(yaw - _viktorYaw, 360) * Math.Min(1, dt * 8);
        }
        else if (CurrentPhase == Phase.Outro)
        {
            _viktorYaw += Math.IEEERemainder(0 - _viktorYaw, 360) * Math.Min(1, dt * 3);
        }
        _viktorCrouch += ((Player.Crouching ? 1 : 0) - _viktorCrouch) * Math.Min(1, dt * 5);

        // не наступать на Макса
        var d2 = Dist2D(_viktorPos, Player.Position);
        if (d2 < 0.7 && d2 > 1e-3)
        {
            var push = new Vector3D(_viktorPos.X - Player.Position.X, 0, _viktorPos.Z - Player.Position.Z);
            push.Normalize();
            _viktorPos += push * (0.7 - d2);
        }

        _viktor.Position = _viktorPos;
        _viktor.YawDegrees = _viktorYaw;
        _viktor.Update(dt, new LocomotionState
        {
            Speed = dist > 0.05 ? _viktorSpeed : 0,
            RunSpeed = 5.2,
            Grounded = true,
            Crouch = _viktorCrouch,
            Searching = _spotted && dist < 0.3
        });
    }

    // ------------------------------------------------------------------ окружение и финал

    private void UpdateAmbient(double dt)
    {
        if (_rng.NextDouble() < dt * 30)
        {
            var p = Player.Position;
            _dust.Emit(new Point3D(p.X + (_rng.NextDouble() - 0.5) * 12, 0.3 + _rng.NextDouble() * 4, p.Z + _rng.NextDouble() * 12 - 3),
                new Vector3D((_rng.NextDouble() - 0.5) * 0.15, (_rng.NextDouble() - 0.4) * 0.08, (_rng.NextDouble() - 0.5) * 0.15), 5, 0.05);
        }
        var alarm = _spotted ? 0.5 + 0.5 * Math.Sin(Time * 6) : 0;
        _alarmLight.Color = Color.FromRgb((byte)(170 * alarm), (byte)(12 * alarm), (byte)(12 * alarm));
        Hud.SetAlarm(_spotted && CurrentPhase == Phase.Play ? alarm : 0);
        Audio.SetLoop("alarm", _spotted && CurrentPhase == Phase.Play ? 0.35 : 0);
    }

    private void StartOutro()
    {
        _outro = true;
        EnterPhase(Phase.Outro);
        Hud.SetInteractPrompt(null);
        Audio.Play("objective", 0.7);
        Cam.SetCinematic(new Point3D(2.6, 1.9, 58.4), new Point3D(-0.3, 1.2, 61.6), 1.4);
    }

    private void UpdateOutro(double dt)
    {
        Hud.SetFade(Math.Clamp((PhaseTime - 2.4) / 0.8, 0, 1));
        if (PhaseTime > 3.3)
            FinishEpisode(_spotted ? "SPOTTED" : "CLEAN", "");
    }

    protected override string FootstepSound() => "step_concrete";

    protected override double CameraPivotHeight() => Player.Crouching ? 1.15 : 1.62;

    /// <summary>Для автопрогона: заметили ли и где Виктор.</summary>
    public (bool spotted, Point3D viktor) DebugState => (_spotted, _viktorPos);
}
