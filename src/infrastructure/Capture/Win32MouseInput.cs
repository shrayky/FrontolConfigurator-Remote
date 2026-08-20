using System.Runtime.Versioning;
using Domain.RemoteDesktop.Dto;

namespace Capture;

public sealed class Win32MouseInput
{
    /// <summary>
    /// Перемещает курсор в X*ширина / Y*высота экрана и эмулирует down/up по битовой маске Buttons.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public void Apply(RemotePointer pointer)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
        var screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
        if (screenWidth <= 0 || screenHeight <= 0)
            return;

        var x = (int)(pointer.X * screenWidth);
        var y = (int)(pointer.Y * screenHeight);
        NativeMethods.SetCursorPos(x, y);

        var ev = pointer.Ev;
        if (ev is "move")
            return;

        uint flags = 0;
        if (ev is "down")
            flags = DownFlags(pointer.Buttons);
        else if (ev is "up")
            flags = UpFlags(pointer.Buttons);

        if (flags == 0)
            return;

        NativeMethods.mouse_event(flags, 0, 0, 0, UIntPtr.Zero);
    }

    private static uint DownFlags(int buttons)
    {
        if (buttons == 0)
            return NativeMethods.MOUSEEVENTF_LEFTDOWN;

        uint flags = 0;
        if ((buttons & 1) != 0)
            flags |= NativeMethods.MOUSEEVENTF_LEFTDOWN;
        if ((buttons & 2) != 0)
            flags |= NativeMethods.MOUSEEVENTF_RIGHTDOWN;
        if ((buttons & 4) != 0)
            flags |= NativeMethods.MOUSEEVENTF_MIDDLEDOWN;
        return flags;
    }

    private static uint UpFlags(int buttons)
    {
        if (buttons == 0)
            return NativeMethods.MOUSEEVENTF_LEFTUP;

        uint flags = 0;
        if ((buttons & 1) != 0)
            flags |= NativeMethods.MOUSEEVENTF_LEFTUP;
        if ((buttons & 2) != 0)
            flags |= NativeMethods.MOUSEEVENTF_RIGHTUP;
        if ((buttons & 4) != 0)
            flags |= NativeMethods.MOUSEEVENTF_MIDDLEUP;
        return flags;
    }
}
