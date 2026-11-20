using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ZeroFloor.Models;
using ZeroFloor.Services.Presentation.World3D;
using ZeroFloor.ViewModels;

namespace ZeroFloor.Views.Controls;

/// <summary>
/// Хост интерактивных 3D-эпизодов: Viewport3D + минималистичный HUD + меню паузы. Создаёт
/// эпизод (MarketStealthEpisode / ShaftsChaseEpisode) при появлении NovelViewModel.ActiveEpisode
/// и уничтожает при его исчезновении/выгрузке экрана. Реализует IEpisodeHud — эпизод сообщает
/// данные, а стиль и анимации интерфейса живут здесь.
/// </summary>
public partial class Episode3DOverlayControl : UserControl, IEpisodeHud
{
    private const double BarWidth = 292;
    private NovelViewModel? _viewModel;
    private EpisodeRuntime? _runtime;
    private Episode3DViewModel? _episode;
    private bool _hudVisible;

    public Episode3DOverlayControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => DestroyRuntime();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = e.NewValue as NovelViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SyncEpisode();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NovelViewModel.ActiveEpisode))
            SyncEpisode();
    }

    private void SyncEpisode()
    {
        var episode = _viewModel?.ActiveEpisode;
        if (ReferenceEquals(episode, _episode))
            return;

        DestroyRuntime();
        _episode = episode;
        if (episode is null)
            return;

        if (episode is INotifyPropertyChanged npc)
            npc.PropertyChanged += OnEpisodeSettingChanged;

        ResetHud();
        var settings = App.Current.Settings;
        _runtime = episode.Kind switch
        {
            MinigameKind.MarketStealth3D => new MarketStealthEpisode(Viewport, this, episode, settings),
            MinigameKind.ArchiveStealth3D => new ArchiveStealthEpisode(Viewport, this, episode, settings),
            _ => new ShaftsChaseEpisode(Viewport, this, episode, settings)
        };
        _runtime.HostWindow = Window.GetWindow(this);
        _runtime.Start();
        Focus();
    }

    private void DestroyRuntime()
    {
        if (_episode is INotifyPropertyChanged npc)
            npc.PropertyChanged -= OnEpisodeSettingChanged;
        _runtime?.Dispose();
        _runtime = null;
        _episode = null;
        Viewport.Children.Clear();
        PausePanel.Visibility = Visibility.Collapsed;
    }

    private void OnEpisodeSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Episode3DViewModel.ShowControlHints))
            ApplyHintsVisibility();
    }

    private void OnResumeClick(object sender, RoutedEventArgs e) => _runtime?.SetPaused(false);

    private void ResetHud()
    {
        FadeOverlay.Opacity = 1;
        AlarmVignette.Opacity = 0;
        DamageFlash.Opacity = 0;
        BannerPanel.Opacity = 0;
        ToastText.Opacity = 0;
        WarningPanel.Visibility = Visibility.Collapsed;
        PromptPanel.Visibility = Visibility.Collapsed;
        PausePanel.Visibility = Visibility.Collapsed;
        HudRoot.Opacity = 0;
    }

    private void ApplyHintsVisibility() =>
        ControlsList.Visibility = App.Current.Settings.Current.EpisodeShowControlHints ? Visibility.Visible : Visibility.Collapsed;

    // ------------------------------------------------------------------ IEpisodeHud

    public void SetObjective(string title, string detail)
    {
        ObjectiveTitle.Text = title;
        ObjectiveDetail.Text = detail;
        Pulse(ObjectiveTitle);
    }

    public void SetControls(IReadOnlyList<(string Key, string Label)> controls)
    {
        ControlsList.ItemsSource = controls.Select(c => new { c.Key, c.Label }).ToList();
        ApplyHintsVisibility();
    }

    public void SetInteractPrompt(string? text)
    {
        if (text is null)
        {
            PromptPanel.Visibility = Visibility.Collapsed;
            return;
        }
        PromptText.Text = text.StartsWith("E — ", StringComparison.Ordinal) ? text[4..] : text;
        PromptPanel.Visibility = Visibility.Visible;
    }

    public void SetDetection(double? value, MeterLevel level, string label)
    {
        DetectionPanel.Visibility = value is null ? Visibility.Collapsed : Visibility.Visible;
        if (value is null) return;
        DetectionFill.Width = BarWidth * Math.Clamp(value.Value, 0, 1);
        DetectionFill.Background = new SolidColorBrush(level switch
        {
            MeterLevel.Calm => Color.FromRgb(79, 209, 166),
            MeterLevel.Suspicious => Color.FromRgb(242, 179, 71),
            _ => Color.FromRgb(255, 74, 61)
        });
        DetectionLabel.Text = label;
    }

    public void SetPursuit(double? closeness, string label)
    {
        PursuitPanel.Visibility = closeness is null ? Visibility.Collapsed : Visibility.Visible;
        if (closeness is null) return;
        PursuitFill.Width = BarWidth * Math.Clamp(closeness.Value, 0, 1);
        PursuitLabel.Text = label;
    }

    public void SetVitals(double? health)
    {
        VitalsPanel.Visibility = health is null ? Visibility.Collapsed : Visibility.Visible;
        if (health is null) return;
        VitalsFill.Width = 240 * Math.Clamp(health.Value, 0, 1);
        VitalsFill.Background = new SolidColorBrush(health < 0.5 ? Color.FromRgb(255, 120, 90) : Color.FromRgb(232, 238, 244));
    }

    public void SetWarning(string? text)
    {
        if (text is null)
        {
            WarningPanel.Visibility = Visibility.Collapsed;
            return;
        }
        WarningText.Text = text;
        WarningPanel.Visibility = Visibility.Visible;
        var pulse = new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(420)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
        WarningPanel.BeginAnimation(OpacityProperty, pulse);
    }

    public void Toast(string text)
    {
        ToastText.Text = text;
        var a = new DoubleAnimationUsingKeyFrames();
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2800))));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(3400))));
        ToastText.BeginAnimation(OpacityProperty, a);
    }

    public void Banner(string title, string subtitle)
    {
        BannerTitle.Text = title;
        BannerSubtitle.Text = subtitle;
        var a = new DoubleAnimationUsingKeyFrames();
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(450)), new CubicEase()));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2800))));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(3600)), new CubicEase()));
        BannerPanel.BeginAnimation(OpacityProperty, a);
    }

    public void SetAlarm(double intensity) => AlarmVignette.Opacity = Math.Clamp(intensity, 0, 1) * 0.75;

    public void FlashDamage() =>
        DamageFlash.BeginAnimation(OpacityProperty, new DoubleAnimation(0.85, 0, TimeSpan.FromMilliseconds(650)));

    public void SetFade(double opacity) => FadeOverlay.Opacity = Math.Clamp(opacity, 0, 1);

    public void SetHudVisible(bool visible)
    {
        if (_hudVisible == visible) return;
        _hudVisible = visible;
        HudRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(visible ? 1 : 0, TimeSpan.FromMilliseconds(visible ? 500 : 300)));
    }

    public void ShowPause(bool visible)
    {
        PausePanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (visible)
            PausePanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
    }

    private static void Pulse(UIElement element) =>
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(500)));
}
