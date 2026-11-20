using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ZeroFloor.Models;

namespace ZeroFloor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml. Применяет настройки полноэкранного режима и
    /// разрешения окна (requirement "настройки: полноэкранный режим, разрешение окна, если
    /// поддерживается") — весь интерфейс спроектирован на холсте 1920x1080 и масштабируется
    /// Viewbox'ом целиком (см. MainWindow.xaml), поэтому любое из перечисленных разрешений
    /// поддерживается без искажений. Код здесь, а не в ViewModel — это WPF-специфичное
    /// управление окном, тот же принцип, что у NovelView.xaml.cs (презентационный слой читает
    /// сервисы через App.Current напрямую, см. TECHNICAL_ARCHITECTURE.md, раздел 2).
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyWindowSettings(App.Current.Settings.Current);
            App.Current.Settings.Changed += OnSettingsChanged;
        }

        private void OnClosed(object? sender, EventArgs e) => App.Current.Settings.Changed -= OnSettingsChanged;

        private void OnSettingsChanged(object? sender, EventArgs e) => ApplyWindowSettings(App.Current.Settings.Current);

        private void ApplyWindowSettings(AppSettings settings)
        {
            if (settings.IsFullscreen)
            {
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Maximized;
                return;
            }

            if (WindowStyle != WindowStyle.SingleBorderWindow)
            {
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanResize;
            }

            var (width, height) = ResolveResolution(settings.WindowResolution);
            Width = width;
            Height = height;
        }

        private static (double Width, double Height) ResolveResolution(WindowResolution resolution) => resolution switch
        {
            WindowResolution.Hd1280x720 => (1280, 720),
            WindowResolution.FullHd1920x1080 => (1920, 1080),
            _ => (1600, 900)
        };
    }
}