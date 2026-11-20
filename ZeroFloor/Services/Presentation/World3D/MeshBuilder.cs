using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ZeroFloor.Services.Presentation.World3D;

/// <summary>
/// Накопитель low-poly геометрии для WPF 3D: боксы, цилиндры, сферы, конусы, тесселированные
/// плоскости. Все примитивы можно добавлять с произвольной матрицей (поворот/масштаб/сдвиг),
/// поэтому статичная геометрия уровня одного материала сливается в ОДИН MeshGeometry3D —
/// это главное условие приемлемой производительности Viewport3D (каждый GeometryModel3D —
/// отдельный draw call и отдельный узел дерева рендеринга).
/// UV задаются в метрах (u = метр по поверхности / uvScale), а кисти материалов используют
/// TileMode.Tile с абсолютным Viewport 0..1 — текстура повторяется, а не растягивается.
/// </summary>
public sealed class MeshBuilder
{
    private readonly Point3DCollection _positions = new();
    private readonly Vector3DCollection _normals = new();
    private readonly PointCollection _uvs = new();
    private readonly Int32Collection _indices = new();

    public int VertexCount => _positions.Count;
    public bool IsEmpty => _indices.Count == 0;

    public MeshGeometry3D Build()
    {
        var mesh = new MeshGeometry3D
        {
            Positions = _positions.Clone(),
            Normals = _normals.Clone(),
            TextureCoordinates = _uvs.Clone(),
            TriangleIndices = _indices.Clone()
        };
        mesh.Freeze();
        return mesh;
    }

    /// <summary>Готовая модель из накопленной геометрии; null — если ничего не добавлено.</summary>
    public GeometryModel3D? ToModel(Material material, Material? backMaterial = null)
    {
        if (IsEmpty)
            return null;

        var model = new GeometryModel3D(Build(), material) { BackMaterial = backMaterial };
        model.Freeze();
        return model;
    }

    // ---------------------------------------------------------------- базовые операции

    private int AddVertex(Point3D p, Vector3D n, Point uv)
    {
        _positions.Add(p);
        _normals.Add(n);
        _uvs.Add(uv);
        return _positions.Count - 1;
    }

    /// <summary>Четырёхугольник a-b-c-d (против часовой стрелки, если смотреть со стороны нормали).</summary>
    public void AddQuad(Point3D a, Point3D b, Point3D c, Point3D d, Vector3D normal, Point uvA, Point uvB, Point uvC, Point uvD)
    {
        normal.Normalize();
        var i0 = AddVertex(a, normal, uvA);
        var i1 = AddVertex(b, normal, uvB);
        var i2 = AddVertex(c, normal, uvC);
        var i3 = AddVertex(d, normal, uvD);
        _indices.Add(i0); _indices.Add(i1); _indices.Add(i2);
        _indices.Add(i0); _indices.Add(i2); _indices.Add(i3);
    }

    public void AddTriangle(Point3D a, Point3D b, Point3D c, Point uvA, Point uvB, Point uvC)
    {
        var n = Vector3D.CrossProduct(b - a, c - a);
        if (n.LengthSquared < 1e-12)
            return;
        n.Normalize();
        var i0 = AddVertex(a, n, uvA);
        var i1 = AddVertex(b, n, uvB);
        var i2 = AddVertex(c, n, uvC);
        _indices.Add(i0); _indices.Add(i1); _indices.Add(i2);
    }

    // ---------------------------------------------------------------- бокс

    /// <summary>Осевой бокс по центру и размерам.</summary>
    public void AddBox(Point3D center, Vector3D size, double uvScale = 1.0) =>
        AddBox(center, size, Matrix3D.Identity, uvScale);

    /// <summary>Бокс с локальным центром/размером, затем трансформированный матрицей.</summary>
    public void AddBox(Point3D center, Vector3D size, Matrix3D transform, double uvScale = 1.0, BoxFaces faces = BoxFaces.All)
    {
        var hx = size.X / 2;
        var hy = size.Y / 2;
        var hz = size.Z / 2;
        var c = center;

        Point3D P(double x, double y, double z) => transform.Transform(new Point3D(c.X + x, c.Y + y, c.Z + z));
        Vector3D N(double x, double y, double z)
        {
            var v = transform.Transform(new Vector3D(x, y, z));
            v.Normalize();
            return v;
        }

        var sx = size.X / uvScale;
        var sy = size.Y / uvScale;
        var sz = size.Z / uvScale;

        if (faces.HasFlag(BoxFaces.Front)) // +Z
            AddQuad(P(-hx, -hy, hz), P(hx, -hy, hz), P(hx, hy, hz), P(-hx, hy, hz), N(0, 0, 1),
                new Point(0, sy), new Point(sx, sy), new Point(sx, 0), new Point(0, 0));
        if (faces.HasFlag(BoxFaces.Back)) // -Z
            AddQuad(P(hx, -hy, -hz), P(-hx, -hy, -hz), P(-hx, hy, -hz), P(hx, hy, -hz), N(0, 0, -1),
                new Point(0, sy), new Point(sx, sy), new Point(sx, 0), new Point(0, 0));
        if (faces.HasFlag(BoxFaces.Right)) // +X
            AddQuad(P(hx, -hy, hz), P(hx, -hy, -hz), P(hx, hy, -hz), P(hx, hy, hz), N(1, 0, 0),
                new Point(0, sy), new Point(sz, sy), new Point(sz, 0), new Point(0, 0));
        if (faces.HasFlag(BoxFaces.Left)) // -X
            AddQuad(P(-hx, -hy, -hz), P(-hx, -hy, hz), P(-hx, hy, hz), P(-hx, hy, -hz), N(-1, 0, 0),
                new Point(0, sy), new Point(sz, sy), new Point(sz, 0), new Point(0, 0));
        if (faces.HasFlag(BoxFaces.Top)) // +Y
            AddQuad(P(-hx, hy, hz), P(hx, hy, hz), P(hx, hy, -hz), P(-hx, hy, -hz), N(0, 1, 0),
                new Point(0, sz), new Point(sx, sz), new Point(sx, 0), new Point(0, 0));
        if (faces.HasFlag(BoxFaces.Bottom)) // -Y
            AddQuad(P(-hx, -hy, -hz), P(hx, -hy, -hz), P(hx, -hy, hz), P(-hx, -hy, hz), N(0, -1, 0),
                new Point(0, 0), new Point(sx, 0), new Point(sx, sz), new Point(0, sz));
    }

    /// <summary>Бокс, повёрнутый вокруг СОБСТВЕННОГО центра (ящики, товары, детали одежды).</summary>
    public void AddBoxAt(Point3D center, Vector3D size, double yawDeg = 0, double pitchDeg = 0, double rollDeg = 0, double uvScale = 1.0) =>
        AddBox(new Point3D(0, 0, 0), size, Trs(new Vector3D(center.X, center.Y, center.Z), yawDeg, pitchDeg, rollDeg), uvScale);

    /// <summary>Бокс от min до max (удобно для геометрии уровня).</summary>
    public void AddBoxMinMax(Point3D min, Point3D max, double uvScale = 1.0, BoxFaces faces = BoxFaces.All) =>
        AddBox(new Point3D((min.X + max.X) / 2, (min.Y + max.Y) / 2, (min.Z + max.Z) / 2),
            new Vector3D(max.X - min.X, max.Y - min.Y, max.Z - min.Z), Matrix3D.Identity, uvScale, faces);

    /// <summary>Брус между двумя точками заданного сечения (балки, поручни, кабели-«палки»).</summary>
    public void AddBeam(Point3D from, Point3D to, double width, double height, double uvScale = 1.0)
    {
        var dir = to - from;
        var length = dir.Length;
        if (length < 1e-6)
            return;

        var transform = AlignZ(dir);
        transform.Translate(new Vector3D((from.X + to.X) / 2, (from.Y + to.Y) / 2, (from.Z + to.Z) / 2));
        AddBox(new Point3D(0, 0, 0), new Vector3D(width, height, length), transform, uvScale);
    }

    // ---------------------------------------------------------------- тела вращения

    /// <summary>Цилиндр вдоль локальной оси Y (основание в center - h/2), трансформированный матрицей.</summary>
    public void AddCylinder(Point3D center, double radius, double height, int segments, Matrix3D transform,
        bool caps = true, double uvScale = 1.0, double topRadius = double.NaN)
    {
        if (double.IsNaN(topRadius))
            topRadius = radius;

        var y0 = center.Y - height / 2;
        var y1 = center.Y + height / 2;
        var circumference = 2 * Math.PI * Math.Max(radius, topRadius) / uvScale;
        var slope = (radius - topRadius) / height;

        for (var i = 0; i < segments; i++)
        {
            var a0 = 2 * Math.PI * i / segments;
            var a1 = 2 * Math.PI * (i + 1) / segments;
            var (s0, c0) = Math.SinCos(a0);
            var (s1, c1) = Math.SinCos(a1);

            var b0 = transform.Transform(new Point3D(center.X + c0 * radius, y0, center.Z + s0 * radius));
            var b1 = transform.Transform(new Point3D(center.X + c1 * radius, y0, center.Z + s1 * radius));
            var t1 = transform.Transform(new Point3D(center.X + c1 * topRadius, y1, center.Z + s1 * topRadius));
            var t0 = transform.Transform(new Point3D(center.X + c0 * topRadius, y1, center.Z + s0 * topRadius));

            var am = (a0 + a1) / 2;
            var normal = transform.Transform(new Vector3D(Math.Cos(am), slope, Math.Sin(am)));
            var u0 = circumference * i / segments;
            var u1 = circumference * (i + 1) / segments;
            var v = height / uvScale;

            // порядок вершин: снаружи против часовой
            AddQuad(b1, b0, t0, t1, normal, new Point(u1, v), new Point(u0, v), new Point(u0, 0), new Point(u1, 0));

            if (caps)
            {
                var ct = transform.Transform(new Point3D(center.X, y1, center.Z));
                var cb = transform.Transform(new Point3D(center.X, y0, center.Z));
                AddTriangle(ct, t1, t0, new Point(0.5, 0.5), new Point(0.5 + c1 * 0.5, 0.5 + s1 * 0.5), new Point(0.5 + c0 * 0.5, 0.5 + s0 * 0.5));
                AddTriangle(cb, b0, b1, new Point(0.5, 0.5), new Point(0.5 + c0 * 0.5, 0.5 + s0 * 0.5), new Point(0.5 + c1 * 0.5, 0.5 + s1 * 0.5));
            }
        }
    }

    public void AddCylinder(Point3D center, double radius, double height, int segments = 10, bool caps = true, double uvScale = 1.0) =>
        AddCylinder(center, radius, height, segments, Matrix3D.Identity, caps, uvScale);

    /// <summary>Труба/кабель между двумя точками.</summary>
    public void AddTube(Point3D from, Point3D to, double radius, int segments = 8, bool caps = true, double uvScale = 1.0)
    {
        var dir = to - from;
        var length = dir.Length;
        if (length < 1e-6)
            return;

        var transform = AlignY(dir);
        transform.Translate(new Vector3D((from.X + to.X) / 2, (from.Y + to.Y) / 2, (from.Z + to.Z) / 2));
        AddCylinder(new Point3D(0, 0, 0), radius, length, segments, transform, caps, uvScale);
    }

    /// <summary>Провисающий кабель (цепная линия, аппроксимированная параболой) из нескольких сегментов.</summary>
    public void AddSaggingCable(Point3D from, Point3D to, double sag, double radius, int pieces = 8)
    {
        var prev = from;
        for (var i = 1; i <= pieces; i++)
        {
            var t = i / (double)pieces;
            var p = new Point3D(
                from.X + (to.X - from.X) * t,
                from.Y + (to.Y - from.Y) * t - sag * 4 * t * (1 - t),
                from.Z + (to.Z - from.Z) * t);
            AddTube(prev, p, radius, 5, caps: false);
            prev = p;
        }
    }

    /// <summary>Низкополигональная UV-сфера/эллипсоид.</summary>
    public void AddSphere(Point3D center, Vector3D radii, int slices, int stacks, Matrix3D transform)
    {
        for (var st = 0; st < stacks; st++)
        {
            var p0 = Math.PI * st / stacks - Math.PI / 2;
            var p1 = Math.PI * (st + 1) / stacks - Math.PI / 2;

            for (var sl = 0; sl < slices; sl++)
            {
                var t0 = 2 * Math.PI * sl / slices;
                var t1 = 2 * Math.PI * (sl + 1) / slices;

                Point3D S(double phi, double theta) => transform.Transform(new Point3D(
                    center.X + radii.X * Math.Cos(phi) * Math.Cos(theta),
                    center.Y + radii.Y * Math.Sin(phi),
                    center.Z + radii.Z * Math.Cos(phi) * Math.Sin(theta)));

                Vector3D Nn(double phi, double theta)
                {
                    var v = transform.Transform(new Vector3D(
                        Math.Cos(phi) * Math.Cos(theta) / radii.X,
                        Math.Sin(phi) / radii.Y,
                        Math.Cos(phi) * Math.Sin(theta) / radii.Z));
                    v.Normalize();
                    return v;
                }

                var a = S(p0, t0); var b = S(p0, t1); var c = S(p1, t1); var d = S(p1, t0);
                var ia = AddVertex(a, Nn(p0, t0), new Point(sl / (double)slices, 1 - st / (double)stacks));
                var ib = AddVertex(b, Nn(p0, t1), new Point((sl + 1) / (double)slices, 1 - st / (double)stacks));
                var ic = AddVertex(c, Nn(p1, t1), new Point((sl + 1) / (double)slices, 1 - (st + 1) / (double)stacks));
                var id = AddVertex(d, Nn(p1, t0), new Point(sl / (double)slices, 1 - (st + 1) / (double)stacks));
                // снаружи против часовой
                _indices.Add(ia); _indices.Add(ic); _indices.Add(ib);
                _indices.Add(ia); _indices.Add(id); _indices.Add(ic);
            }
        }
    }

    public void AddSphere(Point3D center, double radius, int slices = 10, int stacks = 6) =>
        AddSphere(center, new Vector3D(radius, radius, radius), slices, stacks, Matrix3D.Identity);

    /// <summary>Конус с вершиной в apex, раскрывающийся вдоль dir до основания радиуса radius
    /// (без донышка) — конус обзора дрона/фонаря.</summary>
    public void AddCone(Point3D apex, Vector3D dir, double length, double radius, int segments = 20)
    {
        dir.Normalize();
        var transform = AlignY(dir);
        transform.Translate(new Vector3D(apex.X, apex.Y, apex.Z));

        for (var i = 0; i < segments; i++)
        {
            var a0 = 2 * Math.PI * i / segments;
            var a1 = 2 * Math.PI * (i + 1) / segments;
            var b0 = transform.Transform(new Point3D(Math.Cos(a0) * radius, length, Math.Sin(a0) * radius));
            var b1 = transform.Transform(new Point3D(Math.Cos(a1) * radius, length, Math.Sin(a1) * radius));
            var tip = transform.Transform(new Point3D(0, 0, 0));
            var u0 = i / (double)segments;
            var u1 = (i + 1) / (double)segments;
            AddTriangle(tip, b0, b1, new Point((u0 + u1) / 2, 0), new Point(u0, 1), new Point(u1, 1));
        }
    }

    // ---------------------------------------------------------------- плоскости

    /// <summary>Горизонтальная плоскость, разбитая на ячейки ~cell метров: WPF считает освещение
    /// по вершинам, поэтому без тесселяции точечный свет лампы на большом полу не виден.</summary>
    public void AddFloorGrid(double x0, double z0, double x1, double z1, double y, double cell = 1.0, double uvScale = 1.0, bool faceDown = false)
    {
        var nx = Math.Max(1, (int)Math.Ceiling((x1 - x0) / cell));
        var nz = Math.Max(1, (int)Math.Ceiling((z1 - z0) / cell));
        var dx = (x1 - x0) / nx;
        var dz = (z1 - z0) / nz;
        var normal = faceDown ? new Vector3D(0, -1, 0) : new Vector3D(0, 1, 0);

        for (var i = 0; i < nx; i++)
        {
            for (var j = 0; j < nz; j++)
            {
                var xa = x0 + i * dx; var xb = xa + dx;
                var za = z0 + j * dz; var zb = za + dz;
                var a = new Point3D(xa, y, zb); var b = new Point3D(xb, y, zb);
                var c = new Point3D(xb, y, za); var d = new Point3D(xa, y, za);
                var uva = new Point(xa / uvScale, zb / uvScale); var uvb = new Point(xb / uvScale, zb / uvScale);
                var uvc = new Point(xb / uvScale, za / uvScale); var uvd = new Point(xa / uvScale, za / uvScale);
                if (faceDown)
                    AddQuad(d, c, b, a, normal, uvd, uvc, uvb, uva);
                else
                    AddQuad(a, b, c, d, normal, uva, uvb, uvc, uvd);
            }
        }
    }

    /// <summary>Вертикальная стена в плоскости X (нормаль ±Z) или Z (нормаль ±X), тесселированная.</summary>
    public void AddWallGrid(Point3D from, Point3D to, double height, Vector3D normal, double cell = 1.5, double uvScale = 1.0)
    {
        var horizontal = new Vector3D(to.X - from.X, 0, to.Z - from.Z);
        var length = horizontal.Length;
        var nh = Math.Max(1, (int)Math.Ceiling(length / cell));
        var nv = Math.Max(1, (int)Math.Ceiling(height / cell));
        horizontal /= nh;
        var dv = height / nv;

        for (var i = 0; i < nh; i++)
        {
            for (var j = 0; j < nv; j++)
            {
                var a = from + horizontal * i + new Vector3D(0, dv * j, 0);
                var b = a + horizontal;
                var c = b + new Vector3D(0, dv, 0);
                var d = a + new Vector3D(0, dv, 0);
                var u0 = i * length / nh / uvScale; var u1 = (i + 1) * length / nh / uvScale;
                var v0 = (height - j * dv) / uvScale; var v1 = (height - (j + 1) * dv) / uvScale;

                // ориентация: нормаль должна совпадать с заданной
                var n = Vector3D.CrossProduct(b - a, d - a);
                if (Vector3D.DotProduct(n, normal) >= 0)
                    AddQuad(a, b, c, d, normal, new Point(u0, v0), new Point(u1, v0), new Point(u1, v1), new Point(u0, v1));
                else
                    AddQuad(b, a, d, c, normal, new Point(u1, v0), new Point(u0, v0), new Point(u0, v1), new Point(u1, v1));
            }
        }
    }

    /// <summary>Двусторонняя прямоугольная панель (вывеска, брезент): центр, ось "вправо", ось "вверх".</summary>
    public void AddPanel(Point3D center, Vector3D right, Vector3D up, double width, double height, bool doubleSided = true)
    {
        right.Normalize();
        up.Normalize();
        var hw = right * (width / 2);
        var hh = up * (height / 2);
        var normal = Vector3D.CrossProduct(right, up);
        var a = center - hw - hh; var b = center + hw - hh; var c = center + hw + hh; var d = center - hw + hh;
        AddQuad(a, b, c, d, normal, new Point(0, 1), new Point(1, 1), new Point(1, 0), new Point(0, 0));
        if (doubleSided)
            AddQuad(b, a, d, c, -normal, new Point(0, 1), new Point(1, 1), new Point(1, 0), new Point(0, 0));
    }

    // ---------------------------------------------------------------- матрицы

    /// <summary>Поворот, переводящий локальную ось Y в направление dir.</summary>
    public static Matrix3D AlignY(Vector3D dir) => Align(new Vector3D(0, 1, 0), dir);

    /// <summary>Поворот, переводящий локальную ось Z в направление dir.</summary>
    public static Matrix3D AlignZ(Vector3D dir) => Align(new Vector3D(0, 0, 1), dir);

    private static Matrix3D Align(Vector3D from, Vector3D to)
    {
        to.Normalize();
        var axis = Vector3D.CrossProduct(from, to);
        var dot = Vector3D.DotProduct(from, to);
        var m = Matrix3D.Identity;

        if (axis.LengthSquared < 1e-10)
        {
            if (dot < 0)
                m.Rotate(new Quaternion(Math.Abs(from.X) > 0.9 ? new Vector3D(0, 1, 0) : new Vector3D(1, 0, 0), 180));
            return m;
        }

        var angle = Vector3D.AngleBetween(from, to);
        m.Rotate(new Quaternion(axis, angle));
        return m;
    }

    public static Matrix3D Trs(Vector3D translate, double yawDeg = 0, double pitchDeg = 0, double rollDeg = 0, double scale = 1.0)
    {
        var m = Matrix3D.Identity;
        if (Math.Abs(scale - 1.0) > 1e-9)
            m.Scale(new Vector3D(scale, scale, scale));
        if (rollDeg != 0)
            m.Rotate(new Quaternion(new Vector3D(0, 0, 1), rollDeg));
        if (pitchDeg != 0)
            m.Rotate(new Quaternion(new Vector3D(1, 0, 0), pitchDeg));
        if (yawDeg != 0)
            m.Rotate(new Quaternion(new Vector3D(0, 1, 0), yawDeg));
        m.Translate(translate);
        return m;
    }
}

[Flags]
public enum BoxFaces
{
    None = 0,
    Front = 1,
    Back = 2,
    Left = 4,
    Right = 8,
    Top = 16,
    Bottom = 32,
    All = Front | Back | Left | Right | Top | Bottom,
    Sides = Front | Back | Left | Right
}
