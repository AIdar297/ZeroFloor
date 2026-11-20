using System.Windows.Media.Media3D;

namespace ZeroFloor.Services.Presentation.World3D;

/// <summary>
/// Камера от третьего лица: орбита вокруг точки над плечами Макса, мышью меняются рыскание и
/// наклон. Не проходит сквозь стены (луч от опорной точки к желаемой позиции против
/// BlocksCamera-коллайдеров), плавно следует за целью, поддерживает лёгкую тряску («травма»
/// с затуханием) и кинематографический режим для вступления/финала эпизода.
/// </summary>
public sealed class CameraRig
{
    private Point3D _pivot;
    private double _currentDistance;
    private double _trauma;
    private double _time;
    private bool _initialized;
    private Point3D? _cinePos;
    private Point3D _cineLook;
    private double _cineBlend;
    private double _cineTarget;
    private double _cineRate = 1.5;

    public CameraRig()
    {
        Camera = new PerspectiveCamera
        {
            FieldOfView = 70,
            NearPlaneDistance = 0.05,
            FarPlaneDistance = 400,
            UpDirection = new Vector3D(0, 1, 0)
        };
    }

    public PerspectiveCamera Camera { get; }

    /// <summary>Рыскание в радианах: направление взгляда (sin, 0, cos).</summary>
    public double Yaw { get; set; }
    public double Pitch { get; set; } = 0.22;
    public double Distance { get; set; } = 3.6;
    public double PivotHeight { get; set; } = 1.62;
    public double ShoulderOffset { get; set; } = 0.35;
    public double MinPitch { get; set; } = -0.35;
    public double MaxPitch { get; set; } = 0.95;
    public double ShakeScale { get; set; } = 1.0;
    public double FollowSharpness { get; set; } = 14;
    public double BaseFov { get; set; } = 70;
    public double FovKick { get; set; }

    public Vector3D Forward => new(Math.Sin(Yaw), 0, Math.Cos(Yaw));
    public Vector3D Right => new(-Math.Cos(Yaw), 0, Math.Sin(Yaw));
    public Point3D Position => Camera.Position;

    public void AddTrauma(double amount) => _trauma = Math.Min(1, _trauma + amount);

    public void Rotate(double dYaw, double dPitch)
    {
        Yaw += dYaw;
        Pitch = Math.Clamp(Pitch + dPitch, MinPitch, MaxPitch);
    }

    /// <summary>Мягко повернуть рыскание к направлению (погоня: камера сама «доворачивает» за бегом).</summary>
    public void EaseYawTowards(double targetYaw, double rate, double dt)
    {
        var diff = Math.IEEERemainder(targetYaw - Yaw, Math.PI * 2);
        Yaw += diff * (1 - Math.Exp(-rate * dt));
    }

    /// <summary>Кинематографический кадр: камера плавно уходит в заданную точку (blend 0..1).</summary>
    public void SetCinematic(Point3D position, Point3D lookAt, double rate = 1.5)
    {
        _cinePos = position;
        _cineLook = lookAt;
        _cineTarget = 1;
        _cineRate = rate;
    }

    public void ReleaseCinematic(double rate = 1.5)
    {
        _cineTarget = 0;
        _cineRate = rate;
    }

    public void SnapCinematic(Point3D position, Point3D lookAt)
    {
        _cinePos = position;
        _cineLook = lookAt;
        _cineBlend = _cineTarget = 1;
    }

    public void Update(double dt, Point3D target, CollisionWorld world, double pivotHeight = double.NaN)
    {
        _time += dt;
        var ph = double.IsNaN(pivotHeight) ? PivotHeight : pivotHeight;
        var desiredPivot = target + new Vector3D(0, ph, 0) + Right * ShoulderOffset;

        if (!_initialized)
        {
            _pivot = desiredPivot;
            _currentDistance = Distance;
            _initialized = true;
        }

        var k = 1 - Math.Exp(-FollowSharpness * dt);
        _pivot = new Point3D(
            _pivot.X + (desiredPivot.X - _pivot.X) * k,
            _pivot.Y + (desiredPivot.Y - _pivot.Y) * (1 - Math.Exp(-8 * dt)),
            _pivot.Z + (desiredPivot.Z - _pivot.Z) * k);

        var back = new Vector3D(-Math.Sin(Yaw) * Math.Cos(Pitch), Math.Sin(Pitch), -Math.Cos(Yaw) * Math.Cos(Pitch));
        var hit = world.Raycast(_pivot, back, Distance + 0.3, ColliderFlags.BlocksCamera);
        var allowed = Math.Max(0.35, hit - 0.3);
        var wanted = Math.Min(Distance, allowed);
        // в стену — мгновенно, обратно — плавно
        _currentDistance = wanted < _currentDistance ? wanted : _currentDistance + (wanted - _currentDistance) * (1 - Math.Exp(-4 * dt));

        var pos = _pivot + back * _currentDistance;
        var look = _pivot - pos;

        // тряска (шум по нескольким синусам)
        var shake = _trauma * _trauma * ShakeScale;
        _trauma = Math.Max(0, _trauma - dt * 1.4);
        if (shake > 0)
        {
            pos += new Vector3D(
                Math.Sin(_time * 37.1) * 0.06 * shake,
                Math.Sin(_time * 41.7 + 1.3) * 0.05 * shake,
                Math.Sin(_time * 29.3 + 2.1) * 0.06 * shake);
        }

        _cineBlend += (_cineTarget - _cineBlend) * (1 - Math.Exp(-_cineRate * dt));
        if (_cinePos is { } cp && _cineBlend > 0.001)
        {
            var b = Smooth(_cineBlend);
            pos = Lerp(pos, cp, b);
            var lookTarget = Lerp(_pivot, _cineLook, b);
            look = lookTarget - pos;
        }

        if (look.LengthSquared < 1e-8)
            look = new Vector3D(0, 0, 1);

        Camera.Position = pos;
        Camera.LookDirection = look;
        Camera.FieldOfView = BaseFov + FovKick;
    }

    private static double Smooth(double t) => t * t * (3 - 2 * t);

    private static Point3D Lerp(Point3D a, Point3D b, double t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
}
