using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZeroFloor.ViewModels;

namespace ZeroFloor.Views.Controls;

public partial class CableDiagnosticsOverlayControl : UserControl
{
    public CableDiagnosticsOverlayControl()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
        PumpButton.PreviewMouseLeftButtonDown += OnPumpButtonPressed;
        PumpButton.PreviewMouseLeftButtonUp += OnPumpButtonReleased;
        PumpButton.LostMouseCapture += OnPumpButtonLostCapture;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            _ = App.Current.UiReveal.RevealAsync(this);
    }

    private void OnPumpButtonPressed(object sender, MouseButtonEventArgs e)
    {
        PumpButton.CaptureMouse();
        (PumpButton.DataContext as CableDiagnosticsViewModel)?.StartPumping();
    }

    private void OnPumpButtonReleased(object sender, MouseButtonEventArgs e)
    {
        if (PumpButton.IsMouseCaptured)
            PumpButton.ReleaseMouseCapture();
    }

    // Единственная точка остановки насоса (а не MouseUp): срабатывает и на обычном отпускании
    // кнопки, и на любой внешней потере захвата мыши (например, Alt+Tab, пока кнопка зажата) —
    // не оставляет насос "залипшим" включённым, если курсор увели с кнопки силой.
    private void OnPumpButtonLostCapture(object sender, MouseEventArgs e)
    {
        (PumpButton.DataContext as CableDiagnosticsViewModel)?.StopPumping();
    }
}
