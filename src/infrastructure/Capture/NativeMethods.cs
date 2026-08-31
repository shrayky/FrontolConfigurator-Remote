using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Capture;

internal static class NativeMethods
{
    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;

    public const uint MOUSEEVENTF_MOVE = 0x0001;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    public const uint INPUT_MOUSE = 0;
    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint MAPVK_VK_TO_VSC = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseInput
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyboardInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MouseInput mi;
        [FieldOffset(0)] public KeyboardInput ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Input
    {
        public uint type;
        public InputUnion u;
    }

    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll")]
    public static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    public const int CURSOR_SHOWING = 0x00000001;
    public const int DI_NORMAL = 0x0003;

    [StructLayout(LayoutKind.Sequential)]
    public struct PointApi
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CursorInfo
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public PointApi ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IconInfo
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, int dx, int dy, int dwData, UIntPtr dwExtraInfo);

    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll")]
    public static extern bool GetCursorInfo(ref CursorInfo pci);

    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll")]
    public static extern bool GetIconInfo(IntPtr hIcon, out IconInfo piconinfo);

    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll")]
    public static extern bool DrawIconEx(
        IntPtr hdc,
        int xLeft,
        int yTop,
        IntPtr hIcon,
        int cxWidth,
        int cyHeight,
        int istepIfAniCur,
        IntPtr hbrFlickerFreeDraw,
        int diFlags);

    [SupportedOSPlatform("windows")]
    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);
}
