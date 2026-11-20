using System.Windows.Media;

namespace ZeroFloor.Services.Presentation;

/// <summary>
/// Один "слот" зацикленного звука (музыка ИЛИ эмбиент — используется для обеих категорий,
/// чтобы не дублировать логику кроссфейда) с двумя плеерами для мягкого перехода между
/// треками: пока новый плавно нарастает, старый плавно затухает — в любой момент времени
/// слышен только ОДИН трек этой категории (requirement: отсутствие наложения несовместимых
/// звуков внутри одной категории).
/// </summary>
internal sealed class AudioChannel
{
    public MediaPlayer PlayerA { get; } = new();
    public MediaPlayer PlayerB { get; } = new();
    public VolumeFader FaderOutgoing { get; } = new();
    public VolumeFader FaderIncoming { get; } = new();

    public bool ActiveIsA { get; set; } = true;
    public string? CurrentResourceId { get; set; }

    public MediaPlayer Active => ActiveIsA ? PlayerA : PlayerB;
    public MediaPlayer Inactive => ActiveIsA ? PlayerB : PlayerA;
}
