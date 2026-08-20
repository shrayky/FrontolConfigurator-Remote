using System.Security.Cryptography;

namespace Domain.RemoteDesktop;

public class FrameSendPolicy
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(67);
    public static readonly TimeSpan KeepaliveInterval = TimeSpan.FromSeconds(1);

    private byte[]? _lastHash;
    private DateTime _lastSend = DateTime.MinValue;

    /// <summary>
    /// Возвращает true, если JPEG нужно отправить: не пустой, хеш изменился (~15 кадр/с)
    /// или тот же кадр повторно через KeepaliveInterval — чтобы поздний зритель получил картинку.
    /// </summary>
    public bool ShouldSend(byte[] jpeg, DateTime utcNow)
    {
        if (jpeg == null || jpeg.Length == 0)
            return false;

        var hash = SHA256.HashData(jpeg);
        var sameHash = _lastHash != null && hash.AsSpan().SequenceEqual(_lastHash);

        if (sameHash)
        {
            if (utcNow - _lastSend < KeepaliveInterval)
                return false;
        }
        else if (utcNow - _lastSend < MinInterval)
        {
            return false;
        }

        _lastHash = hash;
        _lastSend = utcNow;
        return true;
    }
}
