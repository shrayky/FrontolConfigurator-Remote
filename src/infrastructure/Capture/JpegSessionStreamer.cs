using Domain.RemoteDesktop;
using Domain.RemoteDesktop.Dto;
using Microsoft.Extensions.Logging;

namespace Capture;

public sealed class JpegSessionStreamer
{
    private readonly IScreenCapture _capture;
    private readonly ILogger<JpegSessionStreamer> _logger;
    private readonly object _sync = new();

    private CancellationTokenSource? _cts;
    private Guid _sessionId;

    public JpegSessionStreamer(IScreenCapture capture, ILogger<JpegSessionStreamer> logger)
    {
        _capture = capture;
        _logger = logger;
    }

    /// <summary>
    /// Возвращает true, если сейчас идёт захват кадров.
    /// </summary>
    public bool IsRunning
    {
        get
        {
            lock (_sync)
                return _cts is { IsCancellationRequested: false };
        }
    }

    /// <summary>
    /// Запускает цикл захвата экрана и отправки JPEG для сессии (политика ограничивает до ~15 кадр/с).
    /// </summary>
    public void Start(Guid sessionId, Func<RemoteJpegFrame, CancellationToken, Task> sendFrame)
    {
        lock (_sync)
        {
            CancelUnlocked();
            _sessionId = sessionId;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _ = Task.Run(() => RunAsync(sessionId, sendFrame, token), token);
        }
    }

    /// <summary>
    /// Останавливает стрим указанной сессии.
    /// </summary>
    public void Stop(Guid sessionId)
    {
        lock (_sync)
        {
            if (_cts == null || _sessionId != sessionId)
                return;
            CancelUnlocked();
        }
    }

    /// <summary>
    /// Останавливает текущий стрим независимо от идентификатора сессии.
    /// </summary>
    public void Stop()
    {
        lock (_sync)
            CancelUnlocked();
    }

    private void CancelUnlocked()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private async Task RunAsync(
        Guid sessionId,
        Func<RemoteJpegFrame, CancellationToken, Task> sendFrame,
        CancellationToken token)
    {
        var policy = new FrameSendPolicy();
        var lastEmptyLog = DateTime.MinValue;
        var firstSendLogged = false;
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (!OperatingSystem.IsWindows())
                {
                    await Task.Delay(FrameSendPolicy.MinInterval, token);
                    continue;
                }

                var (width, height, jpeg) = _capture.Capture();
                var utcNow = DateTime.UtcNow;

                if (jpeg.Length == 0 && utcNow - lastEmptyLog >= TimeSpan.FromSeconds(5))
                {
                    lastEmptyLog = utcNow;
                    _logger.LogWarning(
                        "Пустой JPEG-кадр сессии {SessionId}: захват экрана недоступен (возможно Session 0)",
                        sessionId);
                }

                if (policy.ShouldSend(jpeg, utcNow))
                {
                    await sendFrame(new RemoteJpegFrame
                    {
                        SessionId = sessionId,
                        Width = width,
                        Height = height,
                        Jpeg = jpeg
                    }, token);

                    if (!firstSendLogged)
                    {
                        firstSendLogged = true;
                        _logger.LogInformation(
                            "Первый JPEG-кадр сессии {SessionId}: {Width}x{Height}, {Bytes} байт",
                            sessionId, width, height, jpeg.Length);
                    }
                }

                await Task.Delay(FrameSendPolicy.MinInterval, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка стрима JPEG сессии {SessionId}", sessionId);
        }
    }
}
