using System.Windows;
using System.Windows.Controls;

namespace ZeroFloor.Views.Controls;

public partial class ChoiceListControl : UserControl
{
    public ChoiceListControl()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    // Плавное появление списка вариантов выбора (requirement 11) при каждом показе.
    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            _ = App.Current.UiReveal.RevealAsync(this);
    }
}
