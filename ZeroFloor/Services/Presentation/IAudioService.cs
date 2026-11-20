namespace ZeroFloor.Services.Presentation;

/// <summary>
/// Аудиосистема игры: фоновая музыка, звуки окружения, звуковые эффекты, голосовая озвучка
/// реплик — requirements 1–4. Методы принимают только строковые id ресурсов (не
/// FrameworkElement), поэтому, в отличие от ICharacterAnimationService/ISceneTransitionService,
/// этот интерфейс можно безопасно использовать прямо из ViewModel (через AppServices.Audio).
/// </summary>
public interface IAudioService
{
    /// <summary>Музыка — один активный трек с плавной сменой (requirement 6). musicId=null
    /// останавливает музыку (плавным затуханием). Тот же id, что уже играет, — не перезапускает.</summary>
    Task PlayMusicAsync(string? musicId, int crossfadeMs = 1200);

    void StopMusic(int fadeOutMs = 800);

    /// <summary>Эмбиент — та же механика кроссфейда, отдельная категория громкости.</summary>
    Task PlayAmbienceAsync(string? ambienceId, int crossfadeMs = 1200);

    void StopAmbience(int fadeOutMs = 800);

    /// <summary>Одноразовый звуковой эффект — может звучать одновременно с другими SFX
    /// (это не "несовместимые" звуки), количество одновременных экземпляров ограничено.</summary>
    void PlaySfx(string? sfxId);

    /// <summary>
    /// Голос реплики — запускается синхронно с показом текста (вызывающая сторона вызывает
    /// это ровно тогда, когда реплика появляется). Всегда сначала останавливает предыдущую
    /// озвучку — наложение голосов невозможно. voiceId=null или отсутствующий файл — тихо,
    /// без исключения (см. CLAUDE.md: не подменять отсутствующую озвучку случайным звуком).
    /// </summary>
    void PlayVoice(string? voiceId);

    void StopVoice();

    /// <summary>Повторное прослушивание последней запрошенной реплики (та же логика, что и
    /// обычный PlayVoice — без дублирования кода).</summary>
    void ReplayVoice();

    /// <summary>Есть ли у текущей реплики реально существующий файл озвучки — используется
    /// UI, чтобы показывать кнопку "повторить" только когда это осмысленно.</summary>
    bool HasVoiceForCurrentLine { get; }

    /// <summary>Полная тишина — вызывается при выходе из игровой сессии (в главное меню).</summary>
    void StopAll(int fadeOutMs = 400);
}
