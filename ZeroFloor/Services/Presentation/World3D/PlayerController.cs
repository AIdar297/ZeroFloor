using System.Windows.Media.Media3D;

namespace ZeroFloor.Services.Presentation.World3D;

/// <summary>
/// Кинематический контроллер Макса: движение относительно камеры (WASD), бег (Shift), прыжок
/// (Space, с «койот-таймом» и буфером нажатия), присед/крадущийся шаг (C/Ctrl), скольжение
/// (присед на бегу — для погони), гравитация, ступени, столкновения с геометрией, плавный
/// поворот корпуса к направлению движения. Оглушение/потеря равновесия временно ослабляют
/// управление — но никогда не отключают его надолго (ошибка не блокирует прохождение).
/// </summary>
public sealed class PlayerController
{
    private readonly CollisionWorld _world;
    private double _coyote;
    private double _jumpBuffer;
    private double _slideTimer;
    private Vector3D _slideDir;
    private double _stepAccumulator;

    public PlayerController(CollisionWorld world, Point3D start, double yawRad)
    {
        _world = world;
        Position = start;
        Yaw = yawRad;
        Grounded = true;
    }

    // параметры
    public double WalkSpeed { get; set; } = 3.0;
    public double RunSpeed { get; set; } = 6.0;
    public double CrouchSpeed { get; set; } = 1.7;
    public double Acceleration { get; set; } = 16;
    public double AirControl { get; set; } = 0.35;
    public double Gravity { get; set; } = 22;
    public double JumpVelocity { get; set; } = 6.7;
    public double Radius { get; set; } = 0.3;
    public double StandHeight { get; set; } = 1.78;
    public double CrouchHeight { get; set; } = 1.05;
    public double StepUp { get; set; } = 0.38;
    public bool SlideEnabled { get; set; }
    /// <summary>Принудительный присед (низкий вентиляционный короб: пригибаться автоматически).</summary>
    public bool ForceCrouch { get; set; }

    // состояние
    public Point3D Position { get; set; }
    public Vector3D Velocity { get; set; }
    public double Yaw { get; set; }
    public bool Grounded { get; private set; }
    public bool Crouching { get; private set; }
    public bool Sliding => _slideTimer > 0;
    public bool Running { get; private set; }
    public double Stun { get; set; }
    public double Stumble { get; set; }
    public double HitTimer { get; set; }
    public double LandImpact { get; private set; }
    public bool Frozen { get; set; }
    public double SpeedMultiplier { get; set; } = 1.0;
    public double LastGroundY { get; private set; }
    public bool BumpedThisFrame { get; private set; }

    public double HorizontalSpeed => Math.Sqrt(Velocity.X * Velocity.X + Velocity.Z * Velocity.Z);
    public double CurrentHeight => Crouching || Sliding ? CrouchHeight : StandHeight;

    /// <summary>Шаг ноги (для звука шагов) — срабатывает каждые ~полшага.</summary>
    public event Action<bool>? Footstep;
    public event Action<double>? Landed;
    public event Action? Jumped;

    public void Teleport(Point3D p, double yaw)
    {
        Position = p;
        Yaw = yaw;
        Velocity = new Vector3D();
        Grounded = true;
        _slideTimer = 0;
    }

    public void Update(double dt, EpisodeInput input, Vector3D camForward, Vector3D camRight, bool allowInput = true)
    {
        BumpedThisFrame = false;
        Stun = Math.Max(0, Stun - dt);
        Stumble = Math.Max(0, Stumble - dt);
        HitTimer = Math.Max(0, HitTimer - dt);
        LandImpact = Math.Max(0, LandImpact - dt * 3.5);

        var control = allowInput && !Frozen;
        var moveX = control ? input.MoveX : 0;
        var moveY = control ? input.MoveY : 0;
        var wish = camForward * moveY + camRight * moveX;
        if (wish.LengthSquared > 1) wish.Normalize();

        // присед/скольжение
        var wantsCrouch = (control && input.Crouch) || ForceCrouch;
        if (SlideEnabled && control && input.CrouchPressed && Grounded && HorizontalSpeed > RunSpeed * 0.7 && !Sliding)
        {
            _slideTimer = 0.75;
            _slideDir = new Vector3D(Velocity.X, 0, Velocity.Z);
            _slideDir.Normalize();
        }

        if (wantsCrouch)
            Crouching = true;
        else if (Crouching)
        {
            // встать можно только если над головой есть место
            var ceiling = _world.CeilingHeight(Position.X, Position.Z, Radius, Position.Y + 0.05);
            if (ceiling - Position.Y >= StandHeight)
                Crouching = false;
        }
        if (Sliding)
        {
            _slideTimer -= dt;
            if (_slideTimer <= 0)
            {
                var ceiling = _world.CeilingHeight(Position.X, Position.Z, Radius, Position.Y + 0.05);
                if (ceiling - Position.Y < StandHeight)
                {
                    Crouching = true;
                    _slideTimer = 0;
                }
            }
        }

        Running = control && input.Sprint && !Crouching && wish.LengthSquared > 0.01;
        var targetSpeed = (Crouching ? CrouchSpeed : Running ? RunSpeed : WalkSpeed) * SpeedMultiplier;
        if (Stumble > 0) targetSpeed *= 0.55;
        if (Stun > 0) targetSpeed *= 0.25;

        var vx = Velocity.X;
        var vz = Velocity.Z;
        if (Sliding)
        {
            var slideSpeed = Math.Max(RunSpeed * 0.9 * (0.5 + _slideTimer / 1.5), 2);
            vx = _slideDir.X * slideSpeed;
            vz = _slideDir.Z * slideSpeed;
        }
        else
        {
            var tx = wish.X * targetSpeed;
            var tz = wish.Z * targetSpeed;
            var accel = Grounded ? Acceleration : Acceleration * AirControl;
            var decelBoost = wish.LengthSquared < 0.01 && Grounded ? 1.4 : 1.0;
            var k = 1 - Math.Exp(-accel * decelBoost * dt / Math.Max(1, targetSpeed) * 2);
            vx += (tx - vx) * k;
            vz += (tz - vz) * k;
        }

        // поворот корпуса к направлению движения
        if (wish.LengthSquared > 0.01 && !Sliding)
        {
            var targetYaw = Math.Atan2(wish.X, wish.Z);
            var diff = Math.IEEERemainder(targetYaw - Yaw, Math.PI * 2);
            Yaw += diff * (1 - Math.Exp(-(Grounded ? 12 : 5) * dt));
        }

        // прыжок
        _coyote = Grounded ? 0.12 : Math.Max(0, _coyote - dt);
        _jumpBuffer = control && input.JumpPressed ? 0.15 : Math.Max(0, _jumpBuffer - dt);
        var vy = Velocity.Y;
        if (_jumpBuffer > 0 && _coyote > 0 && Stun <= 0 && !Crouching)
        {
            vy = JumpVelocity;
            _jumpBuffer = 0;
            _coyote = 0;
            _slideTimer = 0;
            Grounded = false;
            Jumped?.Invoke();
        }

        vy -= Gravity * dt;
        if (vy < -30) vy = -30;

        // интегрирование с подшагами (быстрый бег не проходит сквозь тонкие стены)
        var p = Position;
        var steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(vx * vx + vz * vz) * dt / (Radius * 0.5)));
        var sdt = dt / steps;
        var height = CurrentHeight;
        for (var s = 0; s < steps; s++)
        {
            var x = p.X + vx * sdt;
            var z = p.Z + vz * sdt;
            var ox = x; var oz = z;
            if (_world.ResolveHorizontal(ref x, ref z, Radius, p.Y, height, Grounded ? StepUp : 0.05))
            {
                BumpedThisFrame = true;
                // гасим скорость в направлении удара
                var nx = x - ox; var nz = z - oz;
                var nl = Math.Sqrt(nx * nx + nz * nz);
                if (nl > 1e-6)
                {
                    nx /= nl; nz /= nl;
                    var dot = vx * nx + vz * nz;
                    if (dot < 0) { vx -= dot * nx; vz -= dot * nz; }
                }
            }
            p = new Point3D(x, p.Y, z);
        }

        // вертикаль
        var newY = p.Y + vy * dt;
        if (vy > 0)
        {
            var ceiling = _world.CeilingHeight(p.X, p.Z, Radius, p.Y + 0.05);
            if (newY + height > ceiling)
            {
                newY = Math.Max(p.Y, ceiling - height);
                vy = 0;
            }
        }

        var ground = _world.GroundHeight(p.X, p.Z, Radius * 0.35, Math.Max(p.Y, newY), Grounded ? StepUp : 0.05);
        var wasGrounded = Grounded;
        if (newY <= ground + 0.001 && vy <= 0)
        {
            if (!wasGrounded)
            {
                var impact = Math.Clamp(-Velocity.Y / 12, 0, 1);
                LandImpact = Math.Max(LandImpact, 0.35 + impact * 0.65);
                Landed?.Invoke(-Velocity.Y);
            }
            newY = ground;
            vy = 0;
            Grounded = true;
            LastGroundY = ground;
        }
        else if (wasGrounded && vy <= 0 && ground > double.NegativeInfinity && p.Y - ground < 0.42)
        {
            // спуск по ступеням: прилипаем к опоре
            newY = ground;
            vy = 0;
            Grounded = true;
            LastGroundY = ground;
        }
        else
        {
            Grounded = false;
        }

        Position = new Point3D(p.X, newY, p.Z);
        Velocity = new Vector3D(vx, vy, vz);

        // шаги
        if (Grounded && HorizontalSpeed > 0.5)
        {
            _stepAccumulator += HorizontalSpeed * dt;
            var stride = Running ? 1.05 : Crouching ? 0.6 : 0.8;
            if (_stepAccumulator >= stride)
            {
                _stepAccumulator = 0;
                Footstep?.Invoke(Running);
            }
        }
    }

    public LocomotionState Locomotion(bool breathless = false, bool hidden = false) => new()
    {
        Speed = HorizontalSpeed,
        RunSpeed = RunSpeed,
        Grounded = Grounded,
        VerticalVelocity = Velocity.Y,
        Crouch = Crouching && !Sliding ? 1 : 0,
        Sliding = Sliding,
        Stumble = Math.Clamp(Stumble / 0.8, 0, 1),
        Hit = Math.Clamp(HitTimer / 0.35, 0, 1),
        Land = LandImpact,
        Breathless = breathless,
        Hidden = hidden
    };

    /// <summary>Ошибка игрока (удар, падение, захват): короткое оглушение + потеря равновесия.</summary>
    public void TakeHit(Vector3D knock, double stun = 0.35, double stumble = 0.9)
    {
        HitTimer = 0.35;
        Stun = Math.Max(Stun, stun);
        Stumble = Math.Max(Stumble, stumble);
        Velocity = new Vector3D(Velocity.X * 0.3 + knock.X, Math.Max(Velocity.Y, knock.Y), Velocity.Z * 0.3 + knock.Z);
        _slideTimer = 0;
    }
}
