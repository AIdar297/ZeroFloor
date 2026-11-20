using System.Windows;

namespace ZeroFloor.Services.Presentation;

/// <summary>
/// Переходы на уровне всей сцены — требования 4, 5, 7, 8, 9 технического задания: приближение/
/// отдаление камеры, затемнение/осветление экрана, кроссфейд фонов, лёгкий параллакс.
/// </summary>
public interface ISceneTransitionService
{
    /// <summary>Кроссфейд двух фоновых слоёв: hidingLayer гаснет, revealingLayer проявляется —
    /// одновременно, одним Storyboard.</summary>
    Task CrossfadeAsync(FrameworkElement hidingLayer, FrameworkElement revealingLayer, int durationMs);

    /// <summary>Лёгкое кинематографичное приближение/отдаление всей сцены (камера, не персонаж).
    /// targetScale — итоговый масштаб; 1.0 = обычный план.</summary>
    Task ZoomAsync(FrameworkElement stage, double targetScale, int durationMs);

    Task FadeToBlackAsync(FrameworkElement overlay, int durationMs);

    Task FadeFromBlackAsync(FrameworkElement overlay, int durationMs);

    /// <summary>Едва заметный бесконечный дрейф фона — ощущение глубины, а не перемещение.</summary>
    void StartParallax(FrameworkElement layer, double amplitude = 14);

    void StopParallax(FrameworkElement layer);
}
