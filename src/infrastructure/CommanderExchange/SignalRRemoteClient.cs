using Capture;
using Domain.Configuration;
using Domain.RemoteDesktop;
using Domain.RemoteDesktop.Dto;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace CommanderExchange;

public class SignalRRemoteClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<SignalRRemoteClient> _logger;
    private readonly Parameters _parameters;
    private readonly JpegSessionStreamer _streamer;
    private readonly Win32MouseInput _mouse;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _startLock = new(1, 1);

    private HubConnection? _connection;
    private string _hubUrl = string.Empty;
    private string _token = string.Empty;
    private int _sendingFrame;

    public SignalRRemoteClient(
        ILogger<SignalRRemoteClient> logger,
        Parameters parameters,
        JpegSessionStreamer streamer,
        Win32MouseInput mouse)
    {
        _logger = logger;
        _parameters = parameters;
        _streamer = streamer;
        _mouse = mouse;
    }

    /// <summary>
    /// Возвращает true, если соединение с хабом установлено.
    /// </summary>
    public bool ConnectionUp() =>
        _connection != null && _connection.State == HubConnectionState.Connected;

    /// <summary>
    /// Подключается к хабу remote и регистрирует модуль по токену агента.
    /// </summary>
    public async Task StartAsync()
    {
        var phase = ToPhase(_connection);
        if (!RemoteHubStartPolicy.CanStart(phase))
        {
            _logger.LogInformation("StartAsync пропущен, фаза {Phase}", phase);
            return;
        }

        try
        {
            await _startLock.WaitAsync(_cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var connectWatch = Stopwatch.StartNew();
        try
        {
            phase = ToPhase(_connection);
            if (!RemoteHubStartPolicy.CanStart(phase))
            {
                _logger.LogInformation("StartAsync пропущен после lock, фаза {Phase}", phase);
                return;
            }

            _hubUrl = RemoteHubUrl.FromAddress(_parameters.CentralServerSettings.Address);
            _token = _parameters.CentralServerSettings.Token;

            if (_hubUrl == string.Empty || _token == string.Empty)
            {
                _logger.LogDebug("Нет настроек для подключения к центральному серверу");
                return;
            }

            await DisposeConnectionUnlockedAsync();

            _connection = new HubConnectionBuilder()
                .WithUrl(_hubUrl, options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.HttpMessageHandlerFactory = _ => CreateHubHttpHandler();
                })
                .WithAutomaticReconnect(RemoteHubConnectPolicy.ReconnectDelays)
                .Build();

            _connection.HandshakeTimeout = RemoteHubConnectPolicy.HandshakeTimeout;
            _connection.ServerTimeout = RemoteHubConnectPolicy.ServerTimeout;
            _connection.KeepAliveInterval = RemoteHubConnectPolicy.KeepAliveInterval;

            _connection.On<string>("RemoteRegistered", OnRemoteRegistered);
            _connection.On<JsonElement>("RemoteSessionStart", OnRemoteSessionStart);
            _connection.On<JsonElement>("RemoteSessionStop", OnRemoteSessionStop);
            _connection.On<JsonElement, JsonElement>("RemoteControllerChanged", OnRemoteControllerChanged);
            _connection.On<RemotePointer>("RemotePointer", OnRemotePointer);

            _connection.Reconnecting += error =>
            {
                _logger.LogWarning(error, "Переподключение к SignalR серверу...");
                _streamer.Stop();
                return Task.CompletedTask;
            };

            _connection.Reconnected += connectionId =>
            {
                _logger.LogInformation("Переподключено к SignalR серверу. ConnectionId: {ConnectionId}", connectionId);
                _ = Task.Run(async () => await RegisterRemoteAsync());
                return Task.CompletedTask;
            };

            _connection.Closed += OnConnectionClosed;

            await _connection.StartAsync(_cancellationTokenSource.Token);
            _logger.LogInformation(
                "Подключено к SignalR серверу: {HubUrl} за {ElapsedMs} мс",
                _hubUrl, connectWatch.ElapsedMilliseconds);

            await RegisterRemoteAsync();
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Подключение к SignalR отменено за {ElapsedMs} мс: {HubUrl}",
                connectWatch.ElapsedMilliseconds, _hubUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Ошибка при подключении к SignalR серверу за {ElapsedMs} мс: {HubUrl}",
                connectWatch.ElapsedMilliseconds, _hubUrl);
        }
        finally
        {
            _startLock.Release();
        }
    }

    /// <summary>
    /// Останавливает клиент SignalR и освобождает соединение.
    /// </summary>
    public async Task StopAsync()
    {
        _streamer.Stop();
        await _cancellationTokenSource.CancelAsync();

        var acquired = await _startLock.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await DisposeConnectionUnlockedAsync();
        }
        finally
        {
            if (acquired)
                _startLock.Release();
        }

        _logger.LogInformation("Клиент SignalR остановлен");
    }

    /// <summary>
    /// Отправляет JPEG-кадр на хаб Командера методом UploadFrame без ожидания ответа сервера.
    /// </summary>
    public async Task<bool> UploadFrameAsync(RemoteJpegFrame frame, CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _sendingFrame, 1, 0) != 0)
            return false;

        try
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
                return false;

            await _connection.SendAsync("UploadFrame", frame, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки JPEG-кадра сессии {SessionId}", frame.SessionId);
            return false;
        }
        finally
        {
            Interlocked.Exchange(ref _sendingFrame, 0);
        }
    }

    private async Task RegisterRemoteAsync()
    {
        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Невозможно зарегистрировать remote-модуль: соединение не установлено");
            return;
        }

        var registerWatch = Stopwatch.StartNew();
        try
        {
            await _connection.InvokeAsync("RegisterRemote", _token, _cancellationTokenSource.Token);
            _logger.LogInformation("RegisterRemote выполнен за {ElapsedMs} мс", registerWatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при регистрации remote-модуля за {ElapsedMs} мс", registerWatch.ElapsedMilliseconds);
        }
    }

    private async Task OnConnectionClosed(Exception? error)
    {
        _logger.LogError(error, "Соединение с SignalR сервером закрыто");
        if (error == null || _cancellationTokenSource.IsCancellationRequested)
            return;

        _streamer.Stop();
        try
        {
            await Task.Delay(RemoteHubConnectPolicy.ClosedRetryDelay, _cancellationTokenSource.Token);
            await StartAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка повторного подключения к SignalR");
        }
    }

    private async Task DisposeConnectionUnlockedAsync()
    {
        if (_connection == null)
            return;

        var connection = _connection;
        _connection = null;
        connection.Closed -= OnConnectionClosed;
        await connection.DisposeAsync();
    }

    private void OnRemoteRegistered(string token)
    {
        _logger.LogInformation("Remote-модуль зарегистрирован на сервере. Token: {Token}", token);
    }

    private void OnRemoteSessionStart(JsonElement payload)
    {
        try
        {
            if (!TryGetSessionId(payload, out var sessionId))
            {
                _logger.LogWarning("RemoteSessionStart без sessionId: {Payload}", payload);
                return;
            }

            _logger.LogInformation("Старт JPEG-стрима сессии {SessionId}", sessionId);
            _streamer.Start(sessionId, UploadFrameAsync);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки RemoteSessionStart");
        }
    }

    private void OnRemoteSessionStop(JsonElement payload)
    {
        try
        {
            if (!TryGetSessionId(payload, out var sessionId))
            {
                _logger.LogWarning("RemoteSessionStop без sessionId: {Payload}", payload);
                return;
            }

            _logger.LogInformation("Остановка JPEG-стрима сессии {SessionId}", sessionId);
            _streamer.Stop(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки RemoteSessionStop");
        }
    }

    private void OnRemoteControllerChanged(JsonElement sessionId, JsonElement controllerUserLogin)
    {
        _logger.LogInformation(
            "Получено RemoteControllerChanged. SessionId: {SessionId}, Controller: {Controller}",
            sessionId, controllerUserLogin);
    }

    private void OnRemotePointer(RemotePointer pointer)
    {
        try
        {
            if (!_streamer.IsRunning || !OperatingSystem.IsWindows())
                return;

            if (pointer.Ev is not ("down" or "up" or "move"))
                return;

            _mouse.Apply(pointer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки RemotePointer");
        }
    }

    /// <summary>
    /// Создаёт HTTP-handler для SignalR: на Windows 7/8 — HTTP/1.1 и TLS 1.2.
    /// </summary>
    private static HttpMessageHandler CreateHubHttpHandler()
    {
        var sockets = new SocketsHttpHandler
        {
            ConnectTimeout = RemoteHubConnectPolicy.ConnectTimeout
        };

        if (!ForceHttp11MessageHandler.IsRequiredOnThisOs)
            return sockets;

        return new ForceHttp11MessageHandler(sockets);
    }

    private static RemoteConnectionPhase ToPhase(HubConnection? connection) =>
        connection?.State switch
        {
            null => RemoteConnectionPhase.None,
            HubConnectionState.Disconnected => RemoteConnectionPhase.Disconnected,
            HubConnectionState.Connecting => RemoteConnectionPhase.Connecting,
            HubConnectionState.Connected => RemoteConnectionPhase.Connected,
            HubConnectionState.Reconnecting => RemoteConnectionPhase.Reconnecting,
            _ => RemoteConnectionPhase.Disconnected
        };

    private static bool TryGetSessionId(JsonElement payload, out Guid sessionId)
    {
        sessionId = Guid.Empty;

        if (payload.ValueKind == JsonValueKind.String)
            return payload.TryGetGuid(out sessionId);

        if (payload.ValueKind != JsonValueKind.Object)
            return false;

        if (payload.TryGetProperty("sessionId", out var property) && property.TryGetGuid(out sessionId))
            return true;

        var parsed = payload.Deserialize<SessionIdPayload>(JsonOptions);
        if (parsed == null || parsed.SessionId == Guid.Empty)
            return false;

        sessionId = parsed.SessionId;
        return true;
    }

    private sealed class SessionIdPayload
    {
        public Guid SessionId { get; set; }
    }
}
