using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace ZeroFloor.Services.Presentation.World3D;

/// <summary>
/// Процедурные материалы 3D-эпизодов. Все текстуры синтезируются в коде (шум, панели, заклёпки,
/// рифление, полосы, неоновый текст) — никаких внешних файлов: это собственные ресурсы эпизодов,
/// а не подмена отсутствующих ассетов автора (CLAUDE.md, правило 6 касается портретов/фонов/звука
/// новеллы — для 3D-эпизодов пользователь явно поручил создать ресурсы самостоятельно).
/// Кэшируются по ключу и замораживаются (Freeze) — безопасно разделять между моделями.
/// </summary>
public static class Materials3D
{
    private static readonly Dictionary<string, Material> Cache = new();
    private static readonly Dictionary<string, ImageSource> TextureCache = new();

    // ------------------------------------------------------------------ простые материалы

    public static Material Matte(Color color, double specular = 0, double power = 30)
    {
        var key = $"matte:{color}:{specular}:{power}";
        return Cached(key, () =>
        {
            var diffuse = new DiffuseMaterial(new SolidColorBrush(color));
            if (specular <= 0)
                return diffuse;
            var group = new MaterialGroup();
            group.Children.Add(diffuse);
            group.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb((byte)(255 * specular), 255, 255, 255)), power));
            return group;
        });
    }

    /// <summary>Светящийся материал (лампы, неон, индикаторы): тёмная база + эмиссия.</summary>
    public static Material Emissive(Color color, double intensity = 1.0)
    {
        var key = $"emissive:{color}:{intensity}";
        return Cached(key, () =>
        {
            var group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(8, 8, 10))));
            var c = Color.FromRgb(
                (byte)Math.Min(255, color.R * intensity),
                (byte)Math.Min(255, color.G * intensity),
                (byte)Math.Min(255, color.B * intensity));
            group.Children.Add(new EmissiveMaterial(new SolidColorBrush(c)));
            return group;
        });
    }

    /// <summary>Полупрозрачный светящийся материал (конус сканера, лучи прожекторов, дымка).</summary>
    public static Material Glow(Color color, double alpha)
    {
        var key = $"glow:{color}:{alpha}";
        return Cached(key, () =>
        {
            var group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromArgb((byte)(alpha * 255 * 0.25), 0, 0, 0))));
            group.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), color.R, color.G, color.B))));
            return group;
        });
    }

    /// <summary>Мягкое световое пятно/ореол — радиальный градиент с альфой (частицы, гало ламп).</summary>
    public static Material SoftGlow(Color color, double alpha = 1.0)
    {
        var key = $"softglow:{color}:{alpha}";
        return Cached(key, () =>
        {
            var brush = new RadialGradientBrush();
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * alpha), color.R, color.G, color.B), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(110 * alpha), color.R, color.G, color.B), 0.35));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
            brush.Freeze();
            var group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(new SolidColorBrush(Colors.Transparent)));
            group.Children.Add(new EmissiveMaterial(brush));
            return group;
        });
    }

    /// <summary>Мягкая дымка (пар/пыль) — рассеянный, а не светящийся материал.</summary>
    public static Material Smoke(Color color, double alpha)
    {
        var key = $"smoke:{color}:{alpha}";
        return Cached(key, () =>
        {
            var brush = new RadialGradientBrush();
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * alpha), color.R, color.G, color.B), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(120 * alpha), color.R, color.G, color.B), 0.45));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
            brush.Freeze();
            var group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(brush));
            group.Children.Add(new EmissiveMaterial(brush) { Color = Color.FromArgb(255, 70, 70, 70) });
            return group;
        });
    }

    // ------------------------------------------------------------------ текстурированные

    public static Material MetalPanel(Color baseColor, double rust = 0.3, double specular = 0.25, double uvTile = 1.0)
    {
        var key = $"metal:{baseColor}:{rust}:{specular}";
        return Cached(key, () => Textured(MetalPanelTexture(baseColor, rust), specular, 25));
    }

    public static Material DiamondPlate(Color baseColor) =>
        Cached($"diamond:{baseColor}", () => Textured(DiamondPlateTexture(baseColor), 0.35, 40));

    public static Material Grating(Color baseColor) =>
        Cached($"grating:{baseColor}", () => Textured(GratingTexture(baseColor), 0.3, 30));

    public static Material Concrete(Color baseColor) =>
        Cached($"concrete:{baseColor}", () => Textured(NoiseTexture(baseColor, 0.18, 0.08, 7, stains: true), 0, 0));

    public static Material Fabric(Color baseColor, double contrast = 0.12) =>
        Cached($"fabric:{baseColor}:{contrast}", () => Textured(FabricTexture(baseColor, contrast), 0, 0));

    /// <summary>Лоскутная куртка Гоши (по портрету: бурые, красные, тёмно-синие заплаты со швами).</summary>
    public static Material Patchwork() =>
        Cached("patchwork", () =>
        {
            var tex = CachedTexture("t-patchwork", () =>
            {
                const int size = 128;
                var rng = new Random(17);
                Color[] palette =
                {
                    Hex("#5B4636"), Hex("#6E2F28"), Hex("#2D3547"), Hex("#7A6A4E"), Hex("#3E3A33"), Hex("#8A7B62")
                };
                var px = NoisePixels(size, palette[0], 0.12, 0.08, 61);
                for (var p = 0; p < 22; p++)
                {
                    var c = palette[rng.Next(palette.Length)];
                    int x0 = rng.Next(size), y0 = rng.Next(size), w = rng.Next(14, 40), h = rng.Next(14, 40);
                    for (var y = y0; y < y0 + h; y++)
                    for (var x = x0; x < x0 + w; x++)
                    {
                        var i = (y & (size - 1)) * size + (x & (size - 1));
                        var edge = x == x0 || y == y0 || x == x0 + w - 1 || y == y0 + h - 1;
                        px[i] = edge ? Lighten(c, 0.25) : Lerp(px[i], c, 0.85);
                    }
                }
                return ToBitmap(size, px);
            });
            return Textured(tex, 0.05, 8);
        });

    public static Material Hazard() =>
        Cached("hazard", () => Textured(HazardTexture(), 0.2, 20));

    public static Material Wood(Color baseColor) =>
        Cached($"wood:{baseColor}", () => Textured(WoodTexture(baseColor), 0.05, 10));

    public static Material Screen(Color glow) =>
        Cached($"screen:{glow}", () =>
        {
            var tex = ScreenTexture(glow);
            var group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(5, 6, 8))));
            group.Children.Add(new EmissiveMaterial(TileBrush(tex, tile: false)));
            return group;
        });

    /// <summary>Неоновая вывеска с текстом: светится сама, без внешнего источника.</summary>
    public static Material NeonSign(string text, Color color, Color background, double fontSize = 64, double widthPx = 512, double heightPx = 160)
    {
        var key = $"neon:{text}:{color}:{background}:{fontSize}";
        return Cached(key, () =>
        {
            var tex = Render((int)widthPx, (int)heightPx, dc =>
            {
                dc.DrawRectangle(new SolidColorBrush(background), null, new Rect(0, 0, widthPx, heightPx));
                var frame = new Pen(new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B)), 4);
                dc.DrawRoundedRectangle(null, frame, new Rect(8, 8, widthPx - 16, heightPx - 16), 14, 14);
                var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Condensed);
                var dpi = 1.0;
                // ореол: тот же текст несколько раз с расфокусом прозрачностью
                for (var i = 4; i >= 1; i--)
                {
                    var glowText = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        typeface, fontSize + i * 3, new SolidColorBrush(Color.FromArgb((byte)(28 + 10 * (4 - i)), color.R, color.G, color.B)), dpi);
                    dc.DrawText(glowText, new Point((widthPx - glowText.Width) / 2, (heightPx - glowText.Height) / 2));
                }
                var core = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    typeface, fontSize, new SolidColorBrush(Lerp(color, Colors.White, 0.55)), dpi);
                dc.DrawText(core, new Point((widthPx - core.Width) / 2, (heightPx - core.Height) / 2));
            });
            var group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(4, 4, 6))));
            group.Children.Add(new EmissiveMaterial(TileBrush(tex, tile: false)));
            return group;
        });
    }

    /// <summary>Табличка/надпись без свечения (трафарет на металле).</summary>
    public static Material Stencil(string text, Color textColor, Color background, double fontSize = 56, int widthPx = 512, int heightPx = 128)
    {
        var key = $"stencil:{text}:{textColor}:{background}";
        return Cached(key, () =>
        {
            var tex = Render(widthPx, heightPx, dc =>
            {
                dc.DrawRectangle(new SolidColorBrush(background), null, new Rect(0, 0, widthPx, heightPx));
                var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Condensed);
                var ft = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    typeface, fontSize, new SolidColorBrush(textColor), 1.0);
                dc.DrawText(ft, new Point((widthPx - ft.Width) / 2, (heightPx - ft.Height) / 2));
            });
            return Textured(tex, 0.1, 10, tile: false);
        });
    }

    // ------------------------------------------------------------------ синтез текстур

    private static Material Textured(ImageSource texture, double specular, double power, bool tile = true)
    {
        var diffuse = new DiffuseMaterial(TileBrush(texture, tile));
        if (specular <= 0)
            return diffuse;
        var group = new MaterialGroup();
        group.Children.Add(diffuse);
        group.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb((byte)(255 * specular), 255, 255, 255)), power));
        return group;
    }

    private static ImageBrush TileBrush(ImageSource texture, bool tile)
    {
        var brush = new ImageBrush(texture)
        {
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, 1, 1),
            TileMode = tile ? TileMode.Tile : TileMode.None,
            Stretch = Stretch.Fill
        };
        brush.Freeze();
        return brush;
    }

    private static ImageSource MetalPanelTexture(Color baseColor, double rust)
    {
        return CachedTexture($"t-metal:{baseColor}:{rust}", () =>
        {
            const int size = 128;
            var px = NoisePixels(size, baseColor, 0.10, 0.05, 11);
            var rng = new Random(baseColor.GetHashCode() ^ 77);
            var rustColor = Color.FromRgb(110, 58, 30);
            var rustNoise = ValueNoise(size, 16, 91);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var i = y * size + x;
                    var r = rustNoise[i];
                    var drip = Math.Max(0, (y / (double)size) - 0.55) * 0.8; // потёки к низу панели
                    var amount = Math.Clamp((r + drip - (1 - rust)) * 2.2, 0, 0.85);
                    px[i] = Lerp(px[i], rustColor, amount);

                    // швы панели по краям
                    if (x < 2 || y < 2)
                        px[i] = Darken(px[i], 0.45);
                    else if (x > size - 3 || y > size - 3)
                        px[i] = Lighten(px[i], 0.12);
                }
            }

            // заклёпки по углам
            foreach (var (cx, cy) in new[] { (8, 8), (size - 9, 8), (8, size - 9), (size - 9, size - 9), (size / 2, 8), (size / 2, size - 9) })
            {
                for (var dy = -2; dy <= 2; dy++)
                for (var dx = -2; dx <= 2; dx++)
                {
                    if (dx * dx + dy * dy > 5) continue;
                    var i = (cy + dy) * size + cx + dx;
                    px[i] = dy < 0 ? Lighten(px[i], 0.35) : Darken(px[i], 0.25);
                }
            }

            // царапины
            for (var s = 0; s < 14; s++)
            {
                var x0 = rng.Next(size); var y0 = rng.Next(size);
                var len = rng.Next(6, 26);
                var dir = rng.NextDouble() * Math.PI;
                for (var t = 0; t < len; t++)
                {
                    var x = (int)(x0 + Math.Cos(dir) * t) & (size - 1);
                    var y = (int)(y0 + Math.Sin(dir) * t) & (size - 1);
                    px[y * size + x] = Lighten(px[y * size + x], 0.18);
                }
            }

            return ToBitmap(size, px);
        });
    }

    private static ImageSource DiamondPlateTexture(Color baseColor)
    {
        return CachedTexture($"t-diamond:{baseColor}", () =>
        {
            const int size = 128;
            var px = NoisePixels(size, baseColor, 0.08, 0.06, 23);
            const int cell = 16;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var cx = x % cell - cell / 2.0;
                    var cy = y % cell - cell / 2.0;
                    var flip = ((x / cell) + (y / cell)) % 2 == 0;
                    // вытянутый ромб-рифль под 45°
                    var u = flip ? (cx + cy) : (cx - cy);
                    var v = flip ? (cx - cy) : (cx + cy);
                    var d = Math.Abs(u) / 6.5 + Math.Abs(v) / 1.6;
                    var i = y * size + x;
                    if (d < 1)
                        px[i] = (flip ? u : v) > 0 ? Lighten(px[i], 0.28) : Darken(px[i], 0.15);
                }
            }
            return ToBitmap(size, px);
        });
    }

    private static ImageSource GratingTexture(Color baseColor)
    {
        return CachedTexture($"t-grating:{baseColor}", () =>
        {
            const int size = 128;
            var px = NoisePixels(size, baseColor, 0.08, 0.05, 31);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var i = y * size + x;
                    var bx = x % 16;
                    var by = y % 32;
                    var isBar = bx < 3 || by < 3;
                    px[i] = isBar ? Lighten(px[i], bx < 1 || by < 1 ? 0.12 : 0.0) : Darken(px[i], 0.86);
                }
            }
            return ToBitmap(size, px);
        });
    }

    private static ImageSource FabricTexture(Color baseColor, double contrast)
    {
        return CachedTexture($"t-fabric:{baseColor}:{contrast}", () =>
        {
            const int size = 64;
            var px = NoisePixels(size, baseColor, contrast, 0.07, 41);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var i = y * size + x;
                if ((x + y) % 4 == 0)
                    px[i] = Darken(px[i], 0.08);
            }
            return ToBitmap(size, px);
        });
    }

    private static ImageSource HazardTexture()
    {
        return CachedTexture("t-hazard", () =>
        {
            const int size = 64;
            var yellow = Color.FromRgb(214, 168, 38);
            var black = Color.FromRgb(22, 22, 24);
            var noise = ValueNoise(size, 8, 5);
            var px = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var i = y * size + x;
                var stripe = ((x + y) / 16) % 2 == 0;
                var c = stripe ? yellow : black;
                px[i] = Darken(c, noise[i] * 0.35);
            }
            return ToBitmap(size, px);
        });
    }

    private static ImageSource WoodTexture(Color baseColor)
    {
        return CachedTexture($"t-wood:{baseColor}", () =>
        {
            const int size = 64;
            var px = NoisePixels(size, baseColor, 0.12, 0.05, 51);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var i = y * size + x;
                var grain = Math.Sin(y * 0.9 + Math.Sin(x * 0.15) * 3) * 0.5 + 0.5;
                px[i] = Darken(px[i], grain * 0.18);
                if (y % 16 == 0) px[i] = Darken(px[i], 0.5); // щели между досками
            }
            return ToBitmap(size, px);
        });
    }

    private static ImageSource ScreenTexture(Color glow)
    {
        return CachedTexture($"t-screen:{glow}", () =>
        {
            return Render(128, 96, dc =>
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(4, 10, 12)), null, new Rect(0, 0, 128, 96));
                var rng = new Random(glow.GetHashCode());
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(220, glow.R, glow.G, glow.B)), 2);
                for (var line = 0; line < 9; line++)
                {
                    var w = 20 + rng.Next(80);
                    dc.DrawLine(pen, new Point(10, 10 + line * 9), new Point(10 + w, 10 + line * 9));
                }
                var graph = new StreamGeometry();
                using (var ctx = graph.Open())
                {
                    ctx.BeginFigure(new Point(70, 80), false, false);
                    for (var i = 0; i < 10; i++)
                        ctx.LineTo(new Point(70 + i * 5, 80 - rng.Next(30)), true, false);
                }
                dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(255, 255, 200, 90)), 1.5), graph);
            });
        });
    }

    private static ImageSource NoiseTexture(Color baseColor, double contrast, double fine, int seed, bool stains)
    {
        return CachedTexture($"t-noise:{baseColor}:{contrast}:{seed}:{stains}", () =>
        {
            const int size = 128;
            var px = NoisePixels(size, baseColor, contrast, fine, seed);
            if (stains)
            {
                var s = ValueNoise(size, 32, seed + 3);
                for (var i = 0; i < px.Length; i++)
                    px[i] = Darken(px[i], Math.Max(0, s[i] - 0.6) * 0.9);
            }
            return ToBitmap(size, px);
        });
    }

    // ------------------------------------------------------------------ шум и утилиты

    private static Color[] NoisePixels(int size, Color baseColor, double contrast, double fine, int seed)
    {
        var coarse = ValueNoise(size, 16, seed);
        var mid = ValueNoise(size, 8, seed + 1);
        var rng = new Random(seed);
        var px = new Color[size * size];
        for (var i = 0; i < px.Length; i++)
        {
            var n = (coarse[i] - 0.5) * contrast * 2 + (mid[i] - 0.5) * contrast + (rng.NextDouble() - 0.5) * fine;
            px[i] = n >= 0 ? Lighten(baseColor, n) : Darken(baseColor, -n);
        }
        return px;
    }

    /// <summary>Тайлящийся value-noise [0..1] с периодом size и размером ячейки cell.</summary>
    private static double[] ValueNoise(int size, int cell, int seed)
    {
        var rng = new Random(seed);
        var grid = size / cell;
        var lattice = new double[grid * grid];
        for (var i = 0; i < lattice.Length; i++)
            lattice[i] = rng.NextDouble();

        var result = new double[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var gx = x / (double)cell; var gy = y / (double)cell;
                var x0 = (int)gx; var y0 = (int)gy;
                var fx = Smooth(gx - x0); var fy = Smooth(gy - y0);
                double L(int a, int b) => lattice[((b % grid + grid) % grid) * grid + ((a % grid + grid) % grid)];
                var top = L(x0, y0) * (1 - fx) + L(x0 + 1, y0) * fx;
                var bottom = L(x0, y0 + 1) * (1 - fx) + L(x0 + 1, y0 + 1) * fx;
                result[y * size + x] = top * (1 - fy) + bottom * fy;
            }
        }
        return result;
    }

    private static double Smooth(double t) => t * t * (3 - 2 * t);

    private static BitmapSource ToBitmap(int size, Color[] px)
    {
        var bytes = new byte[size * size * 4];
        for (var i = 0; i < px.Length; i++)
        {
            bytes[i * 4] = px[i].B;
            bytes[i * 4 + 1] = px[i].G;
            bytes[i * 4 + 2] = px[i].R;
            bytes[i * 4 + 3] = 255;
        }
        var bmp = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, bytes, size * 4);
        bmp.Freeze();
        return bmp;
    }

    private static BitmapSource Render(int width, int height, Action<DrawingContext> draw)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
            draw(dc);
        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    public static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * t),
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    public static Color Darken(Color c, double amount) => Lerp(c, Color.FromRgb(0, 0, 0), amount);
    public static Color Lighten(Color c, double amount) => Lerp(c, Color.FromRgb(255, 255, 255), amount);

    public static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static Material Cached(string key, Func<Material> factory)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var material))
                return material;
            material = factory();
            material.Freeze();
            Cache[key] = material;
            return material;
        }
    }

    private static ImageSource CachedTexture(string key, Func<ImageSource> factory)
    {
        lock (TextureCache)
        {
            if (TextureCache.TryGetValue(key, out var tex))
                return tex;
            tex = factory();
            TextureCache[key] = tex;
            return tex;
        }
    }
}
