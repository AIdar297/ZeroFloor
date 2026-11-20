using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroFloor.ViewModels;

namespace ZeroFloor.Views;

public partial class MainMenuView : UserControl
{
    public MainMenuView()
    {
        InitializeComponent();
    }

    /// <summary>Главное меню — требование "главное меню должно использовать отдельное
    /// фоновое изображение": если backgroundId "main_menu" зарегистрирован и файл читается,
    /// показываем его (UniformToFill — без растягивания/искажений) и прячем декоративные
    /// линии-заглушки; иначе экран остаётся как был (тот же плейсхолдер, что и раньше).
    /// Первый пункт меню предподсвечен сразу при входе (как на референсе) — обычный
    /// клавиатурный фокус, подсветка та же, что и при наведении мышью (см.
    /// MainMenuButtonStyle, триггер IsKeyboardFocused).</summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NewGameButton.Focus();

        if (DataContext is not MainMenuViewModel viewModel)
            return;

        var image = viewModel.Assets.ResolveBackgroundImage("main_menu");
        if (image is null)
            return;

        RootGrid.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
        DecorCanvas.Visibility = Visibility.Collapsed;

        // Референс уже несёт собственный логотип-заголовок в кадре — не дублируем его своим
        // текстом; тёмная подложка (MenuScrim) отделяет настоящие, кликабельные пункты меню
        // от любого текста, нарисованного на самом изображении фона.
        TitleText.Visibility = Visibility.Collapsed;
        SubtitleText.Visibility = Visibility.Collapsed;
        MenuScrim.Opacity = 1;
    }
}
