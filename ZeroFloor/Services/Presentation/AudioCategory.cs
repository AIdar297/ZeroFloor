namespace ZeroFloor.Services.Presentation;

/// <summary>Пять категорий громкости по заданию: Master — общий множитель поверх всех
/// остальных, плюс четыре независимые категории.</summary>
public enum AudioCategory
{
    Music,
    Ambience,
    Sfx,
    Voice
}
