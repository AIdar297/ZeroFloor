using System.Windows;

namespace ZeroFloor.Services.Presentation;

/// <summary>
/// Единообразное "плавное появление" для панелей интерфейса — окно диалога, список выбора,
/// HUD (requirements 10, 11). Не привязано к сценарным данным: это постоянное, предсказуемое
/// оформление показа/скрытия панели, а не режиссёрская ремарка конкретного узла.
/// </summary>
public interface IUiRevealService
{
    /// <summary>Лёгкий fade + едва заметный подъём снизу — не "выезжающее окно", а мягкое проявление.</summary>
    Task RevealAsync(FrameworkElement element, int durationMs = 420);

    /// <summary>Обратный эффект RevealAsync — лёгкий fade + едва заметное опускание вниз. Используется
    /// перед сменой содержимого панели (напр. окна реплики на новый узел), чтобы переход между
    /// репликами не был мгновенной подменой текста, а читался как уход старой реплики и приход новой.</summary>
    Task HideAsync(FrameworkElement element, int durationMs = 260);
}
