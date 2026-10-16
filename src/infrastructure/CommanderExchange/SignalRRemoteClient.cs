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
    private readonly Win32KeyboardInput _keyboard;
    private readonly Win32TaskManagerStart _taskManager;
    private readonly Win32FrontolAdminStart _frontolAdmin;
    private readonly Win32FrontolDbFolderOpen _frontolDbFolder;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _startLock = new(1, 1);

    private HubConnection? _connection;
    private Func<Exception?, Task>? _closedHandler;
    private string _hubUrl = string.Empty;
    private string _token = string.Empty;
    private int _sendingFrame;

    /// <summary>
    /// Последняя начатая сессия: нужна, чтобы возобновить стрим после автопереподключения,
    /// когда сервер не присылает RemoteSessionStart повторно.
    /// </summary>
    private readonly object _sessionSync = new();
    private Guid _persistedSessionId = Guid.Empty;

    public SignalRRemoteClient(
        ILogger<SignalRRemoteClient> logger,
        Parameters parameters,
        JpegSessionStreamer streamer,
        Win32MouseInput mouse,
        Win32KeyboardInput keyboard,
        Win32TaskManagerStart taskManager,
        Win32FrontolAdminStart frontolAdmin,
        Win32FrontolDbFolderOpen frontolDbFolder)
    {
        _logger = logger;
        _parameters = parameters;
        _streamer = streamer;
        _mouse = mouse;
        _keyboard = keyboard;
        _taskManager = taskManager;
        _frontolAdmin = frontolAdmin;
        _frontolDbFolder = frontolDbFolder;
    }

    /// <summary>
    /// Возвращает true, если соединение с хабом установлено.
    /// </summary>
    public bool ConnectionUp() =>
        _connection != null && _connection.State == HubConnectionState.Connected;

    /// <summary>
    /// Текущая фаза хаба для heartbeat.
    /// </summary>
    public RemoteConnectionPhase Phase => ToPhase(_connection);

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
                    if (ForceHttp11MessageHandler.IsRequiredOnThisOs)
                        ForceHttp11MessageHandler.ApplyLegacyOsHubOptions(options, CreateHubHttpHandler);
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
            _connection.On<RemoteKey>("RemoteKey", OnRemoteKey);
            _connection.On<RemoteTaskManager>("RemoteTaskManager", OnRemoteTaskManager);
            _connection.On<RemoteFrontolAdmin>("RemoteFrontolAdmin", OnRemoteFrontolAdmin);
            _connection.On<RemoteFrontolDbFolder>("RemoteFrontolDbFolder", OnRemoteFrontolDbFolder);

            _connection.Reconnecting += error =>
            {
                _logger.LogWarning(error, "Переподключение к SignalR серверу...");
                if (OperatingSystem.IsWindows())
                    _keyboard.ReleaseModifiers();
                _streamer.Stop();
                return Task.CompletedTask;
            };

            _connection.Reconnected += connectionId =>
            {
                _logger.LogInformation("Переподключено к SignalR серверу. ConnectionId: {ConnectionId}", connectionId);
                _ = Task.Run(async () =>
                {
                    await RegisterRemoteAsync();
                    await TryResumeSessionAsync();
                });
                return Task.CompletedTask;
            };

            _closedHandler = error =>
            {
                _ = Task.Run(() => OnConnectionClosed(error));
                return Task.CompletedTask;
            };
            _connection.Closed += _closedHandler;

            await _connection.StartAsync(_cancellationTokenSource.Token);
            _logger.LogInformation(
                "Подключено к SignalR серверу: {HubUrl} за {ElapsedMs} мс",
                _hubUrl, connectWatch.ElapsedMilliseconds);

            await RegisterRemoteAsync();
        }
        catch (OperationCanceledException)
        {
            if (RemoteHubCancelKindPolicy.Classify(_cancellationTokenSource.IsCancellationRequested) ==
                RemoteHubCancelKind.HostStop)
            {
                _logger.LogWarning(
                    "Подключение к SignalR отменено за {ElapsedMs} мс: {HubUrl}",
                    connectWatch.ElapsedMilliseconds, _hubUrl);
            }
            else
            {
                _logger.LogWarning(
                    "Таймаут handshake SignalR за {ElapsedMs} мс: {HubUrl}",
                    connectWatch.ElapsedMilliseconds, _hubUrl);
            }
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

    public async Task RefreshRegistrationAsync()
    {
        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Невозможно обновить присутствие remote-модуля: соединение не установлено");
            return;
        }

        try
        {
            await RemoteRegistrationRefresher.Refresh(
                () => _connection.InvokeAsync(
                    "RefreshRemotePresence",
                    _token,
                    _cancellationTokenSource.Token),
                RegisterRemoteAsync);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении присутствия remote-модуля");
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
            _logger.LogError(
                ex,
                "Ошибка отправки JPEG-кадра сессии {SessionId}: {Message}",
                frame.SessionId,
                ex.Message);
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
        _streamer.Stop();
        _logger.LogWarning(
            "Стрим остановлен из-за закрытия хаба, кадры продолжатся только после нового RemoteSessionStart");

        if (!RemoteHubClosedPolicy.ShouldReconnect(_cancellationTokenSource.IsCancellationRequested))
            return;

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
        if (_closedHandler != null)
        {
            connection.Closed -= _closedHandler;
            _closedHandler = null;
        }

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
            lock (_sessionSync)
                _persistedSessionId = sessionId;

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
            ClearPersistedSession(sessionId);
            if (OperatingSystem.IsWindows())
                _keyboard.ReleaseModifiers();
            _streamer.Stop(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки RemoteSessionStop");
        }
    }

    /// <summary>
    /// Забывает сессию, если сервер закрыл именно её (сессию могли уже заменить новой).
    /// </summary>
    private void ClearPersistedSession(Guid sessionId)
    {
        lock (_sessionSync)
        {
            if (_persistedSessionId == sessionId)
                _persistedSessionId = Guid.Empty;
        }
    }

    /// <summary>
    /// Возобновляет стрим последней сессии после автопереподключения: до этого кадры
    /// возобновлялись только по новому RemoteSessionStart, и оператор видел чёрный экран.
    /// Если сервер уже завершил сессию, кадры будут отвергнуты — это видно по ошибкам отправки.
    /// </summary>
    private async Task TryResumeSessionAsync()
    {
        Guid sessionId;
        lock (_sessionSync)
            sessionId = _persistedSessionId;

        if (sessionId == Guid.Empty)
            return;

        if (_streamer.IsRunning)
        {
            _logger.LogInformation("Стрим сессии {SessionId} уже идёт, возобновление не требуется", sessionId);
            return;
        }

        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Не удалось возобновить стрим сессии {SessionId}: соединение не установлено", sessionId);
            return;
        }

        _logger.LogInformation("Возобновление JPEG-стрима сессии {SessionId} после переподключения", sessionId);
        _streamer.Start(sessionId, UploadFrameAsync);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Принимать ли ввод: стрим должен быть жив и события должны принадлежать активной сессии,
    /// иначе ввод прошлой или чужой сессии попадёт на кассу.
    /// </summary>
    private bool ShouldApplyInput(Guid sessionId) =>
        RemoteInputAcceptancePolicy.ShouldApply(
            _streamer.IsRunning,
            _streamer.ActiveSessionId,
            sessionId);

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
            if (!ShouldApplyInput(pointer.SessionId) || !OperatingSystem.IsWindows())
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

    private void OnRemoteKey(RemoteKey key)
    {
        try
        {
            if (!ShouldApplyInput(key.SessionId) || !OperatingSystem.IsWindows())
                return;

            _keyboard.Apply(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки RemoteKey");
        }
    }

    private void OnRemoteTaskManager(RemoteTaskManager request)
    {
        try
        {
            if (!ShouldApplyInput(request.SessionId) || !OperatingSystem.IsWindows())
                return;

            _logger.LogInformation("Запуск диспетчера задач, сессия {SessionId}", request.SessionId);
            _taskManager.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки RemoteTaskManager");
        }
    }

    private void OnRemoteFrontolAdmin(RemoteFrontolAdmin request)
    {
        try
        {
            if (!ShouldApplyInput(request.SessionId) || !OperatingSystem.IsWindows())
                return;

            _logger.LogInformation("Запуск Frontol Администратор, сессия {SessionId}", request.SessionId);
            _ = ReportActionResultAsync(_frontolAdmin.Start(request.SessionId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки RemoteFrontolAdmin");
            _ = ReportActionResultAsync(new RemoteActionResult
            {
                SessionId = request.SessionId,
                Action = RemoteActionResult.FrontolAdmin,
                Success = false,
                Error = ex.Message
            });
        }
    }

    private void OnRemoteFrontolDbFolder(RemoteFrontolDbFolder request)
    {
        try
        {
            if (!ShouldApplyInput(request.SessionId) || !OperatingSystem.IsWindows())
                return;

            _logger.LogInformation("Открытие каталога базы Frontol, сессия {SessionId}", request.SessionId);
            _ = ReportActionResultAsync(_frontolDbFolder.Open(request.SessionId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки RemoteFrontolDbFolder");
            _ = ReportActionResultAsync(new RemoteActionResult
            {
                SessionId = request.SessionId,
                Action = RemoteActionResult.FrontolDbFolder,
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Отправляет результат действия в окно оператора.
    /// </summary>
    private async Task ReportActionResultAsync(RemoteActionResult result)
    {
        try
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
                return;

            await _connection.SendAsync("ReportActionResult", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки ReportActionResult");
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
