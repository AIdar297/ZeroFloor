using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ZeroFloor.ViewModels;

namespace ZeroFloor.Views.Controls;

/// <summary>Оверлей «Сборки разрушенного кода». Логика — в CodeAssemblyViewModel; здесь только
/// анимации: всплывающее сообщение о событии (серия, штраф, перемешивание) и тряска фрагмента
/// при ошибке.</summary>
public partial class CodeAssemblyOverlayControl : UserControl
{
    private NovelViewModel? _novel;
    private CodeAssemblyViewModel? _vm;

    public CodeAssemblyOverlayControl()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
        DataContextChanged += (_, _) => Hook();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            _ = App.Current.UiReveal.RevealAsync(this);
    }

    private void Hook()
    {
        if (_novel is not null)
            _novel.PropertyChanged -= OnNovelChanged;
        _novel = DataContext as NovelViewModel;
        if (_novel is not null)
            _novel.PropertyChanged += OnNovelChanged;
        Attach(_novel?.ActiveCodeAssembly);
    }

    private void OnNovelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NovelViewModel.ActiveCodeAssembly))
            Attach(_novel?.ActiveCodeAssembly);
    }

    private void Attach(CodeAssemblyViewModel? vm)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmChanged;
            foreach (var f in _vm.Fragments) f.PropertyChanged -= OnFragmentChanged;
        }
        _vm = vm;
        StatusLine.BeginAnimation(OpacityProperty, null);
        StatusLine.Opacity = 0;
        if (_vm is null) return;
        _vm.PropertyChanged += OnVmChanged;
        foreach (var f in _vm.Fragments) f.PropertyChanged += OnFragmentChanged;
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CodeAssemblyViewModel.StatusPulse))
            return;
        var a = new DoubleAnimationUsingKeyFrames();
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1300))));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1800))));
        StatusLine.BeginAnimation(OpacityProperty, a);
    }

    private void OnFragmentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CodeFragmentViewModel.WrongPulse) || sender is not CodeFragmentViewModel fragment)
            return;
        if (FragmentPool.ItemContainerGenerator.ContainerFromItem(fragment) is not DependencyObject container)
            return;
        var button = FindChild<Button>(container);
        if (button is null) return;
        var shake = new TranslateTransform();
        button.RenderTransform = shake;
        var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(380) };
        foreach (var (t, x) in new[] { (0, 0.0), (60, -10.0), (120, 9.0), (190, -6.0), (260, 4.0), (380, 0.0) })
            a.KeyFrames.Add(new LinearDoubleKeyFrame(x, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(t))));
        shake.BeginAnimation(TranslateTransform.XProperty, a);
    }

    private static T? FindChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) return t;
            var nested = FindChild<T>(child);
            if (nested is not null) return nested;
        }
        return null;
    }
}
