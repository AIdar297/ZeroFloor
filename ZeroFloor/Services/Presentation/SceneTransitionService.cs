using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static ZeroFloor.Services.Presentation.AnimationPrimitives;

namespace ZeroFloor.Services.Presentation;

public sealed class SceneTransitionService : ISceneTransitionService
{
    private readonly ISettingsStore _settingsStore;

    public SceneTransitionService(ISettingsStore settingsStore) => _settingsStore = settingsStore;

    public Task CrossfadeAsync(FrameworkElement hidingLayer, FrameworkElement revealingLayer, int durationMs)
    {
        var duration = ScaleDuration(_settingsStore, durationMs);

        if (duration == TimeSpan.Zero)
        {
            hidingLayer.Opacity = 0;
            revealingLayer.Opacity = 1;
            return Task.CompletedTask;
        }

        var storyboard = new Storyboard();
        AddTargetedAnimation(storyboard, hidingLayer, UIElement.OpacityProperty, hidingLayer.Opacity, 0, duration);
        AddTargetedAnimation(storyboard, revealingLayer, UIElement.OpacityProperty, revealingLayer.Opacity, 1, duration);
        return BeginAsync(storyboard);
    }

    public Task ZoomAsync(FrameworkElement stage, double targetScale, int durationMs)
    {
        var scale = EnsureScaleTransform(stage);
        var duration = ScaleDuration(_settingsStore, durationMs);

        if (duration == TimeSpan.Zero)
        {
            scale.ScaleX = targetScale;
            scale.ScaleY = targetScale;
            return Task.CompletedTask;
        }

        var storyboard = new Storyboard();
        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
        AddTargetedAnimation(storyboard, scale, ScaleTransform.ScaleXProperty, scale.ScaleX, targetScale, duration, ease);
        AddTargetedAnimation(storyboard, scale, ScaleTransform.ScaleYProperty, scale.ScaleY, targetScale, duration, ease);
        return BeginAsync(storyboard);
    }

    public Task FadeToBlackAsync(FrameworkElement overlay, int durationMs)
    {
        var duration = ScaleDuration(_settingsStore, durationMs);

        if (duration == TimeSpan.Zero)
        {
            overlay.Opacity = 1;
            return Task.CompletedTask;
        }

        var storyboard = new Storyboard();
        AddTargetedAnimation(storyboard, overlay, UIElement.OpacityProperty, overlay.Opacity, 1, duration);
        return BeginAsync(storyboard);
    }

    public Task FadeFromBlackAsync(FrameworkElement overlay, int durationMs)
    {
        var duration = ScaleDuration(_settingsStore, durationMs);

        if (duration == TimeSpan.Zero)
        {
            overlay.Opacity = 0;
            return Task.CompletedTask;
        }

        var storyboard = new Storyboard();
        AddTargetedAnimation(storyboard, overlay, UIElement.OpacityProperty, overlay.Opacity, 0, duration);
        return BeginAsync(storyboard);
    }

    public void StartParallax(FrameworkElement layer, double amplitude = 14)
    {
        if (_settingsStore.Current.AnimationLevel == Models.AnimationLevel.Off)
            return;

        var translate = EnsureTranslateTransform(layer);
        var effectiveAmplitude = _settingsStore.Current.AnimationLevel == Models.AnimationLevel.Reduced
            ? amplitude * 0.4
            : amplitude;

        var animation = new DoubleAnimation
        {
            From = -effectiveAmplitude,
            To = effectiveAmplitude,
            Duration = TimeSpan.FromSeconds(16),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        translate.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    public void StopParallax(FrameworkElement layer)
    {
        if (layer.RenderTransform is TranslateTransform translate)
            translate.BeginAnimation(TranslateTransform.XProperty, null);
    }

    private static ScaleTransform EnsureScaleTransform(FrameworkElement element)
    {
        if (element.RenderTransform is ScaleTransform existing)
            return existing;

        var scale = new ScaleTransform(1, 1);
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = scale;
        return scale;
    }

    private static TranslateTransform EnsureTranslateTransform(FrameworkElement element)
    {
        if (element.RenderTransform is TranslateTransform existing)
            return existing;

        var translate = new TranslateTransform(0, 0);
        element.RenderTransform = translate;
        return translate;
    }
}
