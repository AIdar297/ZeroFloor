using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static ZeroFloor.Services.Presentation.AnimationPrimitives;

namespace ZeroFloor.Services.Presentation;

public sealed class UiRevealService : IUiRevealService
{
    private readonly ISettingsStore _settingsStore;

    public UiRevealService(ISettingsStore settingsStore) => _settingsStore = settingsStore;

    public Task RevealAsync(FrameworkElement element, int durationMs = 420)
    {
        var duration = ScaleDuration(_settingsStore, durationMs);
        var translate = EnsureTranslateTransform(element);

        if (duration == TimeSpan.Zero)
        {
            element.Opacity = 1;
            translate.Y = 0;
            return Task.CompletedTask;
        }

        element.Opacity = 0;
        translate.Y = 16;

        var storyboard = new Storyboard();
        AddTargetedAnimation(storyboard, element, UIElement.OpacityProperty, 0, 1, duration);
        AddTargetedAnimation(storyboard, translate, TranslateTransform.YProperty, 16, 0, duration);
        return BeginAsync(storyboard);
    }

    public Task HideAsync(FrameworkElement element, int durationMs = 260)
    {
        var duration = ScaleDuration(_settingsStore, durationMs);
        var translate = EnsureTranslateTransform(element);

        if (duration == TimeSpan.Zero)
        {
            element.Opacity = 0;
            translate.Y = 16;
            return Task.CompletedTask;
        }

        var storyboard = new Storyboard();
        AddTargetedAnimation(storyboard, element, UIElement.OpacityProperty, element.Opacity, 0, duration);
        AddTargetedAnimation(storyboard, translate, TranslateTransform.YProperty, translate.Y, 16, duration);
        return BeginAsync(storyboard);
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
