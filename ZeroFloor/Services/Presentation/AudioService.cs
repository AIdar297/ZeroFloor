using System.Windows.Media;
using ZeroFloor.Services;

namespace ZeroFloor.Services.Presentation;

public sealed class AudioService : IAudioService, IDisposable
{
    private const int MaxConcurrentSfx = 8;

    private readonly ISettingsStore _settingsStore;
    private readonly AudioChannel _musicChannel = new();
    private readonly AudioChannel _ambienceChannel = new();
    private readonly MediaPlayer _voicePlayer = new();
    private readonly List<MediaPlayer> _activeSfx = new();

    private string? _currentVoiceId;

    public AudioService(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        _settingsStore.Changed += OnSettingsChanged;

        _musicChannel.PlayerA.MediaEnded += OnLoopEnded;
        _musicChannel.PlayerB.MediaEnded += OnLoopEnded;
        _ambienceChannel.PlayerA.MediaEnded += OnLoopEnded;
        _ambienceChannel.PlayerB.MediaEnded += OnLoopEnded;
    }

    public bool HasVoiceForCurrentLine =>
        _currentVoiceId is not null && AudioResourceResolver.Resolve(AudioCategory.Voice, _currentVoiceId) is not null;

    public Task PlayMusicAsync(string? musicId, int crossfadeMs = 1200) =>
        CrossfadeChannelAsync(_musicChannel, AudioCategory.Music, musicId, crossfadeMs);

    public void StopMusic(int fadeOutMs = 800) => _ = CrossfadeChannelAsync(_musicChannel, AudioCategory.Music, null, fadeOutMs);

    public Task PlayAmbienceAsync(string? ambienceId, int crossfadeMs = 1200) =>
        CrossfadeChannelAsync(_ambienceChannel, AudioCategory.Ambience, ambienceId, crossfadeMs);

    public void StopAmbience(int fadeOutMs = 800) => _ = CrossfadeChannelAsync(_ambienceChannel, AudioCategory.Ambience, null, fadeOutMs);

    public void PlaySfx(string? sfxId)
    {
        var path = AudioResourceResolver.Resolve(AudioCategory.Sfx, sfxId);
        if (path is null)
            return; // нет ресурса или файла — тихо продолжаем работу (CLAUDE.md)

        if (_activeSfx.Count >= MaxConcurrentSfx)
        {
            var oldest = _activeSfx[0];
            _activeSfx.RemoveAt(0);
            oldest.Stop();
            oldest.Close();
        }

        var player = new MediaPlayer();

        try
        {
            player.Open(new Uri(path, UriKind.Absolute));
        }
        catch
        {
            return; // повреждённый/недоступный файл — не роняем игру
        }

        player.Volume = EffectiveVolume(AudioCategory.Sfx);
        player.MediaEnded += OnSfxFinished;
        player.MediaFailed += OnSfxFinished;
        _activeSfx.Add(player);
        player.Play();

        void OnSfxFinished(object? sender, EventArgs e)
        {
            player.MediaEnded -= OnSfxFinished;
            player.MediaFailed -= OnSfxFinished;
            player.Close();
            _activeSfx.Remove(player);
        }
    }

    public void PlayVoice(string? voiceId)
    {
        // Требование: предыдущая реплика всегда останавливается — наложение голосов исключено.
        _voicePlayer.Stop();
        _currentVoiceId = voiceId;

        var path = AudioResourceResolver.Resolve(AudioCategory.Voice, voiceId);
        if (path is null)
            return; // нет озвучки для этой реплики (или файл отсутствует) — тишина, не ошибка

        try
        {
            _voicePlayer.Open(new Uri(path, UriKind.Absolute));
        }
        catch
        {
            return;
        }

        _voicePlayer.Volume = EffectiveVolume(AudioCategory.Voice);
        _voicePlayer.Play();
    }

    public void StopVoice() => _voicePlayer.Stop();

    public void ReplayVoice()
    {
        if (_currentVoiceId is not null)
            PlayVoice(_currentVoiceId); // та же логика, что и обычный запуск — без дублирования
    }

    public void StopAll(int fadeOutMs = 400)
    {
        StopMusic(fadeOutMs);
        StopAmbience(fadeOutMs);
        StopVoice();

        foreach (var player in _activeSfx.ToArray())
        {
            player.Stop();
            player.Close();
        }
        _activeSfx.Clear();
    }

    public void Dispose()
    {
        _settingsStore.Changed -= OnSettingsChanged;

        _musicChannel.PlayerA.Close();
        _musicChannel.PlayerB.Close();
        _ambienceChannel.PlayerA.Close();
        _ambienceChannel.PlayerB.Close();
        _voicePlayer.Close();

        foreach (var player in _activeSfx)
            player.Close();
        _activeSfx.Clear();
    }

    private async Task CrossfadeChannelAsync(AudioChannel channel, AudioCategory category, string? resourceId, int crossfadeMs)
    {
        if (resourceId == channel.CurrentResourceId)
            return; // тот же ресурс уже играет (или уже тихо) — ничего не делаем

        channel.CurrentResourceId = resourceId;

        var outgoing = channel.Active;
        var outgoingFade = channel.FaderOutgoing.FadeAsync(outgoing.Volume, 0, crossfadeMs, v => outgoing.Volume = v);
        var incomingFade = Task.CompletedTask;

        var path = AudioResourceResolver.Resolve(category, resourceId);

        if (path is not null)
        {
            var incoming = channel.Inactive;

            try
            {
                incoming.Open(new Uri(path, UriKind.Absolute));
                incoming.Volume = 0;
                incoming.Play();

                // Новый плеер становится "активным" сразу — любые StopXxx/изменения громкости,
                // пришедшие прямо во время кроссфейда, адресуются уже ему, а не уходящему треку.
                channel.ActiveIsA = !channel.ActiveIsA;

                var targetVolume = EffectiveVolume(category);
                incomingFade = channel.FaderIncoming.FadeAsync(0, targetVolume, crossfadeMs, v => incoming.Volume = v);
            }
            catch
            {
                // Файл повреждён/недоступен — просто не проигрываем, без краша (CLAUDE.md).
            }
        }

        await Task.WhenAll(outgoingFade, incomingFade);
        outgoing.Stop();
    }

    private void OnLoopEnded(object? sender, EventArgs e)
    {
        if (sender is not MediaPlayer player)
            return;

        player.Position = TimeSpan.Zero;
        player.Play();
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Громкость применяется к активным плеерам немедленно — пока нет собственного fade
        // "поверх" этого вызова, прямое присваивание безопасно (см. VolumeFader — комментарий
        // в AudioChannel). Голос/SFX тоже подхватывают новое значение со следующего проигрывания.
        _musicChannel.Active.Volume = EffectiveVolume(AudioCategory.Music);
        _ambienceChannel.Active.Volume = EffectiveVolume(AudioCategory.Ambience);

        if (_voicePlayer.Source is not null)
            _voicePlayer.Volume = EffectiveVolume(AudioCategory.Voice);
    }

    private double EffectiveVolume(AudioCategory category)
    {
        var settings = _settingsStore.Current;

        var categoryVolume = category switch
        {
            AudioCategory.Music => settings.MusicVolume,
            AudioCategory.Ambience => settings.AmbienceVolume,
            AudioCategory.Sfx => settings.SfxVolume,
            AudioCategory.Voice => settings.VoiceVolume,
            _ => 1.0
        };

        return Math.Clamp(settings.MasterVolume * categoryVolume, 0, 1);
    }
}
