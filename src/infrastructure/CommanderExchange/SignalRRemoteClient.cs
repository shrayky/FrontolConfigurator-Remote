using Capture;
using Domain.Configuration;
using Domain.RemoteDesktop;
using Domain.RemoteDesktop.Dto;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
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

    private HubConnection? _connection;
    private string _hubUrl = string.Empty;
    private string _token = string.Empty;

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
        _hubUrl = RemoteHubUrl.FromAddress(_parameters.CentralServerSettings.Address);
        _token = _parameters.CentralServerSettings.Token;

        if (_hubUrl == string.Empty || _token == string.Empty)
        {
            _logger.LogDebug("Нет настроек для подключения к центральному серверу");
            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect([
                TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)
            ])
            .Build();

        _connection.On<string>("RemoteRegistered", OnRemoteRegistered);
        _connection.On<JsonElement>("RemoteSessionStart", OnRemoteSessionStart);
        _connection.On<JsonElement>("RemoteSessionStop", OnRemoteSessionStop);
        _connection.On<JsonElement, JsonElement>("RemoteControllerChanged", OnRemoteControllerChanged);
        _connection.On<RemotePointer>("RemotePointer", OnRemotePointer);

        _connection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "Переподключение к SignalR серверу...");
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("Переподключено к SignalR серверу. ConnectionId: {ConnectionId}", connectionId);
            _ = Task.Run(async () => await RegisterRemoteAsync());
            return Task.CompletedTask;
        };

        _connection.Closed += async error =>
        {
            _logger.LogError(error, "Соединение с SignalR сервером закрыто");

            if (error != null)
            {
                await Task.Delay(5000, _cancellationTokenSource.Token);
                await StartAsync();
            }
        };

        try
        {
            await _connection.StartAsync(_cancellationTokenSource.Token);
            _logger.LogInformation("Подключено к SignalR серверу: {HubUrl}", _hubUrl);

            await RegisterRemoteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при подключении к SignalR серверу");
        }
    }

    /// <summary>
    /// Останавливает клиент SignalR и освобождает соединение.
    /// </summary>
    public async Task StopAsync()
    {
        _streamer.Stop();
        await _cancellationTokenSource.CancelAsync();

        if (_connection == null)
            return;

        await _connection.StopAsync();
        await _connection.DisposeAsync();
        _logger.LogInformation("Клиент SignalR остановлен");
    }

    /// <summary>
    /// Отправляет JPEG-кадр на хаб Командера методом UploadFrame.
    /// </summary>
    public async Task UploadFrameAsync(RemoteJpegFrame frame, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
                return;

            await _connection.InvokeAsync("UploadFrame", frame, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки JPEG-кадра сессии {SessionId}", frame.SessionId);
        }
    }

    private async Task RegisterRemoteAsync()
    {
        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Невозможно зарегистрировать remote-модуль: соединение не установлено");
            return;
        }

        try
        {
            await _connection.InvokeAsync("RegisterRemote", _token, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при регистрации remote-модуля");
        }
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
