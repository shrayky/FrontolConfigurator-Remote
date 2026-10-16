using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Capture;

public sealed class Win32Clipboard(ILogger<Win32Clipboard> logger)
{
    private readonly object _sync = new();
    private uint _lastSequence;
    private string _suppressEcho = string.Empty;

    /// <summary>
    /// Запоминает номер буфера на старте сессии, чтобы не отправлять уже лежащий текст.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public void Arm()
    {
        if (!OperatingSystem.IsWindows())
            return;

        lock (_sync)
            _lastSequence = NativeMethods.GetClipboardSequenceNumber();
    }

    [SupportedOSPlatform("windows")]
    public void WriteText(string text)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(text))
            return;

        var bytes = Encoding.Unicode.GetBytes(text + "\0");
        var memory = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (nuint)bytes.Length);
        if (memory == IntPtr.Zero)
        {
            logger.LogWarning("GlobalAlloc для буфера не выделил память, размер {Bytes}", bytes.Length);
            return;
        }

        var locked = NativeMethods.GlobalLock(memory);
        if (locked == IntPtr.Zero)
        {
            NativeMethods.GlobalFree(memory);
            logger.LogWarning("GlobalLock для буфера не удался");
            return;
        }

        Marshal.Copy(bytes, 0, locked, bytes.Length);
        NativeMethods.GlobalUnlock(memory);

        if (!NativeMethods.OpenClipboard(IntPtr.Zero))
        {
            NativeMethods.GlobalFree(memory);
            logger.LogWarning("OpenClipboard не открыл буфер для записи, код {Error}", Marshal.GetLastWin32Error());
            return;
        }

        try
        {
            NativeMethods.EmptyClipboard();
            if (NativeMethods.SetClipboardData(NativeMethods.CF_UNICODETEXT, memory) == IntPtr.Zero)
            {
                NativeMethods.GlobalFree(memory);
                logger.LogWarning("SetClipboardData не записал текст, код {Error}", Marshal.GetLastWin32Error());
                return;
            }

            memory = IntPtr.Zero;
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }

        lock (_sync)
        {
            _suppressEcho = text;
            _lastSequence = NativeMethods.GetClipboardSequenceNumber();
        }
    }

    /// <summary>
    /// Возвращает новый текст буфера. Собственная запись и неизменный номер дают null.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public string? ReadIfChanged()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var sequence = NativeMethods.GetClipboardSequenceNumber();
        lock (_sync)
        {
            if (sequence == _lastSequence)
                return null;

            _lastSequence = sequence;
        }

        var text = ReadText();
        lock (_sync)
        {
            if (text == null || text == _suppressEcho)
                return null;
        }

        return text;
    }

    [SupportedOSPlatform("windows")]
    private string? ReadText()
    {
        if (!NativeMethods.OpenClipboard(IntPtr.Zero))
            return null;

        try
        {
            var handle = NativeMethods.GetClipboardData(NativeMethods.CF_UNICODETEXT);
            if (handle == IntPtr.Zero)
                return null;

            var locked = NativeMethods.GlobalLock(handle);
            if (locked == IntPtr.Zero)
                return null;

            try
            {
                return Marshal.PtrToStringUni(locked);
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }
}
