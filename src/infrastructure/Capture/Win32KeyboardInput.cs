using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Domain.RemoteDesktop;
using Domain.RemoteDesktop.Dto;
using Microsoft.Extensions.Logging;

namespace Capture;

public sealed class Win32KeyboardInput(ILogger<Win32KeyboardInput> logger)
{
    /// <summary>
    /// Эмулирует нажатие или отпускание клавиши через SendInput.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public void Apply(RemoteKey key)
    {
        Send(key.Vk, key.Down);
    }

    /// <summary>
    /// Отпускает Ctrl/Alt/Shift, если они остались зажатыми после обрыва ввода.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public void ReleaseModifiers()
    {
        foreach (var vk in KeyboardSendFlags.ModifierVirtualKeys)
            Send(vk, down: false);
    }

    [SupportedOSPlatform("windows")]
    private void Send(int vk, bool down)
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (!KeyboardSendFlags.ShouldSend(vk))
            return;

        var code = (ushort)vk;
        var input = new NativeMethods.Input
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KeyboardInput
                {
                    wVk = code,
                    wScan = (ushort)NativeMethods.MapVirtualKey(code, NativeMethods.MAPVK_VK_TO_VSC),
                    dwFlags = KeyboardSendFlags.From(down, vk)
                }
            }
        };

        var sent = NativeMethods.SendInput(1, [input], Marshal.SizeOf<NativeMethods.Input>());
        if (sent == 0)
            logger.LogWarning("SendInput не применил клавишу vk={Vk}, код {Error}", vk, Marshal.GetLastWin32Error());
    }
}
