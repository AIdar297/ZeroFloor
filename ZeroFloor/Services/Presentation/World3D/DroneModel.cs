using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ZeroFloor.Services.Presentation.World3D;

/// <summary>
/// Патрульный дрон Администрации: обтекаемый корпус, четыре луча с винтами в кольцах,
/// поворотная сенсорная голова с красным объективом, синие маячки (тревога — красный строб),
/// красный полупрозрачный конус сканирования, пятно сканирования на полу и красный
/// прожектор-SpotLight. Модель — в Root (непрозрачный слой), конус и пятно — в TransparentRoot
/// (рисуется после всего непрозрачного).
/// </summary>
public sealed class DroneModel
{
    private readonly AxisAngleRotation3D _yaw = new(new Vector3D(0, 1, 0), 0);
    private readonly AxisAngleRotation3D _tilt = new(new Vector3D(1, 0, 0), 0);
    private readonly AxisAngleRotation3D _bank = new(new Vector3D(0, 0, 1), 0);
    private readonly TranslateTransform3D _translate = new();
    private readonly AxisAngleRotation3D _sensorYaw = new(new Vector3D(0, 1, 0), 0);
    private readonly AxisAngleRotation3D _sensorPitch = new(new Vector3D(1, 0, 0), 0);
    private readonly List<AxisAngleRotation3D> _rotors = new();
    private readonly GeometryModel3D _beaconsBlue;
    private readonly GeometryModel3D _beaconsRed;
    private readonly MatrixTransform3D _coneTransform = new();
    private readonly MatrixTransform3D _spotTransform = new();
    private readonly GeometryModel3D _cone;
    private readonly GeometryModel3D _floorSpot;
    private readonly Material _coneCalm;
    private readonly Material _coneAlert;
    private double _time;

    public DroneModel(double range, double halfAngleDeg)
    {
        Range = range;
        HalfAngleDeg = halfAngleDeg;

        var body = new Model3DGroup();
        var shell = Materials3D.MetalPanel(Materials3D.Hex("#C9CDD2"), 0.02, 0.6);
        var dark = Materials3D.Matte(Materials3D.Hex("#202328"), 0.4, 40);
        var trim = Materials3D.Matte(Materials3D.Hex("#E2A33A"), 0.3, 30);

        Add(body, shell, m =>
        {
            m.AddSphere(new Point3D(0, 0, 0), new Vector3D(0.42, 0.16, 0.55), 12, 6, Matrix3D.Identity);
            m.AddBox(new Point3D(0, 0.1, -0.1), new Vector3D(0.3, 0.1, 0.4));
        });
        Add(body, dark, m =>
        {
            m.AddBox(new Point3D(0, -0.1, 0), new Vector3D(0.5, 0.06, 0.6));
            // лучи к винтам
            foreach (var (x, z) in Arms)
                m.AddBeam(new Point3D(0, 0, 0), new Point3D(x, 0.04, z), 0.07, 0.05);
            // ножки-антенны
            m.AddBeam(new Point3D(0.2, -0.12, 0.25), new Point3D(0.25, -0.32, 0.3), 0.03, 0.03);
            m.AddBeam(new Point3D(-0.2, -0.12, 0.25), new Point3D(-0.25, -0.32, 0.3), 0.03, 0.03);
            m.AddBeam(new Point3D(0.2, -0.12, -0.25), new Point3D(0.25, -0.32, -0.3), 0.03, 0.03);
            m.AddBeam(new Point3D(-0.2, -0.12, -0.25), new Point3D(-0.25, -0.32, -0.3), 0.03, 0.03);
        });
        Add(body, trim, m => m.AddBox(new Point3D(0, 0.02, 0.5), new Vector3D(0.3, 0.04, 0.06)));

        foreach (var (x, z) in Arms)
        {
            Add(body, dark, m => m.AddCylinder(new Point3D(x, 0.06, z), 0.32, 0.08, 14, Matrix3D.Identity, false));
            Add(body, shell, m => m.AddCylinder(new Point3D(x, 0.08, z), 0.06, 0.12, 8));
            var spin = new AxisAngleRotation3D(new Vector3D(0, 1, 0), x * z > 0 ? 0 : 45);
            _rotors.Add(spin);
            var bladeMb = new MeshBuilder();
            bladeMb.AddBox(new Point3D(0, 0, 0), new Vector3D(0.56, 0.01, 0.05));
            bladeMb.AddBox(new Point3D(0, 0, 0), new Vector3D(0.05, 0.01, 0.56));
            var blade = new GeometryModel3D(bladeMb.Build(), Materials3D.Matte(Materials3D.Hex("#3C4046")));
            var tg = new Transform3DGroup();
            tg.Children.Add(new RotateTransform3D(spin));
            tg.Children.Add(new TranslateTransform3D(x, 0.14, z));
            blade.Transform = tg;
            body.Children.Add(blade);
        }

        var beaconMb = new MeshBuilder();
        foreach (var (x, z) in Arms)
            beaconMb.AddSphere(new Point3D(x * 1.35, 0.06, z * 1.35), 0.04, 6, 4);
        beaconMb.AddSphere(new Point3D(0, 0.17, -0.2), 0.05, 6, 4);
        var beaconMesh = beaconMb.Build();
        _beaconsBlue = new GeometryModel3D(beaconMesh, Materials3D.Emissive(Materials3D.Hex("#3D8BFF"), 1.2));
        _beaconsRed = new GeometryModel3D(beaconMesh, Materials3D.Emissive(Materials3D.Hex("#FF2A2A"), 1.2));
        body.Children.Add(_beaconsBlue);

        // сенсорная голова
        var sensor = new Model3DGroup();
        Add(sensor, dark, m =>
        {
            m.AddSphere(new Point3D(0, 0, 0), new Vector3D(0.15, 0.13, 0.15), 10, 6, Matrix3D.Identity);
            m.AddCylinder(new Point3D(0, 0, 0), 0.075, 0.08, 10, MeshBuilder.Trs(new Vector3D(0, 0, 0.13), pitchDeg: 90), true);
        });
        Add(sensor, Materials3D.Emissive(Materials3D.Hex("#FF2A2A"), 1.4), m =>
            m.AddCylinder(new Point3D(0, 0, 0), 0.05, 0.02, 10, MeshBuilder.Trs(new Vector3D(0, 0, 0.18), pitchDeg: 90), true));
        var sensorTg = new Transform3DGroup();
        sensorTg.Children.Add(new RotateTransform3D(_sensorPitch));
        sensorTg.Children.Add(new RotateTransform3D(_sensorYaw));
        sensorTg.Children.Add(new TranslateTransform3D(0, -0.2, 0.28));
        sensor.Transform = sensorTg;
        body.Children.Add(sensor);

        var bodyTg = new Transform3DGroup();
        bodyTg.Children.Add(new RotateTransform3D(_tilt));
        bodyTg.Children.Add(new RotateTransform3D(_bank));
        bodyTg.Children.Add(new RotateTransform3D(_yaw));
        bodyTg.Children.Add(_translate);
        body.Transform = bodyTg;
        Root = body;

        // конус обзора: ось +Y локально, длина = дальность, радиус = tan(угла)*дальность
        _coneCalm = Materials3D.Glow(Materials3D.Hex("#FF3B30"), 0.13);
        _coneAlert = Materials3D.Glow(Materials3D.Hex("#FF1E1E"), 0.26);
        var coneMb = new MeshBuilder();
        coneMb.AddCone(new Point3D(0, 0, 0), new Vector3D(0, 1, 0), range, Math.Tan(halfAngleDeg * Math.PI / 180) * range, 24);
        _cone = new GeometryModel3D(coneMb.Build(), _coneCalm) { BackMaterial = _coneCalm, Transform = _coneTransform };

        var spotMb = new MeshBuilder();
        spotMb.AddPanel(new Point3D(0, 0, 0), new Vector3D(1, 0, 0), new Vector3D(0, 0, -1), 1, 1, doubleSided: false);
        _floorSpot = new GeometryModel3D(spotMb.Build(), Materials3D.SoftGlow(Materials3D.Hex("#FF2E2E"), 0.55)) { Transform = _spotTransform };

        TransparentRoot = new Model3DGroup();
        TransparentRoot.Children.Add(_floorSpot);
        TransparentRoot.Children.Add(_cone);

        Light = new SpotLight(Color.FromRgb(255, 60, 50), new Point3D(), new Vector3D(0, -1, 0), halfAngleDeg * 2 + 6, halfAngleDeg * 2 - 4)
        {
            Range = range * 1.6,
            ConstantAttenuation = 0.6,
            LinearAttenuation = 0.05
        };
    }

    private static readonly (double x, double z)[] Arms = { (0.62, 0.55), (-0.62, 0.55), (0.62, -0.55), (-0.62, -0.55) };

    public Model3DGroup Root { get; }
    public Model3DGroup TransparentRoot { get; }
    public SpotLight Light { get; }
    public double Range { get; }
    public double HalfAngleDeg { get; }

    public Point3D Position { get; set; }
    public double YawDeg { get; set; }
    public double SensorYawDeg { get; set; }
    public double SensorPitchDeg { get; set; } = 45;
    public bool Alarm { get; set; }
    public bool ConeVisible { get; set; } = true;
    public double FloorY { get; set; }

    public Point3D SensorPosition =>
        Position + Rotate(new Vector3D(0, -0.2, 0.28), YawDeg);

    /// <summary>Мировое направление сенсора (суммарный поворот корпуса и головы, наклон вниз).</summary>
    public Vector3D SensorDirection
    {
        get
        {
            var yaw = (YawDeg + SensorYawDeg) * Math.PI / 180;
            var pitch = SensorPitchDeg * Math.PI / 180;
            return new Vector3D(Math.Sin(yaw) * Math.Cos(pitch), -Math.Sin(pitch), Math.Cos(yaw) * Math.Cos(pitch));
        }
    }

    public void Update(double dt, Vector3D velocity)
    {
        _time += dt;
        var bob = Math.Sin(_time * 2.1) * 0.06;
        _translate.OffsetX = Position.X;
        _translate.OffsetY = Position.Y + bob;
        _translate.OffsetZ = Position.Z;
        _yaw.Angle = YawDeg;

        // наклон в сторону движения
        var yawRad = YawDeg * Math.PI / 180;
        var fwd = velocity.X * Math.Sin(yawRad) + velocity.Z * Math.Cos(yawRad);
        var side = velocity.X * -Math.Cos(yawRad) + velocity.Z * Math.Sin(yawRad);
        _tilt.Angle += (Math.Clamp(fwd * 4, -14, 14) - _tilt.Angle) * Math.Min(1, dt * 4);
        _bank.Angle += (Math.Clamp(-side * 4, -14, 14) - _bank.Angle) * Math.Min(1, dt * 4);

        _sensorYaw.Angle = SensorYawDeg;
        _sensorPitch.Angle = SensorPitchDeg;

        foreach (var r in _rotors)
            r.Angle = (r.Angle + dt * 2400) % 360;

        // маяки: спокойно — редкое синее мигание, тревога — красный строб
        var blinkOn = Alarm ? (_time * 8 % 1) < 0.5 : (_time % 1.4) < 0.15;
        var desired = Alarm ? _beaconsRed : _beaconsBlue;
        var other = Alarm ? _beaconsBlue : _beaconsRed;
        Root.Children.Remove(other);
        if (blinkOn && !Root.Children.Contains(desired)) Root.Children.Add(desired);
        if (!blinkOn) Root.Children.Remove(desired);

        // конус и прожектор
        var origin = SensorPosition + new Vector3D(0, bob, 0);
        var dir = SensorDirection;
        var m = MeshBuilder.AlignY(dir);
        m.Translate(new Vector3D(origin.X, origin.Y, origin.Z));
        _coneTransform.Matrix = m;
        _cone.Material = Alarm ? _coneAlert : _coneCalm;
        _cone.BackMaterial = _cone.Material;
        if (!ConeVisible) _coneTransform.Matrix = new Matrix3D(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -1000, 0, 1);

        Light.Position = origin;
        Light.Direction = dir;
        Light.Color = Alarm ? Color.FromRgb(255, 30, 30) : Color.FromRgb(255, 70, 60);

        // пятно на полу — там, где ось конуса пересекает пол
        if (dir.Y < -0.05 && ConeVisible)
        {
            var t = (FloorY + 0.03 - origin.Y) / dir.Y;
            var hit = origin + dir * Math.Min(t, Range);
            var size = Math.Max(0.8, Math.Tan(HalfAngleDeg * Math.PI / 180) * t * 2.2);
            var sm = Matrix3D.Identity;
            sm.Scale(new Vector3D(size, 1, size / Math.Max(0.35, -dir.Y)));
            sm.Rotate(new Quaternion(new Vector3D(0, 1, 0), YawDeg + SensorYawDeg));
            sm.Translate(new Vector3D(hit.X, FloorY + 0.03, hit.Z));
            _spotTransform.Matrix = sm;
        }
        else
        {
            _spotTransform.Matrix = new Matrix3D(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -1000, 0, 1);
        }
    }

    private static Vector3D Rotate(Vector3D v, double yawDeg)
    {
        var a = yawDeg * Math.PI / 180;
        return new Vector3D(v.X * Math.Cos(a) + v.Z * Math.Sin(a), v.Y, -v.X * Math.Sin(a) + v.Z * Math.Cos(a));
    }

    private static Matrix3D Inverse(double x, double y, double z)
    {
        var m = Matrix3D.Identity;
        m.Translate(new Vector3D(-x, -y, -z));
        return m;
    }

    private static void Add(Model3DGroup group, Material material, Action<MeshBuilder> build)
    {
        var mb = new MeshBuilder();
        build(mb);
        var model = mb.ToModel(material);
        if (model is not null)
            group.Children.Add(model);
    }
}
