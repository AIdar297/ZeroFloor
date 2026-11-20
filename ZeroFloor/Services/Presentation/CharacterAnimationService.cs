using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static ZeroFloor.Services.Presentation.AnimationPrimitives;

namespace ZeroFloor.Services.Presentation;

public sealed class CharacterAnimationService : ICharacterAnimationService
{
    private readonly ISettingsStore _settingsStore;

    public CharacterAnimationService(ISettingsStore settingsStore) => _settingsStore = settingsStore;

    public Task EnterAsync(FrameworkElement element, double targetX, double targetY, double targetScale, int durationMs)
    {
        var (scale, translate) = EnsureTransforms(element);
        var duration = ScaleDuration(_settingsStore, durationMs);

        translate.X = targetX;
        translate.Y = targetY;

        // Заметный, но не "скользящий" выход на сцену: более глубокий начальный масштаб
        // (0.85 вместо едва заметного 0.97) + мягкий overshoot при завершении (BackEase) —
        // персонаж будто слегка "оседает" в финальный размер, а не просто проявляется.
        const double startScaleFactor = 0.85;
        var popEase = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 };

        if (duration == TimeSpan.Zero)
        {
            element.Opacity = 1;
            scale.ScaleX = targetScale;
            scale.ScaleY = targetScale;
            return Task.CompletedTask;
        }

        element.Opacity = 0;
        scale.ScaleX = targetScale * startScaleFactor;
        scale.ScaleY = targetScale * startScaleFactor;

        var storyboard = new Storyboard();
        AddTargetedAnimation(storyboard, element, UIElement.OpacityProperty, 0, 1, duration);
        AddTargetedAnimation(storyboard, scale, ScaleTransform.ScaleXProperty, targetScale * startScaleFactor, targetScale, duration, popEase);
        AddTargetedAnimation(storyboard, scale, ScaleTransform.ScaleYProperty, targetScale * startScaleFactor, targetScale, duration, popEase);
        return BeginAsync(storyboard);
    }

    public Task ExitAsync(FrameworkElement element, int durationMs)
    {
        var (scale, _) = EnsureTransforms(element);
        var duration = ScaleDuration(_settingsStore, durationMs);
        var fromScale = scale.ScaleX;

        if (duration == TimeSpan.Zero)
        {
            element.Opacity = 0;
            return Task.CompletedTask;
        }

        var storyboard = new Storyboard();
        AddTargetedAnimation(storyboard, element, UIElement.OpacityProperty, element.Opacity, 0, duration);
        AddTargetedAnimation(storyboard, scale, ScaleTransform.ScaleXProperty, fromScale, fromScale * 0.97, duration);
        AddTargetedAnimation(storyboard, scale, ScaleTransform.ScaleYProperty, fromScale, fromScale * 0.97, duration);
        return BeginAsync(storyboard);
    }

    public Task MoveToAsync(FrameworkElement element, double targetX, double targetY, int durationMs)
    {
        var (_, translate) = EnsureTransforms(element);
        var duration = ScaleDuration(_settingsStore, durationMs);

        if (duration == TimeSpan.Zero)
        {
            translate.X = targetX;
            translate.Y = targetY;
            return Task.CompletedTask;
        }

        var storyboard = new Storyboard();
        AddTargetedAnimation(storyboard, translate, TranslateTransform.XProperty, translate.X, targetX, duration);
        AddTargetedAnimation(storyboard, translate, TranslateTransform.YProperty, translate.Y, targetY, duration);
        return BeginAsync(storyboard);
    }

    public Task ScaleToAsync(FrameworkElement element, double targetScale, int durationMs)
    {
        var (scale, _) = EnsureTransforms(element);
        var duration = ScaleDuration(_settingsStore, durationMs);

        if (duration == TimeSpan.Zero)
        {
            scale.ScaleX = targetScale;
            scale.ScaleY = targetScale;
            return Task.CompletedTask;
        }

        var storyboard = new Storyboard();
        AddTargetedAnimation(storyboard, scale, ScaleTransform.ScaleXProperty, scale.ScaleX, targetScale, duration);
        AddTargetedAnimation(storyboard, scale, ScaleTransform.ScaleYProperty, scale.ScaleY, targetScale, duration);
        return BeginAsync(storyboard);
    }

    public void StartIdleBob(FrameworkElement element)
    {
        if (_settingsStore.Current.AnimationLevel == Models.AnimationLevel.Off)
            return;

        var (_, translate) = EnsureTransforms(element);
        var amplitude = _settingsStore.Current.AnimationLevel == Models.AnimationLevel.Reduced ? 2.5 : 5.0;

        // By (не From/To) — колебание добавляется ОТНОСИТЕЛЬНО текущей базовой линии Y
        // (translate.Y, заданной Enter/MoveTo через VerticalOffset сцены), а не сбрасывает
        // её на 0: иначе idle-дыхание рвало бы вертикальное позиционирование персонажа.
        var animation = new DoubleAnimation
        {
            By = amplitude,
            Duration = TimeSpan.FromSeconds(3.6),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        translate.BeginAnimation(TranslateTransform.YProperty, animation);
    }

    public void StopIdleBob(FrameworkElement element)
    {
        if (element.RenderTransform is not TransformGroup { Children.Count: 2 } group ||
            group.Children[1] is not TranslateTransform translate)
        {
            return;
        }

        translate.BeginAnimation(TranslateTransform.YProperty, null);
    }

    private static (ScaleTransform Scale, TranslateTransform Translate) EnsureTransforms(FrameworkElement element)
    {
        if (element.RenderTransform is TransformGroup { Children.Count: 2 } group &&
            group.Children[0] is ScaleTransform existingScale &&
            group.Children[1] is TranslateTransform existingTranslate)
        {
            return (existingScale, existingTranslate);
        }

        var scale = new ScaleTransform(1, 1);
        var translate = new TranslateTransform(0, 0);

        // Якорь у ног персонажа — масштабирование и сдвиг выглядят естественно для фигуры,
        // стоящей на "полу" сцены, а не растут из центра спрайта.
        element.RenderTransformOrigin = new Point(0.5, 1.0);
        element.RenderTransform = new TransformGroup { Children = { scale, translate } };

        return (scale, translate);
    }
}
