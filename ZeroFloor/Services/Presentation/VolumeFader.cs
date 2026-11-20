namespace ZeroFloor.Services.Presentation;

/// <summary>
/// Плавно изменяет громкость во времени (requirement 5, 6: появление/затухание звука,
/// смена музыки) — без WPF Storyboard, т.к. System.Windows.Media.MediaPlayer.Volume не
/// является DependencyProperty и не может быть анимирован штатным Storyboard/BeginAnimation.
/// Каждый BeginAnimation этого фейдера отменяет предыдущий — повторный вызов (например,
/// быстрая смена трека) просто продолжает с текущего значения, не накапливая обработчики.
/// </summary>
internal sealed class VolumeFader
{
    private CancellationTokenSource? _cts;

    public async Task FadeAsync(double from, double to, int durationMs, Action<double> setVolume)
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        var token = cts.Token;

        if (durationMs <= 0 || Math.Abs(from - to) < 0.0001)
        {
            setVolume(to);
            return;
        }

        const int stepMs = 16;
        var steps = Math.Max(1, durationMs / stepMs);

        for (var i = 1; i <= steps; i++)
        {
            try
            {
                await Task.Delay(stepMs, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (token.IsCancellationRequested)
                return;

            var progress = (double)i / steps;
            setVolume(from + (to - from) * progress);
        }
    }

    public void Cancel() => _cts?.Cancel();
}
