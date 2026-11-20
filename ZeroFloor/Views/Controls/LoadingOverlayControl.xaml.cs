using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ZeroFloor.Views.Controls;

public partial class LoadingOverlayControl : UserControl
{
    public LoadingOverlayControl()
    {
        InitializeComponent();
        Loaded += (_, _) => StartSpin();
    }

    private void StartSpin()
    {
        var animation = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.2))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };

        SpinnerRotation.BeginAnimation(RotateTransform.AngleProperty, animation);
    }
}
