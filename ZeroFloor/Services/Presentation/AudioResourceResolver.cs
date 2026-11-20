using System.IO;

namespace ZeroFloor.Services.Presentation;

/// <summary>
/// Система именования и хранения аудиоресурсов: каждая категория — своя папка под
/// Assets/Audio/, ресурс называется без расширения и может содержать "/" для подпапок
/// (например, voiceClip = "max/ch01_n002" → Assets/Audio/Voice/max/ch01_n002.wav).
/// Расширение подбирается автоматически из списка поддерживаемых Windows Media Foundation
/// форматов. Если файла нет — возвращает null, а не бросает исключение (см. CLAUDE.md:
/// отсутствующий ассет не должен ронять игру и не должен подменяться случайным файлом).
/// </summary>
internal static class AudioResourceResolver
{
    private static readonly string[] KnownExtensions = { ".wav", ".mp3", ".wma" };

    public static string? Resolve(AudioCategory category, string? resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            return null;

        var categoryFolder = category switch
        {
            AudioCategory.Music => "Music",
            AudioCategory.Ambience => "Ambience",
            AudioCategory.Sfx => "SFX",
            AudioCategory.Voice => "Voice",
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
        };

        var baseDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", categoryFolder);
        var relativePath = resourceId.Replace('/', Path.DirectorySeparatorChar);

        foreach (var extension in KnownExtensions)
        {
            var candidate = Path.Combine(baseDirectory, relativePath + extension);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
