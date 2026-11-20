using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace ZeroFloor.Services.Presentation.World3D;

/// <summary>
/// «Кожа» 3D-модели из авторского портрета персонажа (Assets/Characters/*.png): фронтальная
/// проекция изображения на переднюю сторону тела в позе привязки. Лицо, причёска, одежда,
/// ремень и руки берутся прямо с портрета; задняя сторона и ноги ниже кадра — фрагменты ткани
/// и волос, вырезанные из того же изображения (без подстановки посторонних текстур).
/// Калибровка — по опорным точкам портрета: линия молнии (центр), подбородок и пряжка ремня.
/// Если файла нет, Load возвращает null и модель остаётся на процедурных материалах.
/// </summary>
public enum ProjectionMode
{
    Body,
    Arm,
    Head
}

public sealed class PortraitSkin
{
    private readonly int _width;
    private readonly int _height;

    private PortraitSkin(BitmapSource image, Calibration cal)
    {
        _width = image.PixelWidth;
        _height = image.PixelHeight;
        Cal = cal;
        Front = MakeMaterial(image, new Rect(0, 0, 1, 1), tile: false);
        Fabric = MakeMaterial(Crop(image, cal.FabricRect), new Rect(0, 0, 1.0, 1.0), tile: true);
        Trousers = MakeMaterial(Crop(image, cal.TrousersRect), new Rect(0, 0, 1.0, 1.0), tile: true);
        Hair = MakeMaterial(Crop(image, cal.HairRect), new Rect(0, 0, 0.18, 0.18), tile: true);
    }

    public sealed record Calibration(
        double CenterX,          // px: вертикальная ось симметрии (молния)
        double ChinY,            // px: подбородок
        double BeltY,            // px: центр ремня
        double ModelChinY,       // м: подбородок модели в позе привязки
        double ModelBeltY,       // м: ремень модели
        Int32Rect FabricRect,
        Int32Rect TrousersRect,
        Int32Rect HairRect,
        double HeadCenterX,
        double HeadPixelsPerMeter,
        double ModelHeadChinY,
        double ArmOffset,
        double FarSideScale,
        double HairTopY,
        double ModelShoulderY,
        double FaceFarEdgeX)
    {
        public double PixelsPerMeter => (BeltY - ChinY) / (ModelChinY - ModelBeltY);
    }

    public Calibration Cal { get; }
    public Material Front { get; }
    public Material Fabric { get; }
    public Material Trousers { get; }
    public Material Hair { get; }

    /// <summary>Самая нижняя точка модели (м), ещё попадающая в кадр портрета.</summary>
    public double LowestPhotoY => Cal.ModelBeltY - (_height - 8 - Cal.BeltY) / Cal.PixelsPerMeter;

    /// <summary>
    /// UV портрета для точки модели в позе привязки. Портрет снят в 3/4, поэтому режимы разные:
    /// Body — без зеркала, дальняя (левая для Макса) сторона сжата так же, как на фото;
    /// Arm — обе руки берут более фронтальную правую руку портрета (зеркально) со сдвигом от корпуса;
    /// Head — лицо зеркально вокруг носа и в собственном масштабе (голова на фото крупнее).
    /// </summary>
    public Point Project(Point3D p, ProjectionMode mode)
    {
        double x, y;
        switch (mode)
        {
            case ProjectionMode.Head:
                // лицо повёрнуто: ближняя (правая для Макса) половина — как есть, дальняя сжата
                // лицо на фото повёрнуто: ближняя (правая для Макса) половина — как есть,
                // дальняя — её зеркальная копия относительно средней линии лица
                x = Cal.HeadCenterX - Math.Abs(p.X) * Cal.HeadPixelsPerMeter;
                y = Math.Max(Cal.ChinY + (Cal.ModelHeadChinY - p.Y) * Cal.HeadPixelsPerMeter, Cal.HairTopY);
                break;
            case ProjectionMode.Arm:
                // рука на фото отходит от корпуса к кисти: смещение растёт от плеча (0) вниз
                var along = Math.Clamp((Cal.ModelShoulderY - p.Y) / 0.5, 0, 1);
                x = Cal.CenterX - (Math.Abs(p.X) + Cal.ArmOffset * along) * Cal.PixelsPerMeter;
                y = Cal.ChinY + (Cal.ModelChinY - p.Y) * Cal.PixelsPerMeter;
                break;
            default:
                x = Cal.CenterX + p.X * Cal.PixelsPerMeter * (p.X > 0 ? Cal.FarSideScale : 1.0);
                y = Cal.ChinY + (Cal.ModelChinY - p.Y) * Cal.PixelsPerMeter;
                break;
        }
        return new Point(Math.Clamp(x / _width, 0.001, 0.999), Math.Clamp(y / _height, 0.001, 0.999));
    }

    private static readonly Dictionary<string, PortraitSkin?> Cache = new();

    public static PortraitSkin? LoadMax()
    {
        // Опорные точки портрета max_orlov.png (1024×1536): молния x≈600, нос x≈595,
        // подбородок y≈360, пряжка ремня y≈1080; правая рука висит в ~0.055 м от корпуса.
        // Модель: подбородок 1.605–1.615 м, ремень 1.04 м.
        var cal = new Calibration(600, 360, 1080, 1.615, 1.04,
            FabricRect: new Int32Rect(440, 890, 100, 100),
            TrousersRect: new Int32Rect(440, 890, 100, 100),
            HairRect: new Int32Rect(470, 95, 120, 60),
            HeadCenterX: 588,
            HeadPixelsPerMeter: 1450,
            ModelHeadChinY: 1.605,
            ArmOffset: 0.055,
            FarSideScale: 0.62,
            HairTopY: 78,
            ModelShoulderY: 1.48,
            FaceFarEdgeX: 618);
        return Load("max_orlov.png", cal);
    }

    private static PortraitSkin? Load(string fileName, Calibration cal)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(fileName, out var cached))
                return cached;

            PortraitSkin? skin = null;
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", fileName);
                if (File.Exists(path))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(path);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    skin = new PortraitSkin(Opaque(bmp), cal);
                }
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException)
            {
                skin = null;
            }
            Cache[fileName] = skin;
            return skin;
        }
    }

    /// <summary>Прозрачный фон портрета заливается цветом соседних непрозрачных пикселей по
    /// строке — иначе по контуру силуэта модель «просвечивала» бы насквозь.</summary>
    private static BitmapSource Opaque(BitmapSource source)
    {
        var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var w = bgra.PixelWidth;
        var h = bgra.PixelHeight;
        var stride = w * 4;
        var px = new byte[stride * h];
        bgra.CopyPixels(px, stride, 0);

        for (var y = 0; y < h; y++)
        {
            var row = y * stride;
            int first = -1, last = -1;
            for (var x = 0; x < w; x++)
                if (px[row + x * 4 + 3] > 200) { if (first < 0) first = x; last = x; }

            for (var x = 0; x < w; x++)
            {
                var i = row + x * 4;
                if (px[i + 3] > 200) { px[i + 3] = 255; continue; }
                var src = first < 0 ? -1 : x < first ? first + 2 : x > last ? last - 2 : NearestOpaque(px, row, x, w);
                if (src < 0)
                {
                    px[i] = 66; px[i + 1] = 62; px[i + 2] = 60; px[i + 3] = 255;
                }
                else
                {
                    var j = row + Math.Clamp(src, 0, w - 1) * 4;
                    px[i] = px[j]; px[i + 1] = px[j + 1]; px[i + 2] = px[j + 2]; px[i + 3] = 255;
                }
            }
        }

        var result = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, stride);
        result.Freeze();
        return result;
    }

    private static int NearestOpaque(byte[] px, int row, int x, int w)
    {
        for (var d = 1; d < w; d++)
        {
            if (x - d >= 0 && px[row + (x - d) * 4 + 3] > 200) return x - d;
            if (x + d < w && px[row + (x + d) * 4 + 3] > 200) return x + d;
        }
        return -1;
    }

    private static BitmapSource Crop(BitmapSource image, Int32Rect rect)
    {
        var c = new CroppedBitmap(image, rect);
        c.Freeze();
        return c;
    }

    private static Material MakeMaterial(BitmapSource image, Rect viewport, bool tile)
    {
        var brush = new ImageBrush(image)
        {
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = viewport,
            TileMode = tile ? TileMode.FlipXY : TileMode.None,
            Stretch = Stretch.Fill
        };
        brush.Freeze();
        var group = new MaterialGroup();
        group.Children.Add(new DiffuseMaterial(brush));
        group.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)), 18));
        // лёгкая самоподсветка: портрет остаётся читаемым и в тёмных локациях
        group.Children.Add(new EmissiveMaterial(brush) { Color = Color.FromRgb(48, 48, 48) });
        group.Freeze();
        return group;
    }
}
