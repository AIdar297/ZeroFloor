using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZeroFloor.ViewModels;

namespace ZeroFloor.Views.Controls;

public partial class RingLockOverlayControl : UserControl
{
    public RingLockOverlayControl()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;

        RotateLeftButton.PreviewMouseLeftButtonDown += (_, _) => BeginRotate(RotateLeftButton, -1);
        RotateLeftButton.PreviewMouseLeftButtonUp += (_, _) => EndRotate(RotateLeftButton);
        RotateLeftButton.LostMouseCapture += (_, _) => StopRotating();

        RotateRightButton.PreviewMouseLeftButtonDown += (_, _) => BeginRotate(RotateRightButton, 1);
        RotateRightButton.PreviewMouseLeftButtonUp += (_, _) => EndRotate(RotateRightButton);
        RotateRightButton.LostMouseCapture += (_, _) => StopRotating();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            _ = App.Current.UiReveal.RevealAsync(this);
    }

    private void BeginRotate(Button button, int direction)
    {
        button.CaptureMouse();
        var vm = button.DataContext as RingLockViewModel;
        if (direction < 0)
            vm?.StartRotatingLeft();
        else
            vm?.StartRotatingRight();
    }

    private static void EndRotate(Button button)
    {
        if (button.IsMouseCaptured)
            button.ReleaseMouseCapture();
    }

    private void StopRotating() => (RotateLeftButton.DataContext as RingLockViewModel)?.StopRotating();
}
