using System.Windows;

namespace ZeroFloor.Services.Presentation;

/// <summary>
/// Программные анимации персонажа поверх ОДНОГО статичного изображения — требования 1, 2, 3, 6
/// технического задания. Никакой скелетной анимации и смены кадров: только Opacity/Scale/Translate
/// над целым элементом (см. CLAUDE.md про единственное изображение на персонажа).
/// </summary>
public interface ICharacterAnimationService
{
    /// <summary>Появление: мягкий fade + лёгкий "подъём" масштаба до целевого — без скольжения
    /// через весь экран (сценарий "плавающей фотографии" намеренно избегается). targetY —
    /// вертикальная поправка положения (requirement "положение по вертикали", см.
    /// CharacterCue.VerticalOffset); 0 — персонаж стоит на базовой линии сцены.</summary>
    Task EnterAsync(FrameworkElement element, double targetX, double targetY, double targetScale, int durationMs);

    /// <summary>Исчезновение: обратный эффект Enter.</summary>
    Task ExitAsync(FrameworkElement element, int durationMs);

    /// <summary>Плавное перемещение по сцене к новой X/Y-координате.</summary>
    Task MoveToAsync(FrameworkElement element, double targetX, double targetY, int durationMs);

    /// <summary>Плавное изменение масштаба (персонаж "подходит ближе"/"отступает").</summary>
    Task ScaleToAsync(FrameworkElement element, double targetScale, int durationMs);

    /// <summary>Едва заметное бесконечное покачивание вверх-вниз — "персонаж жив", а не статичная
    /// картинка. Работает по оси Y, не конфликтует с MoveTo (ось X) или ScaleTo.</summary>
    void StartIdleBob(FrameworkElement element);

    void StopIdleBob(FrameworkElement element);
}
