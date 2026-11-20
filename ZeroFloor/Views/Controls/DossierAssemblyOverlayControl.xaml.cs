using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ZeroFloor.ViewModels;

namespace ZeroFloor.Views.Controls;

/// <summary>Оверлей «Сборки досье» (Глава 8.2). Вся логика — в DossierAssemblyViewModel; здесь
/// только ввод (клик/перетаскивание), тряска обрывка при ошибке и полоса времени.</summary>
public partial class DossierAssemblyOverlayControl : UserControl
{
    private Point _dragStart;
    private DossierCardViewModel? _dragCard;
    private DossierAssemblyViewModel? _vm;

    public DossierAssemblyOverlayControl()
    {
        InitializeComponent();
        IsVisibleChanged += (_, e) => { if (e.NewValue is true) _ = App.Current.UiReveal.RevealAsync(this); };
        DataContextChanged += (_, _) => Hook();
    }

    private void Hook()
    {
        if (DataContext is NovelViewModel novel)
        {
            novel.PropertyChanged -= OnNovelChanged;
            novel.PropertyChanged += OnNovelChanged;
            Attach(novel.ActiveDossierAssembly);
        }
    }

    private void OnNovelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NovelViewModel.ActiveDossierAssembly) && sender is NovelViewModel novel)
            Attach(novel.ActiveDossierAssembly);
    }

    private void Attach(DossierAssemblyViewModel? vm)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmChanged;
            foreach (var c in _vm.Cards) c.PropertyChanged -= OnCardChanged;
        }
        _vm = vm;
        if (_vm is null) return;
        _vm.PropertyChanged += OnVmChanged;
        foreach (var c in _vm.Cards) c.PropertyChanged += OnCardChanged;
        UpdateTime();
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DossierAssemblyViewModel.TimeFraction))
            UpdateTime();
    }

    private void UpdateTime()
    {
        if (_vm is null) return;
        TimeFill.Width = 360 * _vm.TimeFraction;
        TimeFill.Background = new SolidColorBrush(_vm.TimeFraction < 0.25 ? Color.FromRgb(255, 90, 70) : Color.FromRgb(242, 179, 71));
    }

    /// <summary>Ошибка: обрывок коротко «отскакивает».</summary>
    private void OnCardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DossierCardViewModel.WrongPulse) || sender is not DossierCardViewModel card)
            return;
        var container = FindCardElement(card);
        if (container?.FindName("Shake") is TranslateTransform shake)
        {
            var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(360) };
            foreach (var (t, x) in new[] { (0, 0.0), (60, -12.0), (120, 10.0), (180, -8.0), (240, 5.0), (360, 0.0) })
                a.KeyFrames.Add(new LinearDoubleKeyFrame(x, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(t))));
            shake.BeginAnimation(TranslateTransform.XProperty, a);
        }
    }

    private FrameworkElement? FindCardElement(DossierCardViewModel card)
    {
        foreach (var button in FindVisualChildren<Button>(this))
        {
            if (ReferenceEquals(button.Tag, card) && VisualTreeHelper.GetChildrenCount(button) > 0)
            {
                var presenter = VisualTreeHelper.GetChild(button, 0) as FrameworkElement;
                var grid = presenter is null ? null : FindVisualChildren<Grid>(presenter).FirstOrDefault(g => g.Name == "Card");
                return grid;
            }
        }
        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var nested in FindVisualChildren<T>(child)) yield return nested;
        }
    }

    private void OnCardClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is DossierCardViewModel card)
            _vm?.SelectCard(card);
    }

    private void OnSlotClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is DossierSlotViewModel slot && _vm is not null)
        {
            var selected = _vm.Cards.FirstOrDefault(c => c.IsSelected);
            if (selected is not null) _vm.TryPlace(selected, slot);
        }
    }

    private void OnCardMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragCard = (sender as FrameworkElement)?.Tag as DossierCardViewModel;
    }

    private void OnCardMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCard is null || e.LeftButton != MouseButtonState.Pressed || _vm is null)
            return;
        var delta = e.GetPosition(this) - _dragStart;
        if (Math.Abs(delta.X) < 8 && Math.Abs(delta.Y) < 8)
            return;
        var card = _dragCard;
        _dragCard = null;
        if (!card.IsSelected) _vm.SelectCard(card);
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(DossierCardViewModel), card), DragDropEffects.Move);
    }

    private void OnSlotDrop(object sender, DragEventArgs e)
    {
        SetSlotHover(sender, false);
        if (e.Data.GetData(typeof(DossierCardViewModel)) is DossierCardViewModel card &&
            (sender as FrameworkElement)?.Tag is DossierSlotViewModel slot)
            _vm?.TryPlace(card, slot);
    }

    private void OnSlotDragEnter(object sender, DragEventArgs e) => SetSlotHover(sender, true);
    private void OnSlotDragLeave(object sender, DragEventArgs e) => SetSlotHover(sender, false);

    private static void SetSlotHover(object sender, bool hover)
    {
        if (sender is FrameworkElement fe)
            fe.Opacity = hover ? 0.75 : 1.0;
    }
}
