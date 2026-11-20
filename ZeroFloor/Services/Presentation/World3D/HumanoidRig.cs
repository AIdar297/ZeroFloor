using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ZeroFloor.Services.Presentation.World3D;

/// <summary>Сустав иерархической модели: поворот (Y → Z → X) и смещение относительно родителя.</summary>
public sealed class Joint
{
    private readonly AxisAngleRotation3D _x = new(new Vector3D(1, 0, 0), 0);
    private readonly AxisAngleRotation3D _y = new(new Vector3D(0, 1, 0), 0);
    private readonly AxisAngleRotation3D _z = new(new Vector3D(0, 0, 1), 0);
    private readonly TranslateTransform3D _t;

    public Joint(Vector3D offset)
    {
        _t = new TranslateTransform3D(offset);
        BaseOffset = offset;
        var group = new Transform3DGroup();
        group.Children.Add(new RotateTransform3D(_y));
        group.Children.Add(new RotateTransform3D(_z));
        group.Children.Add(new RotateTransform3D(_x));
        group.Children.Add(_t);
        Group.Transform = group;
    }

    public Model3DGroup Group { get; } = new();
    public Vector3D BaseOffset { get; }
    public Joint? Parent { get; private set; }

    /// <summary>Положение сустава в позе привязки (все углы 0) — для проекции портрета.</summary>
    public Vector3D BindWorld => (Parent?.BindWorld ?? new Vector3D()) + BaseOffset;

    public double X { get => _x.Angle; set => _x.Angle = value; }
    public double Y { get => _y.Angle; set => _y.Angle = value; }
    public double Z { get => _z.Angle; set => _z.Angle = value; }

    public Vector3D Offset
    {
        get => new(_t.OffsetX, _t.OffsetY, _t.OffsetZ);
        set { _t.OffsetX = value.X; _t.OffsetY = value.Y; _t.OffsetZ = value.Z; }
    }

    public Joint Add(Joint child)
    {
        child.Parent = this;
        Group.Children.Add(child.Group);
        return child;
    }

    /// <summary>
    /// Часть тела с проекцией портрета: треугольники, смотрящие вперёд и попадающие в кадр
    /// портрета, получают UV фронтальной проекции; остальные — материал «тыла» (ткань/волосы)
    /// с обычной развёрткой. Поза привязки фиксирует UV — при анимации текстура «едет» с телом.
    /// </summary>
    public void MeshProjected(PortraitSkin skin, Material back, Action<MeshBuilder> build, double minPhotoY = double.NaN, double frontThreshold = -0.2, ProjectionMode mode = ProjectionMode.Body)
    {
        var mb = new MeshBuilder();
        build(mb);
        if (mb.IsEmpty) return;
        var mesh = mb.Build();
        var offset = BindWorld;
        var lowest = double.IsNaN(minPhotoY) ? skin.LowestPhotoY : minPhotoY;

        var front = NewMesh();
        var rear = NewMesh();
        var idx = mesh.TriangleIndices;
        for (var t = 0; t + 2 < idx.Count; t += 3)
        {
            var a = mesh.Positions[idx[t]]; var b = mesh.Positions[idx[t + 1]]; var c = mesh.Positions[idx[t + 2]];
            var n = mesh.Normals[idx[t]] + mesh.Normals[idx[t + 1]] + mesh.Normals[idx[t + 2]];
            if (n.LengthSquared > 1e-12) n.Normalize();
            var cy = (a.Y + b.Y + c.Y) / 3 + offset.Y;
            var isFront = n.Z > frontThreshold && cy >= lowest;
            var target = isFront ? front : rear;
            for (var k = 0; k < 3; k++)
            {
                var i = idx[t + k];
                var p = mesh.Positions[i];
                target.Positions.Add(p);
                target.Normals.Add(mesh.Normals[i]);
                target.TextureCoordinates.Add(isFront ? skin.Project(p + offset, mode) : mesh.TextureCoordinates[i]);
                target.TriangleIndices.Add(target.Positions.Count - 1);
            }
        }
        if (front.TriangleIndices.Count > 0) { front.Freeze(); Group.Children.Add(new GeometryModel3D(front, skin.Front)); }
        if (rear.TriangleIndices.Count > 0) { rear.Freeze(); Group.Children.Add(new GeometryModel3D(rear, back)); }
    }

    private static MeshGeometry3D NewMesh() => new()
    {
        Positions = new Point3DCollection(),
        Normals = new Vector3DCollection(),
        TextureCoordinates = new PointCollection(),
        TriangleIndices = new Int32Collection()
    };

    public void Mesh(Material material, Action<MeshBuilder> build)
    {
        var mb = new MeshBuilder();
        build(mb);
        var model = mb.ToModel(material);
        if (model is not null)
            Group.Children.Add(model);
    }
}

public enum HumanoidStyle
{
    Max,
    Guard,
    Merchant,
    Civilian,
    Gosha
}

/// <summary>Внешний вид гуманоида — материалы и пропорции. Один и тот же скелет и аниматор
/// используются для Макса (обе главы — одна и та же модель), патрульных, торговцев и Гоши.</summary>
public sealed class HumanoidAppearance
{
    public HumanoidStyle Style { get; init; } = HumanoidStyle.Civilian;
    public Color Skin { get; init; } = Materials3D.Hex("#C49A7C");
    public Color Hair { get; init; } = Materials3D.Hex("#3A2A1E");
    public Color Top { get; init; } = Materials3D.Hex("#3C4048");
    public Color Bottom { get; init; } = Materials3D.Hex("#2E3138");
    public Color Boots { get; init; } = Materials3D.Hex("#1B1B1D");
    public Color Accent { get; init; } = Materials3D.Hex("#7A5A3A");
    public double Height { get; init; } = 1.0;
    public double Bulk { get; init; } = 1.0;
    public bool Hood { get; init; }
    public bool Apron { get; init; }
    public bool Cap { get; init; }
    public bool Backpack { get; init; }
    public bool Bald { get; init; }

    public static HumanoidAppearance MaxOrlov() => new()
    {
        Style = HumanoidStyle.Max,
        Skin = Materials3D.Hex("#C8987A"),
        Hair = Materials3D.Hex("#3B2B20"),
        Top = Materials3D.Hex("#45484D"),
        Bottom = Materials3D.Hex("#404348"),
        Boots = Materials3D.Hex("#1E1D1C"),
        Accent = Materials3D.Hex("#2A2B2C")
    };

    public static HumanoidAppearance Guard() => new()
    {
        Style = HumanoidStyle.Guard,
        Skin = Materials3D.Hex("#B98C6E"),
        Top = Materials3D.Hex("#1C2230"),
        Bottom = Materials3D.Hex("#181C26"),
        Boots = Materials3D.Hex("#0E0F12"),
        Accent = Materials3D.Hex("#0B0D12"),
        Bulk = 1.1,
        Height = 1.03
    };

    /// <summary>Виктор Савчук (по портрету): лысый, крепкий, оливковый лонгслив, карго-брюки.</summary>
    public static HumanoidAppearance ViktorSavchuk() => new()
    {
        Style = HumanoidStyle.Civilian,
        Skin = Materials3D.Hex("#C08E72"),
        Top = Materials3D.Hex("#5A5B3C"),
        Bottom = Materials3D.Hex("#4A4431"),
        Boots = Materials3D.Hex("#2A241C"),
        Accent = Materials3D.Hex("#3A3527"),
        Bald = true,
        Bulk = 1.14,
        Height = 1.02
    };

    public static HumanoidAppearance GoshaKovalchuk() => new()
    {
        Style = HumanoidStyle.Gosha,
        Skin = Materials3D.Hex("#C09075"),
        Hair = Materials3D.Hex("#4A3523"),
        Top = Materials3D.Hex("#5B4636"),
        Bottom = Materials3D.Hex("#2C2620"),
        Boots = Materials3D.Hex("#231A14"),
        Accent = Materials3D.Hex("#6B4527")
    };
}

/// <summary>Состояние движения, по которому аниматор строит позу каждый кадр.</summary>
public struct LocomotionState
{
    public double Speed;
    public double RunSpeed;
    public bool Grounded;
    public double VerticalVelocity;
    public double Crouch;
    public bool Sliding;
    public double Stumble;
    public double Hit;
    public double Land;
    public bool Gesture;
    public bool Breathless;
    public bool Searching;
    public bool Hidden;
}

/// <summary>
/// Low-poly гуманоид с иерархическим скелетом и полностью процедурной анимацией: ходьба, бег,
/// остановка, присед/крадущийся шаг, прыжок, приземление, скольжение, потеря равновесия,
/// получение удара, ожидание/торговля, поиск, тяжёлое дыхание. Целевые углы суставов
/// считаются из LocomotionState и сглаживаются — переходы между состояниями плавные.
/// </summary>
public sealed class HumanoidRig
{
    private readonly Joint _root, _body, _pelvis, _spine, _neck, _head;
    private readonly Joint _shoulderL, _shoulderR, _elbowL, _elbowR;
    private readonly Joint _hipL, _hipR, _kneeL, _kneeR, _ankleL, _ankleR;
    private readonly Joint[] _all;
    private readonly Dictionary<Joint, (double x, double y, double z)> _smoothed = new();
    private readonly double _scale;
    private double _phase;
    private double _time;
    private double _bodyY;
    private double _prevSpeed;
    private double _brake;
    private readonly double _seed;

    public HumanoidRig(HumanoidAppearance look, int seed = 0)
    {
        Look = look;
        _scale = look.Height;
        _seed = seed * 1.37;
        var b = look.Bulk;

        _root = new Joint(new Vector3D());
        _body = _root.Add(new Joint(new Vector3D()));
        _pelvis = _body.Add(new Joint(new Vector3D(0, 0.97, 0)));
        _spine = _pelvis.Add(new Joint(new Vector3D(0, 0.08, 0)));
        _neck = _spine.Add(new Joint(new Vector3D(0, 0.5, 0)));
        _head = _neck.Add(new Joint(new Vector3D(0, 0.08, 0)));
        _shoulderL = _spine.Add(new Joint(new Vector3D(0.235 * b, 0.43, 0)));
        _shoulderR = _spine.Add(new Joint(new Vector3D(-0.235 * b, 0.43, 0)));
        _elbowL = _shoulderL.Add(new Joint(new Vector3D(0, -0.29, 0)));
        _elbowR = _shoulderR.Add(new Joint(new Vector3D(0, -0.29, 0)));
        _hipL = _pelvis.Add(new Joint(new Vector3D(0.1 * b, -0.06, 0)));
        _hipR = _pelvis.Add(new Joint(new Vector3D(-0.1 * b, -0.06, 0)));
        _kneeL = _hipL.Add(new Joint(new Vector3D(0, -0.44, 0)));
        _kneeR = _hipR.Add(new Joint(new Vector3D(0, -0.44, 0)));
        _ankleL = _kneeL.Add(new Joint(new Vector3D(0, -0.42, 0)));
        _ankleR = _kneeR.Add(new Joint(new Vector3D(0, -0.42, 0)));
        _all = new[] { _body, _pelvis, _spine, _neck, _head, _shoulderL, _shoulderR, _elbowL, _elbowR, _hipL, _hipR, _kneeL, _kneeR, _ankleL, _ankleR };

        var skin = look.Style == HumanoidStyle.Max ? PortraitSkin.LoadMax() : null;
        if (skin is not null)
            BuildMaxFromPortrait(look, skin);
        else
            BuildMeshes(look);

        _rootTransform = new Transform3DGroup();
        _rootTransform.Children.Add(new ScaleTransform3D(_scale, _scale, _scale));
        _rootTransform.Children.Add(new RotateTransform3D(_yaw));
        _rootTransform.Children.Add(_translate);
        Root = new Model3DGroup();
        Root.Children.Add(_root.Group);
        Root.Transform = _rootTransform;
    }

    private readonly Transform3DGroup _rootTransform;
    private readonly AxisAngleRotation3D _yaw = new(new Vector3D(0, 1, 0), 0);
    private readonly TranslateTransform3D _translate = new();

    public HumanoidAppearance Look { get; }
    public Model3DGroup Root { get; }

    /// <summary>Отделяемая модель гаечного ключа на поясе (только у Макса) — для анимации падения.</summary>
    public Model3DGroup? Wrench { get; private set; }

    public Point3D Position
    {
        get => new(_translate.OffsetX, _translate.OffsetY, _translate.OffsetZ);
        set { _translate.OffsetX = value.X; _translate.OffsetY = value.Y; _translate.OffsetZ = value.Z; }
    }

    public double YawDegrees { get => _yaw.Angle; set => _yaw.Angle = value; }

    /// <summary>Скрыть/показать модель (Макс внутри шкафчика).</summary>
    public bool Visible
    {
        get => Root.Children.Contains(_root.Group);
        set
        {
            if (value && !Visible) Root.Children.Add(_root.Group);
            else if (!value) Root.Children.Remove(_root.Group);
        }
    }

    /// <summary>Мировая позиция точки на теле (для прицела камеры, фонаря, детектора).</summary>
    public Point3D HeadPosition => Position + new Vector3D(0, (1.62 + _bodyY) * _scale, 0);

    /// <summary>Мировая точка пояса (откуда падает гаечный ключ).</summary>
    public Point3D HipPosition(double side) =>
        _rootTransform.Value.Transform(new Point3D(-0.2 * side, 0.95 + _bodyY, 0.02));

    public void DetachWrench()
    {
        if (Wrench is null) return;
        _pelvis.Group.Children.Remove(Wrench);
        Wrench = null;
    }

    // ------------------------------------------------------------------ анимация

    public void Update(double dt, LocomotionState s)
    {
        _time += dt;
        var run = s.RunSpeed <= 0 ? 6 : s.RunSpeed;
        var speed = s.Speed;
        var moveAmount = Math.Clamp(speed / 1.2, 0, 1);
        var runAmount = Math.Clamp((speed - 2.6) / (run - 2.6), 0, 1);
        var crouch = Math.Clamp(s.Crouch, 0, 1);

        // торможение: резкая потеря скорости → короткий наклон назад
        var decel = (_prevSpeed - speed) / Math.Max(dt, 1e-3);
        if (decel > 9 && _prevSpeed > 3 && s.Grounded) _brake = 1;
        _brake = Math.Max(0, _brake - dt * 2.5);
        _prevSpeed = speed;

        var stride = 1.05 + runAmount * 0.9 - crouch * 0.35;
        _phase += dt * speed / Math.Max(0.4, stride) * Math.PI;
        var ph = _phase;
        var sinL = Math.Sin(ph);
        var sinR = Math.Sin(ph + Math.PI);

        var legAmp = (26 + runAmount * 22) * moveAmount;
        var kneeAmp = (30 + runAmount * 55) * moveAmount;
        var armAmp = (22 + runAmount * 30) * moveAmount;
        var elbowBase = 12 + runAmount * 70;

        double hipLx = sinL * legAmp, hipRx = sinR * legAmp;
        double kneeL = Math.Max(0, Math.Cos(ph)) * kneeAmp + runAmount * 15 * moveAmount;
        double kneeR = Math.Max(0, Math.Cos(ph + Math.PI)) * kneeAmp + runAmount * 15 * moveAmount;
        double ankleL = -hipLx * 0.3, ankleR = -hipRx * 0.3;
        double shLx = -sinL * armAmp, shRx = -sinR * armAmp, shLz = 6, shRz = -6;
        double elL = -(elbowBase + Math.Max(0, sinR) * 15 * moveAmount);
        double elR = -(elbowBase + Math.Max(0, sinL) * 15 * moveAmount);
        double spineX = runAmount * 13 * moveAmount + 3, spineY = sinL * 6 * moveAmount, spineZ = 0;
        double headX = -spineX * 0.6, headY = 0, headZ = 0;
        double pelvisY = -sinL * 5 * moveAmount;
        var bob = -Math.Abs(Math.Sin(ph)) * (0.025 + runAmount * 0.05) * moveAmount;
        double bodyX = 0;

        // ожидание: дыхание, перенос веса, осмотр
        var idle = 1 - moveAmount;
        if (idle > 0)
        {
            var breath = Math.Sin(_time * 1.6 + _seed);
            spineX += breath * 1.5 * idle;
            shLz += breath * 1.2 * idle; shRz -= breath * 1.2 * idle;
            pelvisY += Math.Sin(_time * 0.4 + _seed) * 4 * idle;
            headY += Math.Sin(_time * 0.27 + _seed * 2) * 18 * idle;
            elL -= 8 * idle; elR -= 8 * idle;
        }

        if (s.Gesture && moveAmount < 0.3)
        {
            // торговля: жестикуляция правой рукой, лёгкий наклон к собеседнику
            var g = Math.Sin(_time * 2.3 + _seed);
            shRx = 45 + g * 15; elR = -70 + g * 20; shRz = -15;
            spineX += 6; headX += 5;
            shLx = 20; elL = -60;
        }

        if (s.Searching && moveAmount < 0.5)
        {
            headY = Math.Sin(_time * 1.3 + _seed) * 50;
            spineY = Math.Sin(_time * 1.3 + _seed) * 20;
        }

        // присед / крадущийся шаг
        if (crouch > 0)
        {
            hipLx = hipLx * (1 - crouch * 0.4) + 62 * crouch;
            hipRx = hipRx * (1 - crouch * 0.4) + 62 * crouch;
            kneeL = kneeL * (1 - crouch * 0.4) + 105 * crouch;
            kneeR = kneeR * (1 - crouch * 0.4) + 105 * crouch;
            ankleL += -40 * crouch; ankleR += -40 * crouch;
            spineX += 22 * crouch; headX -= 14 * crouch;
            shLx = shLx * 0.5 + 25 * crouch; shRx = shRx * 0.5 + 25 * crouch;
            elL -= 25 * crouch; elR -= 25 * crouch;
            bob -= 0.36 * crouch;
        }

        // в воздухе: прыжок / падение
        if (!s.Grounded && !s.Sliding)
        {
            var up = Math.Clamp(s.VerticalVelocity / 6, -1, 1);
            var t = up > 0 ? up : 0;
            hipLx = 50 * t + 25; kneeL = 75 * t + 20;
            hipRx = -10 + 25 * (1 - t); kneeR = 30 + 30 * (1 - t);
            ankleL = -20; ankleR = -10;
            shLx = 40 + 40 * t; shRx = 25; shLz = 25; shRz = -35;
            elL = -40; elR = -50;
            spineX = 10 - 8 * (1 - t); bob = 0.03;
        }

        // приземление: амортизация коленями
        if (s.Land > 0)
        {
            var l = s.Land;
            hipLx += 40 * l; hipRx += 40 * l; kneeL += 75 * l; kneeR += 75 * l;
            ankleL -= 35 * l; ankleR -= 35 * l;
            spineX += 18 * l; bob -= 0.22 * l;
            shLz += 15 * l; shRz -= 15 * l;
        }

        // скольжение под препятствием
        if (s.Sliding)
        {
            hipLx = 85; kneeL = 8; ankleL = 10;
            hipRx = 35; kneeR = 110; ankleR = -20;
            spineX = -38; headX = 30;
            shLx = 20; shLz = 45; elL = -10;
            shRx = -10; shRz = -50; elR = -20;
            bob = -0.6;
            bodyX = 0;
        }

        // торможение
        spineX -= 14 * _brake;
        headX += 6 * _brake;

        // потеря равновесия: размахивание руками, раскачка корпуса
        if (s.Stumble > 0)
        {
            var w = s.Stumble;
            var f = _time * 13;
            shLz += (50 + Math.Sin(f) * 40) * w; shRz -= (50 + Math.Sin(f + 1.7) * 40) * w;
            shLx += Math.Sin(f * 0.7) * 50 * w; shRx += Math.Cos(f * 0.8) * 50 * w;
            spineZ += Math.Sin(_time * 7) * 14 * w;
            spineX += (Math.Sin(_time * 5) * 12 - 6) * w;
            headZ += Math.Sin(_time * 9) * 10 * w;
            bob -= 0.06 * w;
        }

        // получение удара: рывок корпуса назад
        if (s.Hit > 0)
        {
            var h = s.Hit;
            spineX -= 28 * h; headX -= 22 * h; headZ += 12 * h;
            shLz += 30 * h; shRz -= 30 * h;
        }

        // тяжёлое дыхание после погони: руки на коленях
        if (s.Breathless && moveAmount < 0.2)
        {
            var heave = Math.Sin(_time * 4.5);
            spineX = 48 + heave * 4; headX = -32;
            hipLx = 30; hipRx = 30; kneeL = 36; kneeR = 36; ankleL = -8; ankleR = -8;
            // корпус наклонён на ~48°, руки свисают к коленям: относительно корпуса — чуть назад
            shLx = -18; shRx = -18; shLz = 10; shRz = -10; elL = -8; elR = -8;
            bob = -0.12 + heave * 0.01;
        }

        if (s.Hidden)
        {
            // в укрытии: компактная поза, сжавшись
            hipLx = hipRx = 95; kneeL = kneeR = 140; ankleL = ankleR = -45;
            spineX = 35; headX = -10; shLx = shRx = 55; elL = elR = -100;
            bob = -0.55;
        }

        var k = 1 - Math.Exp(-dt * 16);
        Blend(_pelvis, pelvisY * 0, pelvisY, 0, k);
        Blend(_spine, spineX, spineY, spineZ, k);
        Blend(_head, headX, headY, headZ, k);
        Blend(_neck, 0, 0, 0, k);
        Blend(_shoulderL, shLx, 0, shLz, k);
        Blend(_shoulderR, shRx, 0, shRz, k);
        Blend(_elbowL, elL, 0, 0, k);
        Blend(_elbowR, elR, 0, 0, k);
        Blend(_hipL, -hipLx, 0, 3, k);
        Blend(_hipR, -hipRx, 0, -3, k);
        Blend(_kneeL, kneeL, 0, 0, k);
        Blend(_kneeR, kneeR, 0, 0, k);
        Blend(_ankleL, ankleL, 0, 0, k);
        Blend(_ankleR, ankleR, 0, 0, k);
        Blend(_body, bodyX, 0, 0, k);

        _bodyY += (bob - _bodyY) * (1 - Math.Exp(-dt * 18));
        _body.Offset = new Vector3D(0, _bodyY, 0);
    }

    private void Blend(Joint j, double x, double y, double z, double k)
    {
        // Соглашение: X > 0 у бедра/плеча = мах вперёд (в модели это отрицательный поворот вокруг X).
        if (j == _shoulderL || j == _shoulderR) x = -x;

        _smoothed.TryGetValue(j, out var cur);
        cur = (cur.x + (x - cur.x) * k, cur.y + (y - cur.y) * k, cur.z + (z - cur.z) * k);
        _smoothed[j] = cur;
        j.X = cur.x; j.Y = cur.y; j.Z = cur.z;
    }

    // ------------------------------------------------------------------ геометрия

    private void BuildMeshes(HumanoidAppearance look)
    {
        var skin = Materials3D.Matte(look.Skin, 0.08, 12);
        var hair = Materials3D.Matte(look.Hair, 0.05, 8);
        var top = look.Style == HumanoidStyle.Gosha ? Materials3D.Patchwork() : Materials3D.Fabric(look.Top);
        var bottom = Materials3D.Fabric(look.Bottom);
        var boots = Materials3D.Matte(look.Boots, 0.2, 30);
        var accent = Materials3D.Matte(look.Accent, 0.15, 20);
        var metal = Materials3D.Matte(Materials3D.Hex("#6C7075"), 0.6, 60);
        var b = look.Bulk;
        var guard = look.Style == HumanoidStyle.Guard;

        // ноги
        foreach (var (hip, knee, ankle) in new[] { (_hipL, _kneeL, _ankleL), (_hipR, _kneeR, _ankleR) })
        {
            hip.Mesh(bottom, m => m.AddCylinder(new Point3D(0, -0.22, 0), 0.075 * b, 0.46, 8, Matrix3D.Identity, true, 1, 0.088 * b));
            knee.Mesh(bottom, m => m.AddCylinder(new Point3D(0, -0.2, 0), 0.056 * b, 0.42, 8, Matrix3D.Identity, true, 1, 0.07 * b));
            if (guard)
                knee.Mesh(accent, m => m.AddBox(new Point3D(0, -0.02, 0.06), new Vector3D(0.11, 0.13, 0.05)));
            ankle.Mesh(boots, m =>
            {
                m.AddCylinder(new Point3D(0, 0.03, 0), 0.064 * b, 0.16, 8);
                m.AddBox(new Point3D(0, -0.075, 0.045), new Vector3D(0.115 * b, 0.07, 0.27));
            });
        }

        // таз и пояс
        _pelvis.Mesh(bottom, m => m.AddBox(new Point3D(0, 0, 0), new Vector3D(0.34 * b, 0.2, 0.22 * b)));
        _pelvis.Mesh(accent, m =>
        {
            m.AddBox(new Point3D(0, 0.07, 0), new Vector3D(0.36 * b, 0.06, 0.24 * b));
            m.AddBox(new Point3D(0.2 * b, 0.0, 0.03), new Vector3D(0.07, 0.12, 0.13)); // подсумок
        });
        _pelvis.Mesh(metal, m => m.AddBox(new Point3D(0, 0.07, 0.125 * b), new Vector3D(0.08, 0.05, 0.015)));

        if (look.Style == HumanoidStyle.Max)
        {
            // гаечный ключ на правом бедре — отдельная модель, может выпасть в вентиляции
            var wrenchMb = new MeshBuilder();
            var wm = MeshBuilder.Trs(new Vector3D(-0.2 * b, -0.02, 0.02), rollDeg: 12);
            wrenchMb.AddBox(new Point3D(0, 0, 0), new Vector3D(0.025, 0.24, 0.012), wm);
            wrenchMb.AddBox(new Point3D(0, 0.13, 0), new Vector3D(0.06, 0.04, 0.014), wm);
            wrenchMb.AddBox(new Point3D(0, -0.13, 0), new Vector3D(0.05, 0.035, 0.014), wm);
            var wrenchModel = wrenchMb.ToModel(Materials3D.Matte(Materials3D.Hex("#8E9398"), 0.8, 70))!;
            Wrench = new Model3DGroup();
            Wrench.Children.Add(wrenchModel);
            _pelvis.Group.Children.Add(Wrench);
        }

        // корпус
        _spine.Mesh(top, m =>
        {
            m.AddBox(new Point3D(0, 0.15, 0), new Vector3D(0.31 * b, 0.28, 0.2 * b));
            m.AddBox(new Point3D(0, 0.35, 0), new Vector3D(0.41 * b, 0.25, 0.24 * b));
            m.AddBox(new Point3D(0, 0.455, -0.01), new Vector3D(0.3 * b, 0.05, 0.2 * b));
        });

        switch (look.Style)
        {
            case HumanoidStyle.Max:
                _spine.Mesh(Materials3D.Matte(Materials3D.Hex("#2C2E2C")), m => m.AddBox(new Point3D(0, 0.43, 0.118 * b), new Vector3D(0.1, 0.07, 0.01)));
                _spine.Mesh(Materials3D.Fabric(Materials3D.Hex("#3A3D42")), m =>
                {
                    m.AddBox(new Point3D(0.1, 0.36, 0.125 * b), new Vector3D(0.11, 0.1, 0.012)); // карманы
                    m.AddBox(new Point3D(-0.1, 0.36, 0.125 * b), new Vector3D(0.11, 0.1, 0.012));
                    m.AddBoxAt(new Point3D(0.07, 0.47, 0.1), new Vector3D(0.09, 0.03, 0.06), rollDeg: -20); // воротник
                    m.AddBoxAt(new Point3D(-0.07, 0.47, 0.1), new Vector3D(0.09, 0.03, 0.06), rollDeg: 20);
                });
                _spine.Mesh(Materials3D.Matte(Materials3D.Hex("#7C8086"), 0.5, 50), m => m.AddBox(new Point3D(0, 0.25, 0.103 * b), new Vector3D(0.012, 0.4, 0.008))); // молния
                _spine.Mesh(Materials3D.Matte(Materials3D.Hex("#2C2E33")), m => m.AddBox(new Point3D(0, 0.3, -0.13 * b), new Vector3D(0.26, 0.3, 0.06))); // потёртый ранец-подсумок
                break;
            case HumanoidStyle.Guard:
                _spine.Mesh(Materials3D.MetalPanel(Materials3D.Hex("#20242C"), 0.05, 0.35), m =>
                {
                    m.AddBox(new Point3D(0, 0.3, 0.015), new Vector3D(0.44 * b, 0.36, 0.28 * b)); // бронежилет
                    m.AddBox(new Point3D(0.25 * b, 0.45, 0), new Vector3D(0.14, 0.08, 0.2)); // наплечники
                    m.AddBox(new Point3D(-0.25 * b, 0.45, 0), new Vector3D(0.14, 0.08, 0.2));
                });
                _spine.Mesh(Materials3D.Emissive(Materials3D.Hex("#E0353A"), 0.9), m => m.AddBox(new Point3D(0.1, 0.4, 0.16 * b), new Vector3D(0.05, 0.02, 0.01)));
                _spine.Mesh(Materials3D.Emissive(Materials3D.Hex("#9FD7FF"), 0.8), m => m.AddBox(new Point3D(-0.1, 0.36, 0.16 * b), new Vector3D(0.08, 0.025, 0.01)));
                break;
            case HumanoidStyle.Gosha:
                _spine.Mesh(Materials3D.Matte(Materials3D.Hex("#262422")), m => m.AddBox(new Point3D(0, 0.3, 0.118 * b), new Vector3D(0.14, 0.32, 0.012)));
                _spine.Mesh(Materials3D.Emissive(Materials3D.Hex("#E8B84A"), 0.9), m => m.AddBox(new Point3D(0.12, 0.4, 0.125), new Vector3D(0.035, 0.035, 0.01))); // значок
                break;
        }

        if (look.Apron)
            _spine.Mesh(Materials3D.Fabric(look.Accent), m => m.AddBox(new Point3D(0, 0.15, 0.115 * b), new Vector3D(0.3 * b, 0.45, 0.02)));
        if (look.Backpack)
            _spine.Mesh(Materials3D.Fabric(look.Accent), m => m.AddBox(new Point3D(0, 0.3, -0.17 * b), new Vector3D(0.28, 0.36, 0.14)));

        // шея и голова
        _neck.Mesh(skin, m => m.AddCylinder(new Point3D(0, 0.03, 0), 0.052, 0.12, 8));
        _head.Mesh(skin, m =>
        {
            m.AddSphere(new Point3D(0, 0.1, 0.01), new Vector3D(0.1, 0.122, 0.112), 10, 7, Matrix3D.Identity);
            m.AddBox(new Point3D(0, 0.02, 0.05), new Vector3D(0.14, 0.07, 0.1)); // челюсть
            m.AddBox(new Point3D(0, 0.1, 0.118), new Vector3D(0.025, 0.05, 0.03)); // нос
            m.AddBox(new Point3D(0.1, 0.1, 0.0), new Vector3D(0.02, 0.05, 0.035)); // уши
            m.AddBox(new Point3D(-0.1, 0.1, 0.0), new Vector3D(0.02, 0.05, 0.035));
        });
        _head.Mesh(Materials3D.Matte(Materials3D.Hex("#141210")), m =>
        {
            m.AddBox(new Point3D(0.038, 0.125, 0.104), new Vector3D(0.026, 0.012, 0.01));
            m.AddBox(new Point3D(-0.038, 0.125, 0.104), new Vector3D(0.026, 0.012, 0.01));
        });

        if (guard)
        {
            _head.Mesh(Materials3D.MetalPanel(Materials3D.Hex("#1A1E26"), 0.02, 0.5), m =>
                m.AddSphere(new Point3D(0, 0.14, 0.0), new Vector3D(0.135, 0.12, 0.14), 10, 6, Matrix3D.Identity));
            _head.Mesh(Materials3D.Glow(Materials3D.Hex("#5AB2FF"), 0.55), m =>
                m.AddBox(new Point3D(0, 0.11, 0.11), new Vector3D(0.2, 0.06, 0.03)));
        }
        else if (look.Hood)
        {
            _head.Mesh(Materials3D.Fabric(look.Top), m =>
                m.AddSphere(new Point3D(0, 0.12, -0.02), new Vector3D(0.13, 0.14, 0.135), 10, 6, Matrix3D.Identity));
        }
        else if (look.Cap)
        {
            _head.Mesh(Materials3D.Fabric(look.Accent), m =>
            {
                m.AddSphere(new Point3D(0, 0.17, 0.0), new Vector3D(0.112, 0.07, 0.12), 10, 4, Matrix3D.Identity);
                m.AddBox(new Point3D(0, 0.17, 0.13), new Vector3D(0.15, 0.015, 0.08));
            });
        }
        else if (!look.Bald)
        {
            _head.Mesh(hair, m =>
            {
                m.AddSphere(new Point3D(0, 0.165, -0.01), new Vector3D(0.112, 0.075, 0.125), 10, 5, Matrix3D.Identity);
                var rng = new Random(look.Style == HumanoidStyle.Gosha ? 9 : 4);
                var spikes = look.Style is HumanoidStyle.Max or HumanoidStyle.Gosha ? 14 : 6;
                for (var i = 0; i < spikes; i++)
                {
                    var a = rng.NextDouble() * Math.PI * 2;
                    var tilt = look.Style == HumanoidStyle.Gosha ? 15 : 35;
                    var tr = MeshBuilder.Trs(new Vector3D(Math.Cos(a) * 0.06, 0.2, Math.Sin(a) * 0.06 - 0.01),
                        yawDeg: rng.Next(360), pitchDeg: rng.Next(-tilt, tilt), rollDeg: rng.Next(-tilt, tilt));
                    m.AddBox(new Point3D(0, 0.02, 0), new Vector3D(0.035, 0.07, 0.03), tr);
                }
                m.AddBoxAt(new Point3D(0, 0.15, 0.09), new Vector3D(0.15, 0.04, 0.05), pitchDeg: 20); // чёлка
            });
        }

        // руки
        var sleeve = top;
        foreach (var (sh, el, side) in new[] { (_shoulderL, _elbowL, 1), (_shoulderR, _elbowR, -1) })
        {
            sh.Mesh(sleeve, m =>
            {
                m.AddSphere(new Point3D(0, -0.02, 0), new Vector3D(0.075 * b, 0.07, 0.075 * b), 8, 5, Matrix3D.Identity);
                m.AddCylinder(new Point3D(0, -0.15, 0), 0.052 * b, 0.3, 8, Matrix3D.Identity, true, 1, 0.062 * b);
            });

            var rolled = look.Style is HumanoidStyle.Max;
            el.Mesh(sleeve, m => m.AddCylinder(new Point3D(0, rolled ? -0.04 : -0.13, 0), 0.054 * b, rolled ? 0.09 : 0.26, 8));
            if (rolled)
                el.Mesh(skin, m => m.AddCylinder(new Point3D(0, -0.17, 0), 0.042, 0.18, 8));

            var gloves = guard || look.Style == HumanoidStyle.Gosha;
            el.Mesh(gloves ? accent : skin, m => m.AddBox(new Point3D(0, -0.31, 0.005), new Vector3D(0.07, 0.11, 0.045)));

            if (look.Style == HumanoidStyle.Max && side == 1)
            {
                // наручный сканер на левом предплечье
                el.Mesh(Materials3D.MetalPanel(Materials3D.Hex("#35383C"), 0.1, 0.4), m =>
                    m.AddBox(new Point3D(0, -0.18, 0), new Vector3D(0.1, 0.1, 0.095)));
                el.Mesh(Materials3D.Screen(Materials3D.Hex("#39E3D2")), m =>
                    m.AddBox(new Point3D(0.052, -0.18, 0), new Vector3D(0.006, 0.07, 0.06)));
                el.Mesh(Materials3D.Emissive(Materials3D.Hex("#39E3D2"), 1), m =>
                    m.AddBox(new Point3D(0, -0.135, 0.05), new Vector3D(0.02, 0.01, 0.006)));
            }

            if (look.Style == HumanoidStyle.Gosha)
                el.Mesh(Materials3D.Emissive(Materials3D.Hex("#E8B84A"), 0.8), m =>
                    m.AddBox(new Point3D(0, -0.3, 0.03), new Vector3D(0.04, 0.03, 0.008)));

            if (guard && side == -1)
            {
                // фонарь в правой руке
                el.Mesh(Materials3D.Matte(Materials3D.Hex("#101114"), 0.5, 40), m =>
                    m.AddCylinder(new Point3D(0, 0, 0), 0.025, 0.2, 8, MeshBuilder.Trs(new Vector3D(0, -0.33, 0.08), pitchDeg: 90), true));
                el.Mesh(Materials3D.Emissive(Materials3D.Hex("#FFF4D8"), 1), m =>
                    m.AddBox(new Point3D(0, -0.33, 0.185), new Vector3D(0.045, 0.045, 0.01)));
            }
        }
    }

    /// <summary>
    /// Макс по портрету: более гладкое тело (эллипсоиды, 12–16 сегментов) с фронтальной
    /// проекцией изображения max_orlov.png. Ноги ниже кадра портрета «достроены» тканью брюк
    /// комбинезона с того же портрета, ботинки и подошвы смоделированы. Наручный сканер и
    /// гаечный ключ — отдельные детали (сканер — по Story Bible, ключ нужен Главе 6).
    /// </summary>
    private void BuildMaxFromPortrait(HumanoidAppearance look, PortraitSkin skin)
    {
        var boots = Materials3D.Matte(Materials3D.Hex("#1C1B1A"), 0.25, 30);
        var sole = Materials3D.Matte(Materials3D.Hex("#0E0E0E"), 0.1, 10);
        var metal = Materials3D.Matte(Materials3D.Hex("#7E8288"), 0.6, 60);
        var belt = Materials3D.Matte(Materials3D.Hex("#2A2A2A"), 0.2, 20);
        var pouch = Materials3D.MetalPanel(Materials3D.Hex("#3E4040"), 0.05, 0.4);

        // ноги: бёдра — портрет (сколько видно в кадре) + ткань; голени — ткань брюк
        foreach (var (hip, knee, ankle, side) in new[] { (_hipL, _kneeL, _ankleL, 1), (_hipR, _kneeR, _ankleR, -1) })
        {
            hip.Mesh(skin.Trousers, m =>
            {
                m.AddCylinder(new Point3D(0, -0.22, 0), 0.08, 0.46, 14, Matrix3D.Identity, false, 0.4, 0.098);
                m.AddSphere(new Point3D(0, 0.0, 0), new Vector3D(0.098, 0.06, 0.098), 14, 5, Matrix3D.Identity);
            });
            if (side == -1)
                hip.Mesh(skin.Trousers, m => m.AddBoxAt(new Point3D(-0.085, -0.2, 0.01), new Vector3D(0.03, 0.16, 0.13))); // карго-карман
            knee.Mesh(skin.Trousers, m =>
            {
                m.AddSphere(new Point3D(0, 0, 0), new Vector3D(0.078, 0.06, 0.078), 12, 5, Matrix3D.Identity);
                m.AddCylinder(new Point3D(0, -0.19, 0), 0.062, 0.38, 14, Matrix3D.Identity, false, 0.4, 0.078);
            });
            ankle.Mesh(boots, m =>
            {
                m.AddCylinder(new Point3D(0, 0.04, 0), 0.066, 0.15, 14);
                m.AddBoxAt(new Point3D(0, -0.07, 0.04), new Vector3D(0.115, 0.075, 0.25));
                m.AddSphere(new Point3D(0, -0.07, 0.15), new Vector3D(0.058, 0.04, 0.06), 10, 5, Matrix3D.Identity);
            });
            ankle.Mesh(sole, m => m.AddBoxAt(new Point3D(0, -0.1, 0.045), new Vector3D(0.12, 0.025, 0.27)));
        }

        // таз и ремень (с портрета), пряжка, прибор и ручка-фонарик на правом бедре
        _pelvis.MeshProjected(skin, skin.Trousers, m =>
            m.AddSphere(new Point3D(0, 0.0, 0), new Vector3D(0.18, 0.13, 0.12), 16, 8, Matrix3D.Identity), minPhotoY: 0.9);
        _pelvis.MeshProjected(skin, belt, m => m.AddCylinder(new Point3D(0, 0, 0), 0.178, 0.05, 18, Squash(1, 1, 0.72) * Move(0, 0.07, 0), true));
        _pelvis.Mesh(pouch, m =>
        {
            m.AddBoxAt(new Point3D(-0.19, 0.02, 0.04), new Vector3D(0.05, 0.12, 0.09));
            m.AddCylinder(new Point3D(-0.195, 0.05, 0.1), 0.012, 0.11, 6);
        });

        var wrenchMb = new MeshBuilder();
        var wm = MeshBuilder.Trs(new Vector3D(0.19, -0.02, 0.04), rollDeg: -12);
        wrenchMb.AddBox(new Point3D(0, 0, 0), new Vector3D(0.022, 0.22, 0.012), wm);
        wrenchMb.AddBox(new Point3D(0, 0.12, 0), new Vector3D(0.055, 0.04, 0.014), wm);
        wrenchMb.AddBox(new Point3D(0, -0.12, 0), new Vector3D(0.045, 0.03, 0.014), wm);
        Wrench = new Model3DGroup();
        Wrench.Children.Add(wrenchMb.ToModel(Materials3D.Matte(Materials3D.Hex("#8E9398"), 0.8, 70))!);
        _pelvis.Group.Children.Add(Wrench);

        // корпус: живот + грудь + плечевой пояс
        _spine.MeshProjected(skin, skin.Fabric, m =>
        {
            m.AddCylinder(new Point3D(0, 0.2, 0), 0.17, 0.34, 18, Squash(1, 1, 0.66), false, 0.6, 0.2);
            m.AddSphere(new Point3D(0, 0.37, 0.0), new Vector3D(0.2, 0.1, 0.13), 18, 8, Matrix3D.Identity);
            m.AddSphere(new Point3D(0, 0.04, 0.0), new Vector3D(0.172, 0.06, 0.113), 18, 6, Matrix3D.Identity);
        });
        // воротник: стойка вокруг шеи (сам воротник рубашки виден на проекции портрета)
        _spine.MeshProjected(skin, skin.Fabric, m =>
            m.AddCylinder(new Point3D(0, 0.48, -0.005), 0.07, 0.05, 14, Matrix3D.Identity, false, 0.4, 0.06));

        // шея и голова
        _neck.Mesh(Materials3D.Matte(Materials3D.Hex("#9C7560"), 0.06, 12), m => m.AddCylinder(new Point3D(0, 0.03, 0.005), 0.05, 0.13, 12));
        _head.MeshProjected(skin, skin.Hair, m =>
        {
            m.AddSphere(new Point3D(0, 0.1, 0.0), new Vector3D(0.098, 0.12, 0.108), 16, 12, Matrix3D.Identity);
            m.AddSphere(new Point3D(0, 0.035, 0.03), new Vector3D(0.075, 0.06, 0.08), 12, 8, Matrix3D.Identity);
        }, frontThreshold: 0.05, mode: ProjectionMode.Head);
        // растрёпанные волосы (как на портрете)
        _head.MeshProjected(skin, skin.Hair, m =>
        {
            m.AddSphere(new Point3D(0, 0.165, -0.012), new Vector3D(0.112, 0.08, 0.122), 16, 7, Matrix3D.Identity);
            var rng = new Random(4);
            for (var i = 0; i < 26; i++)
            {
                var a = rng.NextDouble() * Math.PI * 2;
                var r = 0.03 + rng.NextDouble() * 0.06;
                var back = Math.Sin(a) < 0;
                // пряди только на макушке и затылке — спереди причёску даёт сам портрет
                m.AddBoxAt(new Point3D(Math.Cos(a) * r, 0.2 + rng.NextDouble() * 0.02, -Math.Abs(Math.Sin(a)) * r - 0.01),
                    new Vector3D(0.03, 0.065 + rng.NextDouble() * 0.03, 0.02),
                    yawDeg: rng.Next(360), pitchDeg: (back ? -25 : 25) + rng.Next(-20, 20), rollDeg: rng.Next(-35, 35));
            }
            m.AddBoxAt(new Point3D(0.1, 0.11, 0.0), new Vector3D(0.025, 0.08, 0.08));
            m.AddBoxAt(new Point3D(-0.1, 0.11, 0.0), new Vector3D(0.025, 0.08, 0.08));
        }, frontThreshold: 0.55, mode: ProjectionMode.Head);

        // руки: рукава закатаны до середины предплечья, кисти и предплечья — с портрета
        foreach (var (sh, el, side) in new[] { (_shoulderL, _elbowL, 1), (_shoulderR, _elbowR, -1) })
        {
            sh.MeshProjected(skin, skin.Fabric, m =>
            {
                m.AddSphere(new Point3D(0, -0.02, 0), new Vector3D(0.06, 0.058, 0.06), 12, 7, Matrix3D.Identity);
                m.AddCylinder(new Point3D(0, -0.15, 0), 0.052, 0.3, 14, Matrix3D.Identity, true, 0.4, 0.06);
            }, mode: ProjectionMode.Arm);
            el.MeshProjected(skin, skin.Fabric, m =>
            {
                m.AddSphere(new Point3D(0, 0, 0), new Vector3D(0.058, 0.05, 0.058), 12, 5, Matrix3D.Identity);
                m.AddCylinder(new Point3D(0, -0.06, 0), 0.062, 0.09, 14);
                m.AddCylinder(new Point3D(0, -0.18, 0), 0.04, 0.2, 12, Matrix3D.Identity, true, 1, 0.05);
                m.AddBoxAt(new Point3D(0, -0.32, 0.008), new Vector3D(0.072, 0.11, 0.042));
                m.AddBoxAt(new Point3D(0.018 * side, -0.36, 0.03), new Vector3D(0.022, 0.06, 0.022), rollDeg: 15 * side);
            }, mode: ProjectionMode.Arm);

            if (side == 1)
            {
                // самодельный наручный сканер на левом предплечье (Story Bible)
                el.Mesh(Materials3D.MetalPanel(Materials3D.Hex("#35383C"), 0.1, 0.4), m =>
                    m.AddCylinder(new Point3D(0, -0.2, 0), 0.052, 0.08, 10));
                el.Mesh(Materials3D.Screen(Materials3D.Hex("#39E3D2")), m =>
                    m.AddBoxAt(new Point3D(0.048, -0.2, 0.0), new Vector3D(0.012, 0.055, 0.05)));
            }
        }
    }

    private static Matrix3D Squash(double x, double y, double z)
    {
        var m = Matrix3D.Identity;
        m.Scale(new Vector3D(x, y, z));
        return m;
    }

    private static Matrix3D Move(double x, double y, double z)
    {
        var m = Matrix3D.Identity;
        m.Translate(new Vector3D(x, y, z));
        return m;
    }

    private static Matrix3D Translation(double x, double y, double z)
    {
        var m = Matrix3D.Identity;
        m.Translate(new Vector3D(x, y, z));
        return m;
    }

    /// <summary>Мировое направление и точка луча фонаря охранника (вперёд по корпусу).</summary>
    public (Point3D origin, Vector3D dir) FlashlightRay()
    {
        var yaw = YawDegrees * Math.PI / 180;
        var dir = new Vector3D(Math.Sin(yaw), -0.25, Math.Cos(yaw));
        return (Position + new Vector3D(Math.Sin(yaw) * 0.3, 1.2 * _scale, Math.Cos(yaw) * 0.3), dir);
    }
}
