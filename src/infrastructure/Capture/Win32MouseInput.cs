using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Domain.RemoteDesktop;
using Domain.RemoteDesktop.Dto;

namespace Capture;

public sealed class Win32MouseInput
{
    /// <summary>
    /// Ставит курсор через SendInput (абсолютные 0..65535) и эмулирует down/up.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public void Apply(RemotePointer pointer)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var flags = NativeMethods.MOUSEEVENTF_MOVE | NativeMethods.MOUSEEVENTF_ABSOLUTE;
        var ev = pointer.Ev;
        if (ev is "down")
            flags |= DownFlags(pointer.Buttons);
        else if (ev is "up")
            flags |= UpFlags(pointer.Buttons);

        var input = new NativeMethods.Input
        {
            type = NativeMethods.INPUT_MOUSE,
            mi = new NativeMethods.MouseInput
            {
                dx = SendInputMapping.ToAbsolute(pointer.X),
                dy = SendInputMapping.ToAbsolute(pointer.Y),
                dwFlags = flags
            }
        };

        NativeMethods.SendInput(1, [input], Marshal.SizeOf<NativeMethods.Input>());
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
