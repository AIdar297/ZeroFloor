using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace ZeroFloor.Services.Presentation.World3D;

/// <summary>
/// Опрос клавиатуры/мыши раз в кадр. Клавиши читаются через Keyboard.IsKeyDown (не через
/// события) — эпизоду не важно, у какого элемента сейчас фокус; «нажатия» (прыжок, E, Esc,
/// присед) выделяются по фронту. Мышь — относительный обзор: курсор скрыт и возвращается в
/// центр окна после каждого кадра (пока игра не на паузе и окно активно).
/// Может работать в скриптовом режиме (Scripted) — значения задаёт автопилот, а не устройство.
/// </summary>
public sealed class EpisodeInput
{
    private bool _prevJump, _prevInteract, _prevPause, _prevCrouch;
    private bool _mouseCaptured;

    public bool Scripted { get; set; }

    public bool Forward { get; set; }
    public bool Back { get; set; }
    public bool Left { get; set; }
    public bool Right { get; set; }
    public bool Sprint { get; set; }
    public bool Crouch { get; set; }
    public bool Jump { get; set; }
    public bool Interact { get; set; }
    public bool Pause { get; set; }

    public bool JumpPressed { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool PausePressed { get; private set; }
    public bool CrouchPressed { get; private set; }

    public double MouseDx { get; private set; }
    public double MouseDy { get; private set; }

    public double MoveX => (Right ? 1 : 0) - (Left ? 1 : 0);
    public double MoveY => (Forward ? 1 : 0) - (Back ? 1 : 0);
    public bool HasMove => MoveX != 0 || MoveY != 0;

    public void Poll(Window? window, bool captureMouse)
    {
        if (!Scripted)
        {
            Forward = Keyboard.IsKeyDown(Key.W) || Keyboard.IsKeyDown(Key.Up);
            Back = Keyboard.IsKeyDown(Key.S) || Keyboard.IsKeyDown(Key.Down);
            Left = Keyboard.IsKeyDown(Key.A) || Keyboard.IsKeyDown(Key.Left);
            Right = Keyboard.IsKeyDown(Key.D) || Keyboard.IsKeyDown(Key.Right);
            Sprint = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            Crouch = Keyboard.IsKeyDown(Key.C) || Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            Jump = Keyboard.IsKeyDown(Key.Space);
            Interact = Keyboard.IsKeyDown(Key.E);
            Pause = Keyboard.IsKeyDown(Key.Escape);
        }

        JumpPressed = Jump && !_prevJump;
        InteractPressed = Interact && !_prevInteract;
        PausePressed = Pause && !_prevPause;
        CrouchPressed = Crouch && !_prevCrouch;
        _prevJump = Jump;
        _prevInteract = Interact;
        _prevPause = Pause;
        _prevCrouch = Crouch;

        MouseDx = MouseDy = 0;
        if (Scripted || window is null)
        {
            ReleaseMouse();
            return;
        }

        var shouldCapture = captureMouse && window.IsActive;
        if (!shouldCapture)
        {
            ReleaseMouse();
            return;
        }

        var center = window.PointToScreen(new Point(window.ActualWidth / 2, window.ActualHeight / 2));
        var cx = (int)center.X;
        var cy = (int)center.Y;

        if (!_mouseCaptured)
        {
            _mouseCaptured = true;
            Mouse.OverrideCursor = Cursors.None;
            SetCursorPos(cx, cy);
            return;
        }

        if (GetCursorPos(out var p))
        {
            MouseDx = p.X - cx;
            MouseDy = p.Y - cy;
            SetCursorPos(cx, cy);
        }
    }

    public void ReleaseMouse()
    {
        if (!_mouseCaptured)
            return;
        _mouseCaptured = false;
        Mouse.OverrideCursor = null;
    }

    /// <summary>Сбросить «залипшие» нажатия (после паузы, чтобы Esc/E не сработали дважды).</summary>
    public void ResetEdges()
    {
        _prevJump = _prevInteract = _prevPause = _prevCrouch = true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);
}
