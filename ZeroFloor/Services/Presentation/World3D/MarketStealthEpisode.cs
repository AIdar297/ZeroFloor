using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ZeroFloor.Services;

namespace ZeroFloor.Services.Presentation.World3D;

/// <summary>
/// Глава 2А «Ярмарка теней»: проникновение через нелегальный рынок мимо патрульного дрона к
/// прилавку Гоши. Три участка: (1) торговый ряд — знакомство с управлением, дрон виден вдали;
/// (2) зона патрулирования — три пути (центральная площадь, тёмный проход за прилавками слева,
/// металлический мостик справа), укрытия реально перекрывают обзор (лучи против коллайдеров),
/// интерактивные шкафчики, брезентовые шторы, искрящий щиток-отвлечение; (3) выход к Гоше.
/// Обнаружение не убивает и не откатывает главу: тревога, преследование дроном, вызов второго
/// дрона — и всё равно путь к Гоше открыт (исход DETECTED → напряжённый вариант реплики).
/// Маршрут: больше времени в обходных зонах → выбор «обойти», иначе «напрямую».
/// </summary>
public sealed class MarketStealthEpisode : EpisodeRuntime
{
    private const double DroneHeight = 4.6;
    private const double SafeZoneZ = 65.5;
    private const double GoalZ = 76.4;

    private readonly Dictionary<Material, MeshBuilder> _batches = new();
    private readonly Dictionary<Material, MeshBuilder> _decals = new();
    private readonly List<Npc> _npcs = new();
    private readonly List<Collider> _npcBlockers = new();
    private readonly List<Interactable> _interactables = new();
    private readonly List<(GeometryModel3D model, Material on, Material off, double rate)> _flickers = new();
    private readonly Random _rng = new(2);

    private Drone _drone = null!;
    private Drone? _backupDrone;
    private ParticleSystem3D _dust = null!;
    private ParticleSystem3D _steam = null!;
    private ParticleSystem3D _sparks = null!;
    private AmbientLight _ambient = null!;
    private DirectionalLight _alarmLight = null!;
    private readonly List<Point3D> _steamVents = new();

    private double _detection;
    private bool _detected;
    private double _alarmTime;
    private double _detourTime;
    private double _directTime;
    private int _section;
    private Interactable? _hidingIn;
    private Interactable? _focused;
    private double _tickTimer;
    private Point3D _hideReturn;
    private bool _outroStarted;
    private string _route = "direct";

    public MarketStealthEpisode(Viewport3D viewport, IEpisodeHud hud, IEpisodeStoryBridge story, ISettingsStore settings)
        : base(viewport, hud, story, settings)
    {
    }

    // ================================================================== вспомогательные типы

    private enum InteractKind
    {
        Locker,
        Tarp,
        Distraction
    }

    private sealed class Interactable
    {
        public required InteractKind Kind { get; init; }
        public required Point3D Spot { get; init; }
        public required string Prompt { get; init; }
        public Collider? Blocker { get; init; }
        public Model3DGroup? Visual { get; init; }
        public ScaleTransform3D? Unroll { get; init; }
        public double Progress;
        public bool Used;
        public double Cooldown;
        public Point3D HidePos;
        public double HideYaw;
    }

    /// <summary>Патрульный дрон: маршрут с точками сканирования, проверка обнаружения,
    /// реакция на шум (отвлечение), преследование при тревоге.</summary>
    private sealed class Drone
    {
        public required DroneModel Model { get; init; }
        public required List<(Point3D pos, double scanTime)> Route { get; init; }
        public int Target;
        public double ScanLeft;
        public double ScanBase;
        public Point3D Pos;
        public Vector3D Vel;
        public double Yaw;
        public Point3D? Investigate;
        public double InvestigateLeft;
        public bool Chasing;
        public Point3D? Suspicion;
    }

    // ================================================================== уровень

    protected override void BuildLevel()
    {
        Player.WalkSpeed = 2.9;
        Player.RunSpeed = 5.4;
        Player.CrouchSpeed = 1.6;
        Player.Teleport(new Point3D(0, 0, 0.5), 0);
        Cam.Yaw = 0;
        Cam.Pitch = 0.25;
        Cam.Distance = 3.4;

        _ambient = new AmbientLight(Color.FromRgb(70, 62, 56));
        Lights.Children.Add(_ambient);
        Lights.Children.Add(new DirectionalLight(Color.FromRgb(60, 70, 88), new Vector3D(0.3, -1, 0.4)));
        _alarmLight = new DirectionalLight(Color.FromRgb(0, 0, 0), new Vector3D(-0.2, -1, -0.3));
        Lights.Children.Add(_alarmLight);
        SetupLightPool(4, Color.FromRgb(255, 176, 100), 11);

        BuildSection1();
        BuildSection2();
        BuildSection3();
        BuildCeilingDetails();

        foreach (var (material, builder) in _batches)
        {
            var m = builder.ToModel(material);
            if (m is not null) Static.Children.Add(m);
        }
        foreach (var (material, builder) in _decals)
        {
            var m = builder.ToModel(material);
            if (m is not null) Transparent.Children.Add(m);
        }

        // дрон
        var model = new DroneModel(range: 11.5, halfAngleDeg: 22) { FloorY = 0 };
        _drone = new Drone
        {
            Model = model,
            Route = new()
            {
                (new Point3D(0, DroneHeight, 30), 2.6),
                (new Point3D(5, DroneHeight, 39), 0),
                (new Point3D(0.5, DroneHeight, 47), 2.8),
                (new Point3D(-4, DroneHeight, 55), 2.4),
                (new Point3D(2.5, DroneHeight, 58), 0),
                (new Point3D(-3.5, DroneHeight, 39), 0)
            },
            Pos = new Point3D(0, DroneHeight, 30)
        };
        Dynamic.Children.Add(model.Root);
        Transparent.Children.Add(model.TransparentRoot);
        Lights.Children.Add(model.Light);

        // частицы
        _dust = AddParticles(a => Materials3D.SoftGlow(Color.FromRgb(255, 214, 160), 0.35 * a), 120, fadeIn: true);
        _steam = AddParticles(a => Materials3D.Smoke(Color.FromRgb(150, 150, 150), 0.32 * a), 140, fadeIn: true);
        _sparks = AddParticles(a => Materials3D.Emissive(Color.FromRgb(255, (byte)(150 + 90 * a), 60), 1.0), 120);

        Hud.SetControls(BaseControls(slide: false));
        Hud.SetObjective("Ярмарка теней", "Пройдите через торговый ряд");
        Hud.SetDetection(0, MeterLevel.Calm, "Дрон");
        Hud.SetPursuit(null, "");
        Hud.SetVitals(null);
        Audio.SetLoop("market_amb", 0.6, ambience: true);
    }

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

    private static readonly Color Warm = Color.FromRgb(255, 178, 96);

    private void Solid(Point3D min, Point3D max, Material mat, ColliderFlags flags = ColliderFlags.Block, double uv = 1.0)
    {
        B(mat).AddBoxMinMax(min, max, uv);
        World.Add(min, max, flags);
    }

    private void Wall(Point3D from, Point3D to, double height, Vector3D normal, Material mat)
    {
        B(mat).AddWallGrid(from, to, height, normal, 1.5, 2.0);
        var thick = 0.3;
        var min = new Point3D(Math.Min(from.X, to.X) - (normal.X != 0 ? (normal.X > 0 ? thick : 0) : 0),
            0, Math.Min(from.Z, to.Z) - (normal.Z != 0 ? (normal.Z > 0 ? thick : 0) : 0));
        var max = new Point3D(Math.Max(from.X, to.X) + (normal.X < 0 ? thick : 0), height,
            Math.Max(from.Z, to.Z) + (normal.Z < 0 ? thick : 0));
        World.Add(min, max, ColliderFlags.Wall);
    }

    private void Floor(double x0, double z0, double x1, double z1, Material mat)
    {
        B(mat).AddFloorGrid(x0, z0, x1, z1, 0, 1.0, 2.0);
        World.Add(new Point3D(x0, -0.5, z0), new Point3D(x1, 0, z1), ColliderFlags.Floor);
    }

    private void Lamp(Point3D at, bool hanging = true, double poolSize = 4.5)
    {
        if (hanging)
            B(Materials3D.Matte(Color.FromRgb(20, 20, 20))).AddTube(at + new Vector3D(0, 0.15, 0), at + new Vector3D(0, 2.2, 0), 0.01, 4, false);
        B(Materials3D.MetalPanel(Color.FromRgb(70, 64, 52), 0.4, 0.3)).AddCylinder(at + new Vector3D(0, 0.08, 0), 0.26, 0.12, 12, Matrix3D.Identity, true, 1, 0.08);
        B(Materials3D.Emissive(Color.FromRgb(255, 205, 140), 1.1)).AddSphere(at, 0.09, 8, 5);
        D(Materials3D.SoftGlow(Warm, 0.5)).AddPanel(at, new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), 0.9, 0.9);
        D(Materials3D.SoftGlow(Warm, 0.5)).AddPanel(at, new Vector3D(0, 0, 1), new Vector3D(0, 1, 0), 0.9, 0.9);
        D(Materials3D.SoftGlow(Color.FromRgb(255, 160, 80), 0.22)).AddPanel(new Point3D(at.X, 0.02, at.Z), new Vector3D(1, 0, 0), new Vector3D(0, 0, -1), poolSize, poolSize, doubleSided: false);
        AddLamp(at);
    }

    private void Neon(string text, Point3D center, Vector3D right, double width, double height, Color color, bool flicker = false)
    {
        var mat = Materials3D.NeonSign(text, color, Color.FromRgb(10, 8, 12));
        var mb = new MeshBuilder();
        mb.AddPanel(center, right, new Vector3D(0, 1, 0), width, height);
        var model = new GeometryModel3D(mb.Build(), mat) { BackMaterial = mat };
        Static.Children.Add(model);
        B(Materials3D.Matte(Color.FromRgb(25, 25, 28))).AddBox(center - Vector3D.CrossProduct(right, new Vector3D(0, 1, 0)) * 0.04, new Vector3D(
            Math.Abs(right.X) * width + 0.12 + Math.Abs(right.Z) * 0.05, height + 0.12, Math.Abs(right.Z) * width + 0.12 + Math.Abs(right.X) * 0.05));
        D(Materials3D.SoftGlow(color, 0.25)).AddPanel(center + Vector3D.CrossProduct(new Vector3D(0, 1, 0), right) * 0.05, right, new Vector3D(0, 1, 0), width * 1.5, height * 2.2);
        if (flicker)
        {
            var off = Materials3D.Matte(Color.FromRgb(30, 26, 30));
            _flickers.Add((model, mat, off, 0.4 + _rng.NextDouble()));
        }
    }

    /// <summary>Прилавок: стойка, товар, навес, лампа; facing = +1 (смотрит в +X) или -1.</summary>
    private void Stall(double xCounter, double z0, double z1, int facing, double awningY = 2.6, bool merchant = true)
    {
        var counter = Materials3D.MetalPanel(Color.FromRgb(92, 80, 64), 0.5, 0.2);
        var top = Materials3D.Wood(Color.FromRgb(96, 70, 46));
        var x0 = xCounter - 0.35;
        var x1 = xCounter + 0.35;
        Solid(new Point3D(x0, 0, z0), new Point3D(x1, 0.95, z1), counter);
        B(top).AddBoxMinMax(new Point3D(x0 - 0.05, 0.95, z0 - 0.05), new Point3D(x1 + 0.05, 1.02, z1 + 0.05));

        // товар: детали, батареи, приборы, экраны
        var goods = new[]
        {
            Materials3D.Matte(Color.FromRgb(160, 60, 50), 0.3, 30), Materials3D.Matte(Color.FromRgb(60, 110, 150), 0.3, 30),
            Materials3D.Matte(Color.FromRgb(200, 180, 120), 0.2, 20), Materials3D.MetalPanel(Color.FromRgb(120, 125, 130), 0.1, 0.6),
            Materials3D.Matte(Color.FromRgb(40, 40, 44), 0.4, 40), Materials3D.Matte(Color.FromRgb(90, 140, 70), 0.2, 20)
        };
        for (var z = z0 + 0.25; z < z1 - 0.2; z += 0.32 + _rng.NextDouble() * 0.25)
        {
            var g = goods[_rng.Next(goods.Length)];
            var w = 0.1 + _rng.NextDouble() * 0.18;
            var h = 0.06 + _rng.NextDouble() * 0.22;
            var x = xCounter + (_rng.NextDouble() - 0.5) * 0.35;
            if (_rng.NextDouble() < 0.3)
                B(g).AddCylinder(new Point3D(x, 1.02 + h / 2, z), w / 2, h, 8);
            else
                B(g).AddBoxAt(new Point3D(x, 1.02 + h / 2, z), new Vector3D(w, h, w * 1.2), yawDeg: _rng.Next(-30, 30));
            if (_rng.NextDouble() < 0.18)
                B(Materials3D.Screen(_rng.NextDouble() < 0.5 ? Color.FromRgb(60, 230, 210) : Color.FromRgb(120, 255, 120)))
                    .AddBox(new Point3D(x, 1.2, z + 0.15), new Vector3D(0.03, 0.2, 0.28));
        }

        // навес (наклонный брезент) и стойки
        var tarps = new[] { Materials3D.Fabric(Color.FromRgb(62, 74, 68)), Materials3D.Fabric(Color.FromRgb(90, 58, 46)), Materials3D.Fabric(Color.FromRgb(47, 59, 78)) };
        var tarp = tarps[_rng.Next(tarps.Length)];
        var back = xCounter - facing * 1.4;
        var front = xCounter + facing * 0.55;
        var mb = B(tarp);
        mb.AddQuad(new Point3D(back, awningY + 0.45, z0), new Point3D(back, awningY + 0.45, z1), new Point3D(front, awningY, z1), new Point3D(front, awningY, z0),
            new Vector3D(0, 1, 0), new System.Windows.Point(0, 0), new System.Windows.Point(z1 - z0, 0), new System.Windows.Point(z1 - z0, 1.5), new System.Windows.Point(0, 1.5));
        mb.AddQuad(new Point3D(front, awningY, z0), new Point3D(front, awningY, z1), new Point3D(back, awningY + 0.45, z1), new Point3D(back, awningY + 0.45, z0),
            new Vector3D(0, -1, 0), new System.Windows.Point(0, 0), new System.Windows.Point(z1 - z0, 0), new System.Windows.Point(z1 - z0, 1.5), new System.Windows.Point(0, 1.5));
        var pole = Materials3D.Matte(Color.FromRgb(50, 50, 52), 0.3, 30);
        B(pole).AddTube(new Point3D(front, 0, z0 + 0.05), new Point3D(front, awningY, z0 + 0.05), 0.03, 6);
        B(pole).AddTube(new Point3D(front, 0, z1 - 0.05), new Point3D(front, awningY, z1 - 0.05), 0.03, 6);
        World.Add(new Point3D(front - 0.05, 0, z0), new Point3D(front + 0.05, awningY, z0 + 0.1), ColliderFlags.Solid);
        World.Add(new Point3D(front - 0.05, 0, z1 - 0.1), new Point3D(front + 0.05, awningY, z1), ColliderFlags.Solid);

        Lamp(new Point3D(xCounter + facing * 0.2, awningY - 0.25, (z0 + z1) / 2), hanging: false, poolSize: 3.2);

        if (merchant)
        {
            var look = RandomLook(apron: true);
            var npc = new Npc(new HumanoidRig(look, _rng.Next(100)), new Point3D(xCounter - facing * 0.75, 0, (z0 + z1) / 2 + (_rng.NextDouble() - 0.5)),
                facing > 0 ? 90 : -90, gesture: _rng.NextDouble() < 0.6);
            AddNpc(npc);
        }
    }

    private HumanoidAppearance RandomLook(bool apron = false)
    {
        Color[] tops = { Materials3D.Hex("#5A4A3A"), Materials3D.Hex("#3D4A55"), Materials3D.Hex("#5E3B34"), Materials3D.Hex("#4A5340"), Materials3D.Hex("#2F2F35"), Materials3D.Hex("#6B5A44") };
        Color[] skins = { Materials3D.Hex("#C49A7C"), Materials3D.Hex("#A87B5C"), Materials3D.Hex("#D8B094"), Materials3D.Hex("#8C6248") };
        var r = _rng.NextDouble();
        return new HumanoidAppearance
        {
            Style = apron ? HumanoidStyle.Merchant : HumanoidStyle.Civilian,
            Skin = skins[_rng.Next(skins.Length)],
            Hair = Materials3D.Hex(_rng.NextDouble() < 0.5 ? "#2A2018" : "#4A3A2A"),
            Top = tops[_rng.Next(tops.Length)],
            Bottom = Materials3D.Hex(_rng.NextDouble() < 0.5 ? "#2C2E33" : "#3A3328"),
            Accent = tops[_rng.Next(tops.Length)],
            Apron = apron && r < 0.6,
            Hood = !apron && r < 0.35,
            Cap = r is > 0.6 and < 0.8,
            Backpack = !apron && r > 0.7,
            Height = 0.93 + _rng.NextDouble() * 0.12,
            Bulk = 0.92 + _rng.NextDouble() * 0.2
        };
    }

    private void AddNpc(Npc npc)
    {
        _npcs.Add(npc);
        _npcBlockers.Add(npc.SightBlocker);
        Dynamic.Children.Add(npc.Rig.Root);
    }

    private void Crate(double x, double z, double w, double h, double d, bool plastic = false, double yaw = 0)
    {
        var mat = plastic ? Materials3D.Matte(Color.FromRgb(48, 92, 110), 0.25, 25) : Materials3D.Wood(Color.FromRgb(122, 90, 56));
        B(mat).AddBoxAt(new Point3D(x, h / 2, z), new Vector3D(w, h, d), yawDeg: yaw);
        if (!plastic)
        {
            var frame = Materials3D.Wood(Color.FromRgb(86, 62, 38));
            B(frame).AddBoxAt(new Point3D(x, h / 2, z), new Vector3D(w + 0.02, 0.08, d + 0.02), yawDeg: yaw);
            B(frame).AddBoxAt(new Point3D(x, h - 0.04, z), new Vector3D(w + 0.02, 0.08, d + 0.02), yawDeg: yaw);
        }
        // коллайдер — описанный осевой бокс (повороты небольшие)
        var ext = Math.Abs(Math.Sin(yaw * Math.PI / 180)) * Math.Max(w, d) * 0.5;
        World.Add(new Point3D(x - w / 2 - ext, 0, z - d / 2 - ext), new Point3D(x + w / 2 + ext, h, z + d / 2 + ext), ColliderFlags.Block);
    }

    private void Container(double x0, double z0, double x1, double z1, double h, Color color)
    {
        var mat = Materials3D.MetalPanel(color, 0.45, 0.25);
        Solid(new Point3D(x0, 0, z0), new Point3D(x1, h, z1), mat, ColliderFlags.Block, 1.2);
        var rib = Materials3D.MetalPanel(Materials3D.Darken(color, 0.2), 0.5, 0.2);
        for (var z = z0 + 0.3; z < z1; z += 0.6)
        {
            B(rib).AddBox(new Point3D(x0 - 0.03, h / 2, z), new Vector3D(0.06, h - 0.2, 0.12));
            B(rib).AddBox(new Point3D(x1 + 0.03, h / 2, z), new Vector3D(0.06, h - 0.2, 0.12));
        }
    }

    private void BuildSection1()
    {
        var floor = Materials3D.Concrete(Color.FromRgb(58, 55, 52));
        var wall = Materials3D.MetalPanel(Color.FromRgb(64, 64, 62), 0.22, 0.15);
        var brick = Materials3D.Concrete(Color.FromRgb(60, 54, 50));

        Floor(-3.8, -2.5, 3.8, 24, floor);
        Wall(new Point3D(-3.6, 0, -2.5), new Point3D(-3.6, 0, 24), 6, new Vector3D(1, 0, 0), wall);
        Wall(new Point3D(3.6, 0, -2.5), new Point3D(3.6, 0, 24), 6, new Vector3D(-1, 0, 0), wall);
        Wall(new Point3D(-3.6, 0, -2.5), new Point3D(3.6, 0, -2.5), 6, new Vector3D(0, 0, 1), brick);
        B(Materials3D.Concrete(Color.FromRgb(30, 29, 28))).AddFloorGrid(-3.6, -2.5, 3.6, 24, 6, 2, 3, faceDown: true);
        World.Add(new Point3D(-3.8, 6, -2.5), new Point3D(3.8, 6.5, 24), ColliderFlags.BlocksCamera);

        // прилавки по обе стороны торгового ряда
        foreach (var (z0, z1) in new[] { (1.0, 5.5), (7.5, 12.5), (14.5, 19.5) })
        {
            Stall(-2.25, z0, z1, facing: 1);
            Stall(2.25, z0 + 0.8, z1 + 0.8, facing: -1);
        }

        // кондиционеры и короба на стенах
        var ac = Materials3D.MetalPanel(Color.FromRgb(150, 150, 140), 0.35, 0.3);
        foreach (var z in new[] { 3.0, 10.5, 17.0 })
        {
            B(ac).AddBox(new Point3D(-3.3, 3.9, z), new Vector3D(0.6, 0.7, 1.1));
            B(Materials3D.Grating(Color.FromRgb(60, 60, 58))).AddBox(new Point3D(-2.99, 3.9, z), new Vector3D(0.02, 0.55, 0.9));
            B(ac).AddBox(new Point3D(3.3, 4.4, z + 3), new Vector3D(0.6, 0.6, 0.9));
        }

        Neon("ЭЛЕКТРОНИКА", new Point3D(-3.5, 3.3, 7), new Vector3D(0, 0, -1), 2.6, 0.7, Color.FromRgb(60, 230, 230));
        Neon("ЗАПЧАСТИ", new Point3D(3.5, 3.4, 12), new Vector3D(0, 0, 1), 2.2, 0.65, Color.FromRgb(255, 70, 200), flicker: true);
        Neon("АПТЕКА 24", new Point3D(-3.5, 3.4, 18.5), new Vector3D(0, 0, -1), 2.2, 0.65, Color.FromRgb(90, 255, 120));
        Neon("БАТАРЕИ · ЧИПЫ", new Point3D(3.5, 3.6, 4.5), new Vector3D(0, 0, 1), 2.4, 0.55, Color.FromRgb(255, 190, 60));

        foreach (var z in new[] { 4.0, 11.5, 19.0 })
            Lamp(new Point3D(0, 3.4, z));

        _steamVents.Add(new Point3D(-3.0, 0.2, 9.5));
        B(Materials3D.Grating(Color.FromRgb(70, 70, 66))).AddBox(new Point3D(-3.0, 0.02, 9.5), new Vector3D(0.6, 0.04, 0.6));

        // прохожие торгового ряда
        AddNpc(new Npc(new HumanoidRig(RandomLook(), 11), new Point3D(0.6, 0, 20), 180,
            new[] { new Point3D(0.6, 0, 20), new Point3D(0.8, 0, 2), new Point3D(-0.6, 0, 2), new Point3D(-0.7, 0, 20) }, 1.1));
        AddNpc(new Npc(new HumanoidRig(RandomLook(), 12), new Point3D(-0.8, 0, 9), 0,
            new[] { new Point3D(-0.8, 0, 9), new Point3D(-0.5, 0, 22), new Point3D(0.9, 0, 22), new Point3D(0.7, 0, 6) }, 1.25));
        AddNpc(new Npc(new HumanoidRig(RandomLook(), 13), new Point3D(1.2, 0, 15), 0, null, gesture: true));
    }

    private void BuildSection2()
    {
        var floor = Materials3D.Concrete(Color.FromRgb(54, 52, 50));
        var wall = Materials3D.MetalPanel(Color.FromRgb(58, 60, 60), 0.25, 0.15);
        var steel = Materials3D.DiamondPlate(Color.FromRgb(96, 98, 100));
        var dark = Materials3D.MetalPanel(Color.FromRgb(44, 46, 48), 0.3, 0.3);

        Floor(-12.2, 24, 12.2, 64, floor);
        Wall(new Point3D(-12, 0, 24), new Point3D(-12, 0, 64), 8, new Vector3D(1, 0, 0), wall);
        Wall(new Point3D(12, 0, 24), new Point3D(12, 0, 64), 8, new Vector3D(-1, 0, 0), wall);
        Wall(new Point3D(-12, 0, 24), new Point3D(-3.6, 0, 24), 8, new Vector3D(0, 0, 1), wall);
        Wall(new Point3D(3.6, 0, 24), new Point3D(12, 0, 24), 8, new Vector3D(0, 0, 1), wall);
        Wall(new Point3D(-12, 0, 64), new Point3D(-3.2, 0, 64), 8, new Vector3D(0, 0, -1), wall);
        Wall(new Point3D(3.2, 0, 64), new Point3D(12, 0, 64), 8, new Vector3D(0, 0, -1), wall);
        B(Materials3D.Concrete(Color.FromRgb(26, 26, 27))).AddFloorGrid(-12, 24, 12, 64, 8, 3, 4, faceDown: true);
        World.Add(new Point3D(-12, 8, 24), new Point3D(12, 8.5, 64), ColliderFlags.BlocksCamera);
        // перемычка над входом в зал
        Solid(new Point3D(-3.6, 4.2, 23.7), new Point3D(3.6, 8, 24.3), wall, ColliderFlags.Wall);
        Solid(new Point3D(-3.2, 4.0, 63.7), new Point3D(3.2, 8, 64.3), wall, ColliderFlags.Wall);

        // ---- левый ряд прилавков, отделяющий тёмный обходной проход
        var panel = Materials3D.MetalPanel(Color.FromRgb(72, 64, 56), 0.35, 0.1);
        foreach (var (z0, z1) in new[] { (27.0, 36.0), (38.5, 48.0), (50.5, 58.0) })
        {
            Solid(new Point3D(-8.6, 0, z0), new Point3D(-8.35, 2.7, z1), panel, ColliderFlags.Wall);
            Stall(-7.4, z0 + 0.2, z1 - 0.2, facing: 1, awningY: 2.75, merchant: z0 < 50);
        }
        Neon("ПРОДАМ · КУПЛЮ", new Point3D(-8.3, 3.25, 32), new Vector3D(0, 0, -1), 3.2, 0.6, Color.FromRgb(255, 90, 160));
        Neon("РЕМОНТ ЭКРАНОВ", new Point3D(-8.3, 3.25, 44), new Vector3D(0, 0, -1), 3.0, 0.6, Color.FromRgb(80, 200, 255), flicker: true);

        // обходной проход: ящики и хлам, полумрак
        Crate(-11.2, 31, 1.0, 1.1, 1.0);
        Crate(-10.0, 35.5, 0.9, 0.9, 0.9, plastic: true, yaw: 12);
        Crate(-11.3, 42, 1.0, 1.4, 1.2);
        Crate(-9.4, 46.5, 0.8, 0.8, 0.8, yaw: -8);
        Crate(-11.0, 52, 1.1, 1.0, 1.0, plastic: true);
        Crate(-10.2, 56.5, 1.0, 1.2, 1.0);
        Lamp(new Point3D(-10.2, 3.4, 39), poolSize: 3.5);
        Lamp(new Point3D(-10.2, 3.4, 51), poolSize: 3.5);
        _steamVents.Add(new Point3D(-11.6, 0.3, 48));
        B(Materials3D.MetalPanel(Color.FromRgb(70, 72, 74), 0.4, 0.3)).AddTube(new Point3D(-11.7, 0.3, 26), new Point3D(-11.7, 0.3, 62), 0.18, 10);

        // ---- центральная площадь: контейнеры, ящики, тележки
        Container(1.2, 31, 3.8, 37.2, 2.6, Color.FromRgb(62, 92, 96));
        Container(-5.2, 53, -2.6, 58.6, 2.6, Color.FromRgb(122, 58, 44));
        Crate(-3.2, 30.2, 1.2, 1.15, 1.0);
        Crate(-3.2, 31.3, 0.9, 0.7, 0.9, plastic: true, yaw: 15);
        Crate(-1.0, 41.5, 1.3, 1.2, 1.1);
        Crate(3.6, 44.5, 1.1, 1.1, 1.1, yaw: 10);
        Crate(4.4, 45.5, 0.8, 0.75, 0.8, plastic: true);
        Crate(-4.0, 47.4, 1.2, 1.25, 1.0, yaw: -6);
        Crate(0.6, 51.5, 1.0, 1.0, 1.0, plastic: true);
        Crate(4.2, 57.0, 1.3, 1.2, 1.1);
        Crate(-1.6, 60.6, 1.2, 1.1, 1.0, yaw: 20);

        Lamp(new Point3D(-4, 5.2, 31));
        Lamp(new Point3D(4, 5.2, 41));
        Lamp(new Point3D(-3, 5.2, 50));
        Lamp(new Point3D(3, 5.2, 60));
        Neon("ЯРМАРКА", new Point3D(0, 6.2, 63.6), new Vector3D(-1, 0, 0), 4.2, 1.1, Color.FromRgb(255, 170, 60));

        // ---- правая сторона: лестница → мостик → лестница вниз
        for (var i = 0; i < 12; i++)
        {
            var z = 28 + i * 0.5;
            var top = 0.25 * (i + 1);
            Solid(new Point3D(8.2, 0, z), new Point3D(10.4, top, z + 0.5), steel, ColliderFlags.Block);
        }
        Solid(new Point3D(8.2, 2.85, 34), new Point3D(10.6, 3.0, 58.5), steel, ColliderFlags.Floor | ColliderFlags.BlocksSight);
        // парапет-экран со стороны площади
        Solid(new Point3D(7.95, 3.0, 34), new Point3D(8.2, 4.05, 58.5), Materials3D.MetalPanel(Color.FromRgb(88, 92, 96), 0.4, 0.3), ColliderFlags.Wall);
        Solid(new Point3D(7.95, 0, 28), new Point3D(8.2, 1.0, 34), dark, ColliderFlags.Solid);
        // опоры и машинерия под мостиком
        for (var z = 35.0; z < 58; z += 5.5)
            B(dark).AddBoxMinMax(new Point3D(8.25, 0, z), new Point3D(8.5, 2.85, z + 0.25));
        Solid(new Point3D(8.6, 0, 35), new Point3D(11.9, 2.4, 57.5), Materials3D.MetalPanel(Color.FromRgb(70, 76, 70), 0.35, 0.3), ColliderFlags.Block);
        for (var z = 37.0; z < 56; z += 3.6)
            B(Materials3D.Emissive(Color.FromRgb(90, 255, 120), 0.7)).AddBox(new Point3D(8.58, 1.6, z), new Vector3D(0.02, 0.06, 0.2));

        // лестница вниз вдоль -X
        for (var i = 0; i < 12; i++)
        {
            var x1 = 8.2 - i * 0.5;
            var top = 3.0 - 0.25 * (i + 1);
            if (top <= 0.01) break;
            Solid(new Point3D(x1 - 0.5, 0, 58.5), new Point3D(x1, top, 60.6), steel, ColliderFlags.Block);
        }
        Solid(new Point3D(2.7, 0, 60.6), new Point3D(10.6, 1.0, 60.85), dark, ColliderFlags.Solid); // перила
        Solid(new Point3D(10.4, 0, 58.5), new Point3D(10.6, 4.0, 60.6), dark, ColliderFlags.Wall);
        Lamp(new Point3D(9.4, 5.3, 46), poolSize: 3);

        // ---- интерактивные укрытия
        AddLocker(new Point3D(-6.0, 0, 43.4), yawDeg: 90);
        AddLocker(new Point3D(6.5, 0, 48.0), yawDeg: -90);
        AddTarp(-6.6, -2.4, 40.2, new Point3D(-2.1, 0, 39.6));
        AddTarp(2.1, 6.6, 52.6, new Point3D(1.8, 0, 52.0));
        AddDistraction(new Point3D(-6.5, 1.35, 30.6));

        // ---- прохожие на площади (живые «щиты»)
        AddNpc(new Npc(new HumanoidRig(RandomLook(), 21), new Point3D(-1, 0, 27), 0,
            new[] { new Point3D(-1, 0, 27), new Point3D(-5.6, 0, 37), new Point3D(-5.4, 0, 50), new Point3D(-1.5, 0, 62), new Point3D(1.5, 0, 50), new Point3D(0.6, 0, 38) }, 1.15));
        AddNpc(new Npc(new HumanoidRig(RandomLook(), 22), new Point3D(5.5, 0, 40), 0,
            new[] { new Point3D(5.5, 0, 40), new Point3D(6.3, 0, 58), new Point3D(-0.5, 0, 63), new Point3D(-2, 0, 45), new Point3D(1, 0, 27.5) }, 1.0));
        AddNpc(new Npc(new HumanoidRig(RandomLook(), 23), new Point3D(-10.5, 0, 28), 0,
            new[] { new Point3D(-10.5, 0, 28), new Point3D(-9.8, 0, 60), new Point3D(-10.6, 0, 60), new Point3D(-10.8, 0, 28.5) }, 0.9));
    }

    private void BuildSection3()
    {
        var floor = Materials3D.Concrete(Color.FromRgb(50, 48, 46));
        var wall = Materials3D.MetalPanel(Color.FromRgb(66, 58, 52), 0.3, 0.15);
        Floor(-4.2, 64, 4.2, 85, floor);
        Wall(new Point3D(-4, 0, 64), new Point3D(-4, 0, 85), 4.5, new Vector3D(1, 0, 0), wall);
        Wall(new Point3D(4, 0, 64), new Point3D(4, 0, 85), 4.5, new Vector3D(-1, 0, 0), wall);
        Wall(new Point3D(-4, 0, 85), new Point3D(4, 0, 85), 4.5, new Vector3D(0, 0, -1), wall);
        Solid(new Point3D(-3.2, 0, 63.8), new Point3D(-4, 4.5, 64.3), wall, ColliderFlags.Wall);
        Solid(new Point3D(3.2, 0, 63.8), new Point3D(4, 4.5, 64.3), wall, ColliderFlags.Wall);
        B(Materials3D.Concrete(Color.FromRgb(28, 27, 26))).AddFloorGrid(-4, 64, 4, 85, 4.5, 2, 3, faceDown: true);
        World.Add(new Point3D(-4.2, 4.5, 64), new Point3D(4.2, 5, 85), ColliderFlags.BlocksCamera);

        // свисающие полосы брезента — граница, за которую дрон не залетает
        var strip = Materials3D.Fabric(Color.FromRgb(70, 64, 56));
        for (var x = -3.0; x < 3.2; x += 0.55)
            B(strip).AddPanel(new Point3D(x, 3.4, 66), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), 0.45, 2.1 - Math.Abs(Math.Sin(x * 3)) * 0.6);

        Stall(-3.0, 68, 73, facing: 1, awningY: 2.5);
        Crate(3.2, 69.5, 0.9, 1.0, 1.4);
        Crate(3.1, 72.2, 0.8, 0.7, 0.8, plastic: true);

        // --- прилавок Гоши «Ремонт и обмен»
        var counter = Materials3D.MetalPanel(Color.FromRgb(104, 84, 60), 0.45, 0.25);
        Solid(new Point3D(-2.4, 0, 79), new Point3D(2.4, 1.05, 79.9), counter);
        B(Materials3D.Wood(Color.FromRgb(110, 80, 50))).AddBoxMinMax(new Point3D(-2.5, 1.05, 78.9), new Point3D(2.5, 1.12, 80.0));
        // витрина: платы, чипы, разобранные панели
        var board = Materials3D.Matte(Color.FromRgb(30, 90, 50), 0.4, 30);
        for (var i = 0; i < 9; i++)
            B(board).AddBoxAt(new Point3D(-2.0 + i * 0.5, 1.15, 79.4 + (i % 2) * 0.15), new Vector3D(0.3, 0.02, 0.2), yawDeg: i * 13);
        B(Materials3D.Screen(Color.FromRgb(255, 200, 80))).AddBox(new Point3D(1.6, 1.4, 79.5), new Vector3D(0.5, 0.35, 0.05));
        B(Materials3D.Screen(Color.FromRgb(60, 230, 210))).AddBoxAt(new Point3D(-1.5, 1.35, 79.5), new Vector3D(0.4, 0.3, 0.05), yawDeg: 15);
        // полки с электроникой
        var shelf = Materials3D.MetalPanel(Color.FromRgb(60, 60, 62), 0.4, 0.3);
        for (var y = 0.6; y < 3.2; y += 0.65)
        {
            B(shelf).AddBoxMinMax(new Point3D(-3.6, y, 83.6), new Point3D(3.6, y + 0.05, 84.6));
            for (var x = -3.3; x < 3.4; x += 0.45 + _rng.NextDouble() * 0.3)
                B(_rng.NextDouble() < 0.5 ? Materials3D.Matte(Color.FromRgb(40, 42, 46), 0.4, 30) : Materials3D.MetalPanel(Color.FromRgb(130, 120, 100), 0.2, 0.4))
                    .AddBox(new Point3D(x, y + 0.2, 84.1), new Vector3D(0.3, 0.3 + _rng.NextDouble() * 0.15, 0.4));
        }
        World.Add(new Point3D(-3.8, 0, 83.5), new Point3D(3.8, 3.2, 85), ColliderFlags.Block);
        // криво висящая вывеска
        var sign = Materials3D.NeonSign("РЕМОНТ И ОБМЕН", Color.FromRgb(255, 140, 40), Color.FromRgb(14, 10, 8), 70, 640, 150);
        var smb = new MeshBuilder();
        var roll = 5.0 * Math.PI / 180;
        smb.AddPanel(new Point3D(0, 3.3, 79.2), new Vector3D(-Math.Cos(roll), Math.Sin(roll), 0), new Vector3D(Math.Sin(roll), Math.Cos(roll), 0), 3.8, 0.9);
        Static.Children.Add(new GeometryModel3D(smb.Build(), sign) { BackMaterial = sign });
        D(Materials3D.SoftGlow(Color.FromRgb(255, 140, 40), 0.3)).AddPanel(new Point3D(0, 3.3, 79.1), new Vector3D(-1, 0, 0), new Vector3D(0, 1, 0), 5.4, 2.0);
        Lamp(new Point3D(0, 3.6, 70.5), poolSize: 3.5);
        Lamp(new Point3D(0, 2.9, 79.5), hanging: false, poolSize: 4);

        // Гоша за прилавком
        var gosha = new Npc(new HumanoidRig(HumanoidAppearance.GoshaKovalchuk(), 77), new Point3D(0.2, 0, 81.0), 180, gesture: true);
        AddNpc(gosha);
    }

    private void BuildCeilingDetails()
    {
        var pipe = Materials3D.MetalPanel(Color.FromRgb(84, 88, 90), 0.5, 0.4);
        var rust = Materials3D.MetalPanel(Color.FromRgb(96, 70, 50), 0.8, 0.2);
        var cable = Materials3D.Matte(Color.FromRgb(18, 18, 20), 0.3, 20);
        var duct = Materials3D.MetalPanel(Color.FromRgb(118, 120, 118), 0.3, 0.35);

        B(pipe).AddTube(new Point3D(-3.0, 5.2, -2.5), new Point3D(-3.0, 5.2, 24), 0.22, 10);
        B(rust).AddTube(new Point3D(-2.4, 5.5, -2.5), new Point3D(-2.4, 5.5, 24), 0.12, 8);
        B(pipe).AddTube(new Point3D(2.8, 5.0, -2.5), new Point3D(2.8, 5.0, 24), 0.16, 8);
        B(duct).AddBoxMinMax(new Point3D(-1.0, 5.4, -2.5), new Point3D(0.6, 5.95, 24));
        for (var z = 0.0; z < 24; z += 3.1)
        {
            B(cable).AddSaggingCable(new Point3D(-3.5, 4.6, z), new Point3D(3.5, 4.4, z + 1.2), 0.6, 0.02);
            B(cable).AddSaggingCable(new Point3D(-3.5, 4.9, z + 1.5), new Point3D(3.5, 5.1, z + 0.6), 0.45, 0.015);
        }

        for (var x = -10.0; x <= 10; x += 4)
            B(pipe).AddTube(new Point3D(x, 7.2, 24), new Point3D(x, 7.2, 64), 0.25, 10);
        B(duct).AddBoxMinMax(new Point3D(-12, 6.2, 44), new Point3D(12, 6.9, 45.2));
        B(rust).AddTube(new Point3D(-12, 6.6, 33), new Point3D(12, 6.6, 33), 0.18, 8);
        for (var z = 26.0; z < 64; z += 4.2)
            B(cable).AddSaggingCable(new Point3D(-12, 7.0, z), new Point3D(12, 6.8, z + 2), 1.1, 0.025);
        for (var z = 65.0; z < 84; z += 2.6)
            B(cable).AddSaggingCable(new Point3D(-4, 4.2, z), new Point3D(4, 4.0, z + 0.8), 0.4, 0.015);
        B(pipe).AddTube(new Point3D(-3.4, 4.0, 64), new Point3D(-3.4, 4.0, 85), 0.15, 8);
    }

    // ------------------------------------------------------------------ интерактивные объекты

    private void AddLocker(Point3D at, double yawDeg)
    {
        var body = Materials3D.MetalPanel(Color.FromRgb(70, 92, 84), 0.55, 0.3);
        var group = new Model3DGroup();
        var mb = new MeshBuilder();
        var tr = MeshBuilder.Trs(new Vector3D(at.X, 0, at.Z), yawDeg: yawDeg);
        mb.AddBox(new Point3D(0, 1.05, 0), new Vector3D(0.9, 2.1, 0.7), tr);
        group.Children.Add(mb.ToModel(body)!);
        var slits = new MeshBuilder();
        for (var i = 0; i < 5; i++)
            slits.AddBox(new Point3D(0, 1.6 + i * 0.07, 0.355), new Vector3D(0.5, 0.025, 0.01), tr);
        group.Children.Add(slits.ToModel(Materials3D.Matte(Color.FromRgb(10, 10, 10)))!);
        Static.Children.Add(group);
        World.Add(new Point3D(at.X - 0.45, 0, at.Z - 0.45), new Point3D(at.X + 0.45, 2.1, at.Z + 0.45), ColliderFlags.Block);

        var yaw = yawDeg * Math.PI / 180;
        var front = new Vector3D(Math.Sin(yaw), 0, Math.Cos(yaw));
        _interactables.Add(new Interactable
        {
            Kind = InteractKind.Locker,
            Spot = at + front * 0.85,
            Prompt = "Спрятаться в шкафчике",
            HidePos = at,
            HideYaw = yaw
        });
    }

    private void AddTarp(double x0, double x1, double z, Point3D ropePost)
    {
        var bar = Materials3D.Matte(Color.FromRgb(50, 50, 52), 0.4, 30);
        B(bar).AddTube(new Point3D(x0, 3.1, z), new Point3D(x1, 3.1, z), 0.04, 6);
        B(bar).AddTube(new Point3D(x0, 0, z), new Point3D(x0, 3.1, z), 0.05, 6);
        B(bar).AddTube(new Point3D(x1, 0, z), new Point3D(x1, 3.1, z), 0.05, 6);
        World.Add(new Point3D(x0 - 0.06, 0, z - 0.06), new Point3D(x0 + 0.06, 3.1, z + 0.06), ColliderFlags.Solid);
        World.Add(new Point3D(x1 - 0.06, 0, z - 0.06), new Point3D(x1 + 0.06, 3.1, z + 0.06), ColliderFlags.Solid);
        B(Materials3D.Emissive(Color.FromRgb(255, 200, 60), 0.8)).AddBox(ropePost + new Vector3D(0, 1.2, 0), new Vector3D(0.08, 0.08, 0.08));
        B(bar).AddTube(ropePost, ropePost + new Vector3D(0, 1.3, 0), 0.04, 6);

        // скатанный брезент + разворачиваемое полотно (масштаб по Y от планки вниз)
        var cloth = Materials3D.Fabric(Color.FromRgb(86, 70, 46), 0.18);
        B(cloth).AddTube(new Point3D(x0, 3.0, z), new Point3D(x1, 3.0, z), 0.12, 8);
        var sheet = new MeshBuilder();
        sheet.AddPanel(new Point3D((x0 + x1) / 2, -1.45, 0), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), x1 - x0, 2.9);
        var scale = new ScaleTransform3D(1, 0.02, 1);
        var tg = new Transform3DGroup();
        tg.Children.Add(scale);
        tg.Children.Add(new TranslateTransform3D(0, 3.0, z));
        var model = new GeometryModel3D(sheet.Build(), cloth) { BackMaterial = cloth, Transform = tg };
        var visual = new Model3DGroup();
        visual.Children.Add(model);
        Dynamic.Children.Add(visual);

        var blocker = World.Add(new Point3D(x0, 0.1, z - 0.05), new Point3D(x1, 3.0, z + 0.05), ColliderFlags.BlocksSight, "tarp");
        blocker.Enabled = false;
        _interactables.Add(new Interactable
        {
            Kind = InteractKind.Tarp,
            Spot = ropePost,
            Prompt = "Опустить брезент — закрыть обзор",
            Blocker = blocker,
            Visual = visual,
            Unroll = scale
        });
    }

    private void AddDistraction(Point3D at)
    {
        B(Materials3D.MetalPanel(Color.FromRgb(90, 96, 92), 0.6, 0.3)).AddBox(at, new Vector3D(0.2, 0.7, 0.55));
        B(Materials3D.Hazard()).AddBox(at + new Vector3D(0.11, 0.25, 0), new Vector3D(0.01, 0.12, 0.5));
        B(Materials3D.Emissive(Color.FromRgb(255, 60, 40), 0.9)).AddBox(at + new Vector3D(0.11, -0.15, 0.15), new Vector3D(0.01, 0.05, 0.05));
        _interactables.Add(new Interactable
        {
            Kind = InteractKind.Distraction,
            Spot = new Point3D(at.X + 0.7, 0, at.Z),
            Prompt = "Закоротить щиток — отвлечь дрон"
        });
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

        UpdateDrone(_drone, dt);
        if (_backupDrone is not null) UpdateDrone(_backupDrone, dt);
        foreach (var npc in _npcs)
        {
            npc.Update(dt, Player.Position);
            if (_hidingIn is null) npc.PushPlayer(Player);
        }
        UpdateInteractableVisuals(dt);
        UpdateAmbientEffects(dt);
    }

    private void UpdateIntro(double dt)
    {
        // облёт: сверху над рядом к дрону вдали, затем — за плечо Макса
        if (PhaseTime < 0.01)
        {
            Cam.SnapCinematic(new Point3D(2.2, 4.8, -1.5), new Point3D(0, 3.4, 28));
            Hud.Banner("УЧАСТОК 1 · ТОРГОВЫЙ РЯД", "Ярмарка теней — граница Низов и Средних этажей");
        }
        Hud.SetFade(Math.Max(0, 1 - PhaseTime / 0.9));
        Player.Update(dt, Input, Cam.Forward, Cam.Right, allowInput: false);
        if (PhaseTime > 2.2)
            Cam.ReleaseCinematic(2.2);
        if (PhaseTime > 3.0)
        {
            EnterPhase(Phase.Play);
            Hud.SetObjective("Ярмарка теней", "Пройдите через торговый ряд. Впереди — патрульный дрон: присмотритесь к его маршруту");
        }
    }

    private void UpdatePlay(double dt)
    {
        if (_hidingIn is not null)
        {
            Player.Update(dt, Input, Cam.Forward, Cam.Right, allowInput: false);
            Player.Position = _hidingIn.HidePos;
            Hud.SetInteractPrompt("E — выйти из шкафчика");
            if (Input.InteractPressed)
                ExitHiding();
        }
        else
        {
            Player.Update(dt, Input, Cam.Forward, Cam.Right);
            UpdateFocus();
            if (_focused is not null && Input.InteractPressed)
                Interact(_focused);
        }

        TrackSections(dt);
        UpdateDetection(dt);

        if (Player.Position.Z > GoalZ && Math.Abs(Player.Position.X) < 2.8 && _hidingIn is null)
            StartOutro();
    }

    private void TrackSections(double dt)
    {
        var p = Player.Position;
        var section = p.Z < 24 ? 1 : p.Z < 64 ? 2 : 3;
        if (section != _section)
        {
            if (section > _section)
            {
                Audio.Play("objective", 0.6);
                if (section == 2)
                {
                    Hud.Banner("УЧАСТОК 2 · ЗОНА ПАТРУЛИРОВАНИЯ", "Не попадайте в красный конус сканера");
                    Hud.SetObjective("Мимо дрона", "Пробирайтесь от укрытия к укрытию. Обход: тёмный проход слева или мостик справа");
                }
                else if (section == 3)
                {
                    Hud.Banner("УЧАСТОК 3 · «РЕМОНТ И ОБМЕН»", "Прилавок Гоши Ковальчука");
                    Hud.SetObjective("К Гоше", "Подойдите к прилавку под вывеской «Ремонт и обмен»");
                }
            }
            _section = section;
        }

        if (section == 2)
        {
            var detour = p.X < -8.2 || p.Y > 1.5 || (p.X > 7.5 && p.Z > 27 && p.Z < 61);
            if (detour) _detourTime += dt;
            else if (Math.Abs(p.X) < 7) _directTime += dt;
        }
    }

    private void UpdateFocus()
    {
        _focused = null;
        var p = Player.Position;
        var best = 1.4;
        foreach (var it in _interactables)
        {
            if (it.Kind == InteractKind.Tarp && it.Used) continue;
            if (it.Kind == InteractKind.Distraction && it.Cooldown > 0) continue;
            var d = Dist2D(p, it.Spot);
            if (d < best)
            {
                best = d;
                _focused = it;
            }
        }
        Hud.SetInteractPrompt(_focused is null ? null : "E — " + _focused.Prompt);
    }

    private void Interact(Interactable it)
    {
        switch (it.Kind)
        {
            case InteractKind.Locker:
                _hidingIn = it;
                _hideReturn = it.Spot;
                Audio.Play("clank", 0.5);
                Max.Visible = false;
                Cam.SetCinematic(it.HidePos + new Vector3D(Math.Sin(it.HideYaw) * 2.6, 1.7, Math.Cos(it.HideYaw) * 2.6) ,
                    it.HidePos + new Vector3D(0, 1.3, 0) + new Vector3D(Math.Sin(it.HideYaw), 0, Math.Cos(it.HideYaw)) * 6, 4);
                Hud.Toast("Макс затаился в шкафчике — дрон его не видит");
                break;
            case InteractKind.Tarp:
                it.Used = true;
                it.Blocker!.Enabled = true;
                Audio.Play("tarp", 0.8);
                Hud.Toast("Брезент опущен — теперь он закрывает обзор дрону");
                break;
            case InteractKind.Distraction:
                it.Cooldown = 14;
                Audio.Play("spark", 1.0);
                Audio.Play("spark", 0.7);
                var at = it.Spot + new Vector3D(-0.6, 1.35, 0);
                for (var i = 0; i < 40; i++)
                    _sparks.Emit(at, new Vector3D((_rng.NextDouble() - 0.2) * 4, _rng.NextDouble() * 4, (_rng.NextDouble() - 0.5) * 4), 0.5 + _rng.NextDouble() * 0.5, 0.05, 0, 9, 0.5, 0.03);
                foreach (var d in Drones())
                {
                    if (d.Chasing) continue;
                    d.Investigate = new Point3D(at.X + 2.5, DroneHeight - 0.4, at.Z);
                    d.InvestigateLeft = 6;
                }
                Hud.Toast("Искры и треск — дрон отвлёкся на шум");
                break;
        }
    }

    private void ExitHiding()
    {
        if (_hidingIn is null) return;
        Player.Teleport(_hideReturn, _hidingIn.HideYaw);
        Max.Visible = true;
        _hidingIn = null;
        Cam.ReleaseCinematic(4);
        Audio.Play("clank", 0.4);
    }

    private IEnumerable<Drone> Drones()
    {
        yield return _drone;
        if (_backupDrone is not null) yield return _backupDrone;
    }

    // ------------------------------------------------------------------ дрон

    private void UpdateDrone(Drone d, double dt)
    {
        var playerInSafeZone = Player.Position.Z > SafeZoneZ;
        Point3D target;
        double speed;
        var model = d.Model;

        if (d.Chasing && CurrentPhase == Phase.Play)
        {
            var p = Player.Position;
            target = new Point3D(p.X, DroneHeight - 0.6, Math.Min(p.Z - 2.5, SafeZoneZ - 2));
            speed = playerInSafeZone ? 1.5 : 4.6;
            var to = new Point3D(p.X, p.Y + 1, p.Z) - d.Pos;
            var yawToPlayer = Math.Atan2(to.X, to.Z) * 180 / Math.PI;
            d.Yaw += Math.IEEERemainder(yawToPlayer - d.Yaw, 360) * Math.Min(1, dt * 5);
            model.SensorYawDeg = 0;
            var horiz = Math.Sqrt(to.X * to.X + to.Z * to.Z);
            model.SensorPitchDeg = Math.Clamp(Math.Atan2(-to.Y, horiz) * 180 / Math.PI, 10, 80);
        }
        else if (d.Investigate is { } inv && d.InvestigateLeft > 0)
        {
            d.InvestigateLeft -= dt;
            target = inv;
            speed = 3.4;
            FaceTowards(d, inv + new Vector3D(-3, -3, 0), dt);
            model.SensorPitchDeg = Approach(model.SensorPitchDeg, 55, dt * 40);
            model.SensorYawDeg = Math.Sin(Time * 1.7) * 30;
            if (d.InvestigateLeft <= 0) d.Investigate = null;
        }
        else if (d.Suspicion is { } sus)
        {
            // подозрение: замирает и разворачивает сенсор к месту, где мелькнул Макс
            target = d.Pos;
            speed = 0;
            var to = sus - d.Pos;
            var yawTo = Math.Atan2(to.X, to.Z) * 180 / Math.PI;
            d.Yaw += Math.IEEERemainder(yawTo - d.Yaw, 360) * Math.Min(1, dt * 2.5);
            model.SensorYawDeg = Approach(model.SensorYawDeg, 0, dt * 60);
            var horiz = Math.Sqrt(to.X * to.X + to.Z * to.Z);
            model.SensorPitchDeg = Approach(model.SensorPitchDeg, Math.Clamp(Math.Atan2(-to.Y, horiz) * 180 / Math.PI, 15, 80), dt * 40);
        }
        else
        {
            var (wp, scan) = d.Route[d.Target];
            target = wp;
            speed = 2.4;
            if (d.ScanLeft > 0)
            {
                d.ScanLeft -= dt;
                speed = 0;
                target = d.Pos;
                // сканирование: веерный разворот сенсора
                model.SensorYawDeg = Math.Sin((d.ScanBase - d.ScanLeft) * 1.6) * 55;
                model.SensorPitchDeg = Approach(model.SensorPitchDeg, 48, dt * 30);
                if (d.ScanLeft <= 0)
                    d.Target = (d.Target + 1) % d.Route.Count;
            }
            else if ((wp - d.Pos).Length < 0.25)
            {
                if (scan > 0)
                {
                    d.ScanLeft = d.ScanBase = scan;
                    Audio.Play("drone_scan", VolumeAt(d.Pos, 0.6));
                }
                else
                {
                    d.Target = (d.Target + 1) % d.Route.Count;
                }
            }
            else
            {
                FaceTowards(d, wp, dt);
                model.SensorYawDeg = Approach(model.SensorYawDeg, Math.Sin(Time * 0.9) * 18, dt * 40);
                model.SensorPitchDeg = Approach(model.SensorPitchDeg, 40, dt * 30);
            }
        }

        var delta = target - d.Pos;
        var dist = delta.Length;
        var desiredVel = dist > 0.05 ? delta / dist * Math.Min(speed, dist * 2.5) : new Vector3D();
        d.Vel += (desiredVel - d.Vel) * Math.Min(1, dt * 3);
        d.Pos += d.Vel * dt;

        model.Position = d.Pos;
        model.YawDeg = d.Yaw;
        model.Alarm = _detected;
        model.Update(dt, d.Vel);
    }

    private static void FaceTowards(Drone d, Point3D target, double dt)
    {
        var to = target - d.Pos;
        if (to.X * to.X + to.Z * to.Z < 0.01) return;
        var yaw = Math.Atan2(to.X, to.Z) * 180 / Math.PI;
        d.Yaw += Math.IEEERemainder(yaw - d.Yaw, 360) * Math.Min(1, dt * 2.2);
    }

    /// <summary>Насколько хорошо дрон видит Макса: 0 — не видит; иначе 0..1 (близость к центру
    /// конуса и дистанция). Проверяются голова, корпус и ноги — каждая точка лучом против
    /// коллайдеров BlocksSight (укрытия, стены, шторы, прохожие).</summary>
    private double Visibility(Drone d)
    {
        if (_hidingIn is not null) return 0;
        var model = d.Model;
        var eye = model.SensorPosition;
        var dir = model.SensorDirection;
        var p = Player.Position;
        var crouch = Player.Crouching;
        double[] heights = crouch ? new[] { 0.95, 0.6, 0.25 } : new[] { 1.65, 1.15, 0.35 };
        var cos = Math.Cos(model.HalfAngleDeg * Math.PI / 180);
        var best = 0.0;
        foreach (var h in heights)
        {
            var target = new Point3D(p.X, p.Y + h, p.Z);
            var to = target - eye;
            var dist = to.Length;
            if (dist > model.Range || dist < 0.1) continue;
            var c = Vector3D.DotProduct(to / dist, dir);
            if (c < cos) continue;
            if (!World.HasLineOfSight(eye, target, _npcBlockers)) continue;
            var centrality = (c - cos) / (1 - cos);
            var v = (1 - dist / model.Range * 0.55) * (0.6 + 0.4 * centrality);
            best = Math.Max(best, v);
        }
        return best;
    }

    private void UpdateDetection(double dt)
    {
        if (_detected)
        {
            _alarmTime += dt;
            var safe = Player.Position.Z > SafeZoneZ;
            Hud.SetDetection(1, MeterLevel.Danger, safe ? "Дрон отстал" : "ТРЕВОГА");
            if (_backupDrone is null && _alarmTime > 1.8)
                SpawnBackupDrone();
            return;
        }

        var seen = 0.0;
        Drone? seer = null;
        foreach (var d in Drones())
        {
            var v = Visibility(d);
            if (v > seen) { seen = v; seer = d; }
        }

        if (seen > 0 && seer is not null)
        {
            var rate = 0.95 * seen * (Player.Crouching ? 0.6 : 1.0) * (Player.Running ? 1.35 : 1.0);
            _detection = Math.Min(1, _detection + rate * dt);
            if (_detection > 0.35)
                seer.Suspicion = Player.Position + new Vector3D(0, 1, 0);
            _tickTimer -= dt;
            if (_tickTimer <= 0)
            {
                Audio.Play("detect_tick", 0.35 + _detection * 0.5);
                _tickTimer = 0.45 - _detection * 0.3;
            }
        }
        else
        {
            _detection = Math.Max(0, _detection - 0.32 * dt);
            if (_detection < 0.15)
                foreach (var d in Drones()) d.Suspicion = null;
        }

        var level = _detection < 0.35 ? MeterLevel.Calm : _detection < 0.75 ? MeterLevel.Suspicious : MeterLevel.Danger;
        Hud.SetDetection(_detection, level, seen > 0 ? "Вас сканируют" : _detection > 0.01 ? "Дрон теряет след" : "Дрон");

        if (_detection >= 1)
            TriggerAlarm();
    }

    private void TriggerAlarm()
    {
        _detected = true;
        _drone.Chasing = true;
        _drone.Suspicion = null;
        Audio.Play("radio", 0.8);
        Hud.Banner("ТРЕВОГА", "Дрон засёк Макса и вызывает патруль");
        Hud.SetObjective("Уходите!", "Бегите к прилавку Гоши — под навесы дрон не пролетит");
        Cam.AddTrauma(0.3);
    }

    private void SpawnBackupDrone()
    {
        var model = new DroneModel(10, 20) { FloorY = 0 };
        _backupDrone = new Drone
        {
            Model = model,
            Route = new() { (new Point3D(-9, DroneHeight, 60), 0) },
            Pos = new Point3D(-11, 7.4, 62),
            Chasing = true,
            Yaw = 120
        };
        Dynamic.Children.Add(model.Root);
        Transparent.Children.Insert(0, model.TransparentRoot);
        Hud.Toast("Второй дрон вылетел из технического люка");
    }

    // ------------------------------------------------------------------ финал

    private void StartOutro()
    {
        if (_outroStarted) return;
        _outroStarted = true;
        _route = _detourTime > _directTime ? "detour" : "direct";
        EnterPhase(Phase.Outro);
        Hud.SetInteractPrompt(null);
        Audio.Play("objective", 0.7);
        Cam.SetCinematic(new Point3D(-3.0, 2.0, 75.5), new Point3D(0.3, 1.3, 79.5), 1.6);
    }

    private void UpdateOutro(double dt)
    {
        // Макс сам подходит к прилавку и останавливается
        var target = new Point3D(-0.4, 0, 78.2);
        var to = target - Player.Position;
        to.Y = 0;
        var fwd = to.Length > 0.15 ? to / to.Length : new Vector3D();
        var scripted = new EpisodeInput { Scripted = true, Forward = to.Length > 0.15 };
        scripted.Poll(null, false);
        var camF = fwd.LengthSquared > 0 ? fwd : new Vector3D(0, 0, 1);
        Player.Update(dt, scripted, camF, new Vector3D(-camF.Z, 0, camF.X));
        if (to.Length <= 0.15)
            Player.Yaw += Math.IEEERemainder(0 - Player.Yaw, Math.PI * 2) * Math.Min(1, dt * 4);

        Hud.SetFade(Math.Clamp((PhaseTime - 2.6) / 0.8, 0, 1));
        if (PhaseTime > 3.5)
            FinishEpisode(_detected ? "DETECTED" : "CLEAN", _route);
    }

    // ------------------------------------------------------------------ окружение

    private void UpdateInteractableVisuals(double dt)
    {
        foreach (var it in _interactables)
        {
            it.Cooldown = Math.Max(0, it.Cooldown - dt);
            if (it.Kind == InteractKind.Tarp && it.Used && it.Progress < 1)
            {
                it.Progress = Math.Min(1, it.Progress + dt * 1.6);
                var e = 1 - Math.Pow(1 - it.Progress, 3);
                it.Unroll!.ScaleY = 0.02 + 0.98 * e;
            }
        }
    }

    private void UpdateAmbientEffects(double dt)
    {
        // пыль в свете ламп
        if (_rng.NextDouble() < dt * 25)
        {
            var p = Player.Position;
            _dust.Emit(new Point3D(p.X + (_rng.NextDouble() - 0.5) * 14, 0.5 + _rng.NextDouble() * 3.5, p.Z + _rng.NextDouble() * 14 - 3),
                new Vector3D((_rng.NextDouble() - 0.5) * 0.2, (_rng.NextDouble() - 0.3) * 0.1, (_rng.NextDouble() - 0.5) * 0.2), 4 + _rng.NextDouble() * 3, 0.05);
        }
        // пар из решёток
        foreach (var v in _steamVents)
        {
            if (_rng.NextDouble() < dt * 6 && Dist2D(v, Player.Position) < 30)
                _steam.Emit(v, new Vector3D((_rng.NextDouble() - 0.5) * 0.3, 0.9 + _rng.NextDouble() * 0.5, (_rng.NextDouble() - 0.5) * 0.3), 2.2, 0.4, 0.7, -0.05, 0.4);
        }
        // мигающие вывески
        foreach (var (model, on, off, rate) in _flickers)
        {
            var flick = Math.Sin(Time * 7 * rate) + Math.Sin(Time * 13.7 * rate);
            model.Material = flick < -1.55 ? off : on;
        }

        // тревога: красное пульсирующее освещение и сирена
        var alarm = _detected ? 0.5 + 0.5 * Math.Sin(Time * 6) : 0;
        _alarmLight.Color = Color.FromRgb((byte)(160 * alarm), (byte)(10 * alarm), (byte)(10 * alarm));
        _ambient.Color = _detected ? Color.FromRgb((byte)(70 + 30 * alarm), 50, 48) : Color.FromRgb(70, 62, 56);
        Hud.SetAlarm(_detected && CurrentPhase == Phase.Play ? alarm : 0);
        Audio.SetLoop("alarm", _detected && CurrentPhase == Phase.Play ? 0.45 : 0);
        Audio.SetLoop("drone_hum", VolumeAt(_drone.Pos, 0.9));
    }

    private double VolumeAt(Point3D source, double max) =>
        Math.Clamp(max * (1 - (source - Player.Position).Length / 26), 0, max);

    protected override LocomotionState CharacterState() => Player.Locomotion(hidden: false);

    protected override string FootstepSound() =>
        Player.Position.Y > 1.5 ? (Time % 2 < 1 ? "step_metal" : "step_metal2") : "step_concrete";

    protected override double CameraPivotHeight() => Player.Crouching ? 1.15 : 1.62;
}
