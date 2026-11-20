using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ZeroFloor.Services;

namespace ZeroFloor.Views.Controls;

public partial class CharacterLayerControl : UserControl
{
    // Размер "коробки", в которую вписывается (Stretch=Uniform, без обрезки/искажений)
    // реальное изображение персонажа — рассчитан на полноростовой портрет на холсте 1920x1080;
    // якорь (центр по горизонтали, низ по вертикали) — тот же, что и у силуэта-заглушки ниже.
    private const double ImageBoxWidth = 420;
    private const double ImageBoxHeight = 760;
    private const double ImageBoxBottomMargin = 160;

    private readonly Dictionary<string, FrameworkElement> _characters = new();

    public CharacterLayerControl()
    {
        InitializeComponent();
    }

    /// <summary>Возвращает визуал персонажа, создавая его (невидимым, Opacity=0) при первом
    /// обращении — вызывается из Enter; дальнейшие Move/Scale берут уже существующий. Если
    /// <paramref name="assets"/> резолвит реальное изображение для этого id — показывается оно
    /// (вписанное без обрезки и искажений, см. BuildCharacterImage); иначе — силуэт-заглушка
    /// с подписанным id (CLAUDE.md, правило 6: отсутствующий ассет не подменяется случайной
    /// картинкой, а явно помечается как отсутствующий).</summary>
    public FrameworkElement GetOrCreateCharacter(string characterId, IAssetService assets)
    {
        if (_characters.TryGetValue(characterId, out var existing))
            return existing;

        var image = assets.ResolveCharacterImage(characterId);
        var element = image is not null ? BuildCharacterImage(image) : BuildSilhouette(characterId);
        element.Opacity = 0;
        _characters[characterId] = element;
        Stage.Children.Add(element);
        return element;
    }

    public FrameworkElement? GetCharacterIfPresent(string characterId) =>
        _characters.TryGetValue(characterId, out var existing) ? existing : null;

    /// <summary>Id всех персонажей, сейчас стоящих на сцене — используется при входе в новую
    /// сцену, чтобы убрать тех, кто не участвует в её разговоре/действии (не должен "незаметно
    /// унаследоваться" из предыдущей сцены этой же главы).</summary>
    public IReadOnlyList<string> PresentCharacterIds => _characters.Keys.ToList();

    public void RemoveCharacter(string characterId)
    {
        if (_characters.Remove(characterId, out var element))
            Stage.Children.Remove(element);
    }

    private static FrameworkElement BuildSilhouette(string characterId)
    {
        var resources = Application.Current.Resources;

        var container = new Grid
        {
            Width = 240,
            Height = 430,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 220)
        };

        var head = new Ellipse
        {
            Width = 96,
            Height = 96,
            Fill = (Brush)resources["SilhouetteBrush"],
            Stroke = (Brush)resources["SilhouetteRimBrush"],
            StrokeThickness = 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top
        };

        var body = new Polygon
        {
            Points = new PointCollection { new Point(30, 330), new Point(210, 330), new Point(178, 110), new Point(62, 110) },
            Fill = (Brush)resources["SilhouetteBrush"],
            Stroke = (Brush)resources["SilhouetteRimBrush"],
            StrokeThickness = 2
        };

        var label = new TextBlock
        {
            Text = characterId,
            Style = (Style)resources["PlaceholderCaptionTextStyle"],
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, -24)
        };

        container.Children.Add(head);
        container.Children.Add(body);
        container.Children.Add(label);

        return container;
    }

    /// <summary>Реальное изображение персонажа — одно статичное изображение, без кадров/поз
    /// (CLAUDE.md). Stretch=Uniform вписывает его в коробку целиком, сохраняя пропорции:
    /// не растягивает и не обрезает ни голову, ни ноги, каким бы ни было кадрирование
    /// исходного файла (во весь рост или только по пояс — см. CharacterDefinition.DefaultScale
    /// для сценарной поправки масштаба между персонажами с разным кадрированием).</summary>
    private static FrameworkElement BuildCharacterImage(BitmapImage image)
    {
        var container = new Grid
        {
            Width = ImageBoxWidth,
            Height = ImageBoxHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, ImageBoxBottomMargin)
        };

        var picture = new Image
        {
            Source = image,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        RenderOptions.SetBitmapScalingMode(picture, BitmapScalingMode.HighQuality);

        container.Children.Add(picture);
        return container;
    }
}
