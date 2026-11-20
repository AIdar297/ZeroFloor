using System.Windows;
using System.Windows.Media.Animation;
using ZeroFloor.Models;

namespace ZeroFloor.Services.Presentation;

/// <summary>
/// Общая "сантехника" для WPF-анимаций (единственное место, где это пишется — остальные
/// Presentation-сервисы собирают из этих кирпичиков свои конкретные эффекты, не дублируя
/// работу с Storyboard/TaskCompletionSource/шкалированием длительности по настройкам).
/// </summary>
internal static class AnimationPrimitives
{
    /// <summary>Длительность с поправкой на AppSettings.AnimationLevel — TimeSpan.Zero означает
    /// "анимации выключены", вызывающий код обязан в этом случае применить конечное состояние
    /// напрямую и не создавать Storyboard вовсе.</summary>
    public static TimeSpan ScaleDuration(ISettingsStore settingsStore, int baseDurationMs) =>
        settingsStore.Current.AnimationLevel switch
        {
            AnimationLevel.Off => TimeSpan.Zero,
            AnimationLevel.Reduced => TimeSpan.FromMilliseconds(Math.Max(1, baseDurationMs * 0.35)),
            _ => TimeSpan.FromMilliseconds(Math.Max(1, baseDurationMs))
        };

    public static IEasingFunction DefaultEase() => new CubicEase { EasingMode = EasingMode.EaseInOut };

    public static void AddTargetedAnimation(
        Storyboard storyboard,
        DependencyObject target,
        DependencyProperty property,
        double from,
        double to,
        TimeSpan duration,
        IEasingFunction? easing = null)
    {
        var animation = new DoubleAnimation(from, to, duration) { EasingFunction = easing ?? DefaultEase() };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, new PropertyPath(property));
        storyboard.Children.Add(animation);
    }

    /// <summary>Запускает Storyboard и возвращает Task, завершающийся по событию Completed —
    /// не блокирует вызывающий поток (диспетчер WPF остаётся отзывчивым всё это время).</summary>
    public static Task BeginAsync(Storyboard storyboard)
    {
        var completionSource = new TaskCompletionSource();

        void OnCompleted(object? sender, EventArgs e)
        {
            storyboard.Completed -= OnCompleted;
            completionSource.TrySetResult();
        }

        storyboard.Completed += OnCompleted;
        storyboard.Begin();
        return completionSource.Task;
    }
}
