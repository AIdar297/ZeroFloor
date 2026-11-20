using ZeroFloor.Models;

namespace ZeroFloor.Services.Presentation;

/// <summary>Переводит именованные позиции сцены в X-смещение (px) от центра холста 1920×1080.</summary>
internal static class StageLayout
{
    public static double ResolveX(StagePosition position) => position switch
    {
        StagePosition.Left => -560,
        StagePosition.CenterLeft => -280,
        StagePosition.Center => 0,
        StagePosition.CenterRight => 280,
        StagePosition.Right => 560,
        StagePosition.Offscreen => 900,
        _ => 0
    };
}
