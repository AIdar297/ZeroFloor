using System.Windows;
using System.Windows.Controls;

namespace ZeroFloor.Views.Controls;

public partial class StealthGridOverlayControl : UserControl
{
    public StealthGridOverlayControl()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            _ = App.Current.UiReveal.RevealAsync(this);
    }
}
