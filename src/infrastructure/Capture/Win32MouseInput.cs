using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Domain.RemoteDesktop;
using Domain.RemoteDesktop.Dto;
using Microsoft.Extensions.Logging;

namespace Capture;

public sealed class Win32MouseInput(ILogger<Win32MouseInput> logger)
{
    private int _wheelRemainder;

    /// <summary>
    /// Ставит курсор через SendInput (абсолютные 0..65535) и эмулирует down/up.
    /// Щелчок колеса — отдельная ветка: флаги только WHEEL, а mouseData кратен 120.
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

        var mouseData = 0u;
        if (ev is "wheel")
        {
            var (data, remainder) = WheelMapping.Consume(_wheelRemainder, pointer.Delta);
            _wheelRemainder = remainder;
            if (data == 0)
                return;

            flags = NativeMethods.MOUSEEVENTF_WHEEL;
            mouseData = unchecked((uint)data);
        }

        var input = new NativeMethods.Input
        {
            type = NativeMethods.INPUT_MOUSE,
            u = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MouseInput
                {
                    dx = SendInputMapping.ToAbsolute(pointer.X),
                    dy = SendInputMapping.ToAbsolute(pointer.Y),
                    mouseData = mouseData,
                    dwFlags = flags
                }
            }
        };

        var sent = NativeMethods.SendInput(1, [input], Marshal.SizeOf<NativeMethods.Input>());
        if (sent == 0)
            logger.LogWarning("SendInput не применил мышь, код {Error}", Marshal.GetLastWin32Error());
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
