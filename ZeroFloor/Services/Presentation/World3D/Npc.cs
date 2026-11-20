using System.Windows.Media.Media3D;

namespace ZeroFloor.Services.Presentation.World3D;

/// <summary>
/// Второстепенный персонаж (торговец, прохожий, житель): та же процедурная модель, что у
/// Макса, другой внешний вид. Ходит по замкнутому маршруту или стоит у прилавка с анимацией
/// торговли/ожидания. Тело — динамический коллайдер, перекрывающий линию обзора (можно
/// укрыться «за чужой спиной», как в тексте главы), и мягко расталкивает игрока.
/// </summary>
public sealed class Npc
{
    private readonly IReadOnlyList<Point3D> _path;
    private int _target;
    private double _wait;
    private double _speed;

    public Npc(HumanoidRig rig, Point3D start, double yawDeg, IReadOnlyList<Point3D>? path = null, double speed = 1.3, bool gesture = false)
    {
        Rig = rig;
        Position = start;
        YawDeg = yawDeg;
        _path = path ?? Array.Empty<Point3D>();
        _speed = speed;
        Gesture = gesture;
        SightBlocker = new Collider(new Point3D(), new Point3D(), ColliderFlags.BlocksSight, "npc");
        SyncCollider();
        rig.Position = start;
        rig.YawDegrees = yawDeg;
    }

    public HumanoidRig Rig { get; }
    public Point3D Position { get; private set; }
    public double YawDeg { get; private set; }
    public bool Gesture { get; set; }
    public Collider SightBlocker { get; }
    public double CurrentSpeed { get; private set; }

    public void Update(double dt, Point3D playerPos)
    {
        CurrentSpeed = 0;
        if (_path.Count > 1)
        {
            if (_wait > 0)
            {
                _wait -= dt;
            }
            else
            {
                var target = _path[_target];
                var to = new Vector3D(target.X - Position.X, 0, target.Z - Position.Z);
                var dist = to.Length;

                // не наступать на Макса: притормозить, если он прямо по курсу
                var toPlayer = new Vector3D(playerPos.X - Position.X, 0, playerPos.Z - Position.Z);
                var blocked = toPlayer.Length < 0.9 && dist > 0.01 && Vector3D.DotProduct(toPlayer, to) / dist > 0.3;

                if (dist < 0.15)
                {
                    _target = (_target + 1) % _path.Count;
                    if (_target == 0) _wait = 1.5;
                }
                else if (blocked)
                {
                    // уступить дорогу: шаг в сторону от Макса, не стоять стеной
                    var side = new Vector3D(-to.Z, 0, to.X);
                    side /= side.Length;
                    if (Vector3D.DotProduct(side, toPlayer) > 0) side = -side;
                    Position += side * 0.9 * dt;
                    CurrentSpeed = 0.9;
                }
                else
                {
                    to /= dist;
                    var step = Math.Min(dist, _speed * dt);
                    Position += to * step;
                    CurrentSpeed = _speed;
                    var targetYaw = Math.Atan2(to.X, to.Z) * 180 / Math.PI;
                    YawDeg += Math.IEEERemainder(targetYaw - YawDeg, 360) * Math.Min(1, dt * 6);
                }
            }
        }

        Rig.Position = Position;
        Rig.YawDegrees = YawDeg;
        Rig.Update(dt, new LocomotionState { Speed = CurrentSpeed, RunSpeed = 6, Grounded = true, Gesture = Gesture });
        SyncCollider();
    }

    /// <summary>Не даёт игроку пройти сквозь персонажа.</summary>
    public void PushPlayer(PlayerController player)
    {
        var p = player.Position;
        var dx = p.X - Position.X;
        var dz = p.Z - Position.Z;
        var d = Math.Sqrt(dx * dx + dz * dz);
        const double min = 0.62;
        if (d < min && d > 1e-4 && Math.Abs(p.Y - Position.Y) < 1.2)
        {
            var push = (min - d) / d;
            player.Position = new Point3D(p.X + dx * push, p.Y, p.Z + dz * push);
        }
    }

    private void SyncCollider()
    {
        SightBlocker.Min = new Point3D(Position.X - 0.22, Position.Y, Position.Z - 0.22);
        SightBlocker.Max = new Point3D(Position.X + 0.22, Position.Y + 1.75 * Rig.Look.Height, Position.Z + 0.22);
    }
}
