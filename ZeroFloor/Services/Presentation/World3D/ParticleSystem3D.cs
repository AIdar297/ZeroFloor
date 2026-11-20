using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ZeroFloor.Services.Presentation.World3D;

/// <summary>
/// Билборд-частицы (искры, пар, дым, пыль). Одна система = несколько GeometryModel3D по
/// «корзинам» прозрачности (WPF не умеет альфу на вершину), меши пересобираются каждый кадр,
/// квады сортируются от дальних к ближним — иначе запись глубины полупрозрачных квадов даёт
/// квадратные артефакты.
/// </summary>
public sealed class ParticleSystem3D
{
    private sealed class Particle
    {
        public Point3D Pos;
        public Vector3D Vel;
        public double Life;
        public double MaxLife;
        public double Size;
        public double Growth;
        public double Gravity;
        public double Drag;
        public double Stretch;
    }

    private const int Buckets = 4;
    private readonly List<Particle> _particles = new();
    private readonly GeometryModel3D[] _models = new GeometryModel3D[Buckets];
    private readonly int _max;
    private readonly bool _fadeIn;

    public ParticleSystem3D(Func<double, Material> materialForAlpha, int max = 160, bool fadeIn = false)
    {
        _max = max;
        _fadeIn = fadeIn;
        Root = new Model3DGroup();
        for (var i = 0; i < Buckets; i++)
        {
            var alpha = 1.0 - i / (double)Buckets;
            _models[i] = new GeometryModel3D(new MeshGeometry3D(), materialForAlpha(alpha));
            Root.Children.Add(_models[i]);
        }
    }

    public Model3DGroup Root { get; }
    public int Count => _particles.Count;

    public void Emit(Point3D pos, Vector3D vel, double life, double size, double growth = 0, double gravity = 0, double drag = 0, double stretch = 0)
    {
        if (_particles.Count >= _max)
            _particles.RemoveAt(0);
        _particles.Add(new Particle
        {
            Pos = pos, Vel = vel, Life = life, MaxLife = life, Size = size, Growth = growth, Gravity = gravity, Drag = drag, Stretch = stretch
        });
    }

    public void Update(double dt, Point3D cameraPos, Vector3D cameraLook)
    {
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life -= dt;
            if (p.Life <= 0)
            {
                _particles.RemoveAt(i);
                continue;
            }
            p.Vel = p.Vel * Math.Max(0, 1 - p.Drag * dt) + new Vector3D(0, -p.Gravity * dt, 0);
            p.Pos += p.Vel * dt;
            p.Size += p.Growth * dt;
        }

        var right = Vector3D.CrossProduct(cameraLook, new Vector3D(0, 1, 0));
        if (right.LengthSquared < 1e-8) right = new Vector3D(1, 0, 0);
        right.Normalize();
        var up = Vector3D.CrossProduct(right, cameraLook);
        up.Normalize();

        var sorted = _particles
            .Select(p => (p, d: (p.Pos - cameraPos).LengthSquared))
            .OrderByDescending(t => t.d)
            .ToList();

        var builders = new (Point3DCollection pos, PointCollection uv, Int32Collection idx)[Buckets];
        for (var i = 0; i < Buckets; i++)
            builders[i] = (new Point3DCollection(), new PointCollection(), new Int32Collection());

        foreach (var (p, _) in sorted)
        {
            var t = p.Life / p.MaxLife; // 1 → 0
            var alpha = _fadeIn ? Math.Min(t, (1 - t) * 4) : t;
            var bucket = Math.Clamp((int)((1 - alpha) * Buckets), 0, Buckets - 1);
            var b = builders[bucket];
            var hs = p.Size / 2;
            var r = right * hs;
            var u = up * hs;
            if (p.Stretch > 0 && p.Vel.LengthSquared > 1e-4)
            {
                var v = p.Vel;
                v.Normalize();
                u = v * (hs + p.Vel.Length * p.Stretch);
                r = Vector3D.CrossProduct(v, cameraLook);
                if (r.LengthSquared < 1e-8) r = right;
                r.Normalize();
                r *= hs * 0.5;
            }
            var baseIndex = b.pos.Count;
            b.pos.Add(p.Pos - r - u);
            b.pos.Add(p.Pos + r - u);
            b.pos.Add(p.Pos + r + u);
            b.pos.Add(p.Pos - r + u);
            b.uv.Add(new Point(0, 1)); b.uv.Add(new Point(1, 1)); b.uv.Add(new Point(1, 0)); b.uv.Add(new Point(0, 0));
            b.idx.Add(baseIndex); b.idx.Add(baseIndex + 1); b.idx.Add(baseIndex + 2);
            b.idx.Add(baseIndex); b.idx.Add(baseIndex + 2); b.idx.Add(baseIndex + 3);
        }

        for (var i = 0; i < Buckets; i++)
        {
            var mesh = new MeshGeometry3D
            {
                Positions = builders[i].pos,
                TextureCoordinates = builders[i].uv,
                TriangleIndices = builders[i].idx
            };
            _models[i].Geometry = mesh;
            _models[i].BackMaterial = _models[i].Material;
        }
    }
}
