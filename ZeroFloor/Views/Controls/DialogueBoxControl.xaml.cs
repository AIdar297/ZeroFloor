using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroFloor.ViewModels;

namespace ZeroFloor.Views.Controls;

public partial class DialogueBoxControl : UserControl
{
    public DialogueBoxControl()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
        Loaded += (_, _) => ((System.Windows.Media.Animation.Storyboard)Resources["Bounce"]).Begin(this, true);
    }

    /// <summary>
    /// Красит панель реплики в цвета, подстроенные под фон текущей сцены (не фиксированный
    /// цвет для любой сцены) — NovelView вычисляет средний цвет фона и передаёт сюда уже готовую
    /// пару "верх/низ" для градиента; сама панель ничего не знает про фоны/ассеты.
    /// </summary>
    public void SetSceneTint(Color topColor, Color bottomColor)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        brush.GradientStops.Add(new GradientStop(topColor, 0));
        brush.GradientStops.Add(new GradientStop(bottomColor, 1));
        brush.Freeze();
        // подкраска ложится полупрозрачным слоем поверх стеклянной панели, не заменяя её
        TintLayer.Background = brush;
    }

    private void OnDialogueBoxClicked(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is NovelViewModel viewModel && viewModel.ContinueCommand.CanExecute(null))
        {
            viewModel.ContinueCommand.Execute(null);
        }
    }

    // Плавное появление окна диалога (requirement 10) — срабатывает при каждом переключении
    // Visibility (IsUiHidden toggle), не только при первой загрузке контрола.
    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            _ = App.Current.UiReveal.RevealAsync(this);
    }
}
