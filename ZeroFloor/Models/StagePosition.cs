namespace ZeroFloor.Models;

/// <summary>Именованные позиции персонажа на сцене — сцена спроектирована под холст 1920×1080
/// (см. MainWindow.xaml), конкретные пиксельные координаты считает Presentation-слой.</summary>
public enum StagePosition
{
    Left,
    CenterLeft,
    Center,
    CenterRight,
    Right,
    Offscreen
}
