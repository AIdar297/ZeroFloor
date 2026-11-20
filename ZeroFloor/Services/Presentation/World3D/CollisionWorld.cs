using System.Windows.Media.Media3D;

namespace ZeroFloor.Services.Presentation.World3D;

[Flags]
public enum ColliderFlags
{
    None = 0,

    /// <summary>Блокирует движение персонажей по горизонтали (стены, ящики, перила).</summary>
    Solid = 1,

    /// <summary>Верхняя грань — опора (пол, платформы, ступени, крышки ящиков).</summary>
    Walkable = 2,

    /// <summary>Перекрывает линию обзора (дрон, патруль) — укрытия обязаны иметь этот флаг.</summary>
    BlocksSight = 4,

    /// <summary>Камера не проходит сквозь (стены, крупные конструкции).</summary>
    BlocksCamera = 8,

    Wall = Solid | BlocksSight | BlocksCamera,
    Floor = Walkable | Solid | BlocksCamera,
    Block = Solid | Walkable | BlocksSight | BlocksCamera
}

/// <summary>Осевой бокс столкновений. Изменяемый: динамические объекты (обрушающийся настил,
/// упавшая балка, дверь, опущенный брезент) двигают/включают свой коллайдер по ходу эпизода.</summary>
public sealed class Collider
{
    public Collider(Point3D min, Point3D max, ColliderFlags flags, string? tag = null)
    {
        Min = min;
        Max = max;
        Flags = flags;
        Tag = tag;
    }

    public Point3D Min { get; set; }
    public Point3D Max { get; set; }
    public ColliderFlags Flags { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Tag { get; }

    public bool Has(ColliderFlags flag) => Enabled && (Flags & flag) != 0;

    public void Translate(Vector3D delta)
    {
        Min += delta;
        Max += delta;
    }
}

/// <summary>
/// Физический мир эпизода: набор AABB. Персонаж — вертикальный цилиндр (radius, height) с
/// «ступенькой» stepUp: всё, что ниже ступеньки, перешагивается, выше — упирается. Простая
/// модель, но точно предсказуемая для игрока: видно глазами = работает столкновением.
/// </summary>
public sealed class CollisionWorld
{
    private readonly List<Collider> _colliders = new();

    public IReadOnlyList<Collider> Colliders => _colliders;

    public Collider Add(Point3D min, Point3D max, ColliderFlags flags, string? tag = null)
    {
        var c = new Collider(
            new Point3D(Math.Min(min.X, max.X), Math.Min(min.Y, max.Y), Math.Min(min.Z, max.Z)),
            new Point3D(Math.Max(min.X, max.X), Math.Max(min.Y, max.Y), Math.Max(min.Z, max.Z)),
            flags, tag);
        _colliders.Add(c);
        return c;
    }

    public Collider AddCentered(Point3D center, Vector3D size, ColliderFlags flags, string? tag = null) =>
        Add(new Point3D(center.X - size.X / 2, center.Y - size.Y / 2, center.Z - size.Z / 2),
            new Point3D(center.X + size.X / 2, center.Y + size.Y / 2, center.Z + size.Z / 2), flags, tag);

    /// <summary>Высота опоры под точкой: самая высокая верхняя грань Walkable-коллайдера, чья
    /// проекция содержит точку (с допуском footprint), но не выше feetY + stepUp.
    /// double.NegativeInfinity — под ногами пустота (провал).</summary>
    public double GroundHeight(double x, double z, double footprint, double feetY, double stepUp)
    {
        var best = double.NegativeInfinity;
        foreach (var c in _colliders)
        {
            if (!c.Has(ColliderFlags.Walkable))
                continue;
            if (x < c.Min.X - footprint || x > c.Max.X + footprint || z < c.Min.Z - footprint || z > c.Max.Z + footprint)
                continue;
            var top = c.Max.Y;
            if (top <= feetY + stepUp && top > best)
                best = top;
        }
        return best;
    }

    /// <summary>Выталкивает цилиндр персонажа из всех Solid-коллайдеров, которые он задевает по
    /// высоте (выше ступеньки и ниже макушки). Возвращает true, если было столкновение.</summary>
    public bool ResolveHorizontal(ref double x, ref double z, double radius, double feetY, double height, double stepUp, Collider? ignore = null)
    {
        var collided = false;
        for (var iteration = 0; iteration < 4; iteration++)
        {
            var any = false;
            foreach (var c in _colliders)
            {
                if (!c.Has(ColliderFlags.Solid) || ReferenceEquals(c, ignore))
                    continue;
                if (c.Max.Y <= feetY + stepUp || c.Min.Y >= feetY + height)
                    continue;

                var cx = Math.Clamp(x, c.Min.X, c.Max.X);
                var cz = Math.Clamp(z, c.Min.Z, c.Max.Z);
                var dx = x - cx;
                var dz = z - cz;
                var d2 = dx * dx + dz * dz;
                if (d2 >= radius * radius)
                    continue;

                if (d2 > 1e-10)
                {
                    var d = Math.Sqrt(d2);
                    var push = radius - d;
                    x += dx / d * push;
                    z += dz / d * push;
                }
                else
                {
                    // центр внутри бокса — выталкиваем по кратчайшей оси
                    var left = x - c.Min.X; var right = c.Max.X - x;
                    var back = z - c.Min.Z; var front = c.Max.Z - z;
                    var m = Math.Min(Math.Min(left, right), Math.Min(back, front));
                    if (m == left) x = c.Min.X - radius;
                    else if (m == right) x = c.Max.X + radius;
                    else if (m == back) z = c.Min.Z - radius;
                    else z = c.Max.Z + radius;
                }
                any = true;
                collided = true;
            }
            if (!any)
                break;
        }
        return collided;
    }

    /// <summary>Есть ли что-то над головой (для прыжка под низкой балкой и выхода из приседа).</summary>
    public double CeilingHeight(double x, double z, double radius, double fromY)
    {
        var best = double.PositiveInfinity;
        foreach (var c in _colliders)
        {
            if (!c.Has(ColliderFlags.Solid))
                continue;
            if (c.Min.Y < fromY - 0.01)
                continue;
            var cx = Math.Clamp(x, c.Min.X, c.Max.X);
            var cz = Math.Clamp(z, c.Min.Z, c.Max.Z);
            if ((x - cx) * (x - cx) + (z - cz) * (z - cz) >= radius * radius * 0.6)
                continue;
            if (c.Min.Y < best)
                best = c.Min.Y;
        }
        return best;
    }

    /// <summary>Луч против коллайдеров с маской; возвращает расстояние до ближайшего попадания
    /// или maxDistance, если луч свободен.</summary>
    public double Raycast(Point3D origin, Vector3D direction, double maxDistance, ColliderFlags mask, IReadOnlyList<Collider>? extra = null)
    {
        direction.Normalize();
        var best = maxDistance;
        foreach (var c in _colliders)
            TestRay(c, origin, direction, mask, ref best);
        if (extra is not null)
            foreach (var c in extra)
                TestRay(c, origin, direction, mask, ref best);
        return best;
    }

    public bool HasLineOfSight(Point3D from, Point3D to, IReadOnlyList<Collider>? extra = null)
    {
        var dir = to - from;
        var dist = dir.Length;
        if (dist < 1e-6)
            return true;
        return Raycast(from, dir, dist, ColliderFlags.BlocksSight, extra) >= dist - 0.05;
    }

    private static void TestRay(Collider c, Point3D o, Vector3D d, ColliderFlags mask, ref double best)
    {
        if (!c.Enabled || (c.Flags & mask) == 0)
            return;

        double tmin = 0, tmax = best;
        if (!Slab(o.X, d.X, c.Min.X, c.Max.X, ref tmin, ref tmax)) return;
        if (!Slab(o.Y, d.Y, c.Min.Y, c.Max.Y, ref tmin, ref tmax)) return;
        if (!Slab(o.Z, d.Z, c.Min.Z, c.Max.Z, ref tmin, ref tmax)) return;

        // старт внутри бокса не считаем попаданием (камера/сенсор может касаться геометрии)
        if (tmin <= 1e-6)
            return;
        if (tmin < best)
            best = tmin;
    }

    private static bool Slab(double o, double d, double min, double max, ref double tmin, ref double tmax)
    {
        if (Math.Abs(d) < 1e-12)
            return o >= min && o <= max;
        var t1 = (min - o) / d;
        var t2 = (max - o) / d;
        if (t1 > t2) (t1, t2) = (t2, t1);
        tmin = Math.Max(tmin, t1);
        tmax = Math.Min(tmax, t2);
        return tmin <= tmax;
    }
}
