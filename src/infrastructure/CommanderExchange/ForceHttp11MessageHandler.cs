using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Authentication;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;

namespace CommanderExchange;

/// <summary>
/// Принудительно HTTP/1.1 и TLS 1.2.
/// Нужно для Windows 7/8: Schannel не умеет ALPN/HTTP/2 и TLS 1.3, .NET 10 иначе падает с 0x80090302.
/// </summary>
internal sealed class ForceHttp11MessageHandler : DelegatingHandler
{
    internal static bool IsRequiredOnThisOs =>
        OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(10);

    public ForceHttp11MessageHandler(HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        RestrictToTls12(innerHandler);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RestrictToHttp11(request);
        return base.SendAsync(request, cancellationToken);
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RestrictToHttp11(request);
        return base.Send(request, cancellationToken);
    }

    /// <summary>
    /// WebSocket только HTTP/1.1 GET+Upgrade, без ALPN.
    /// </summary>
    internal static void RestrictWebSocket(ClientWebSocketOptions options)
    {
        options.HttpVersion = HttpVersion.Version11;
        options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
    }

    /// <summary>
    /// На старых Windows: LongPolling запасным транспортом и своя фабрика WebSocket.
    /// </summary>
    internal static void ApplyLegacyOsHubOptions(
        HttpConnectionOptions options,
        Func<HttpMessageHandler> createHandler)
    {
        options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
        options.WebSocketFactory = (context, cancellationToken) =>
            ConnectHttp11WebSocketAsync(context, createHandler, cancellationToken);
    }

    /// <summary>
    /// Подключает WebSocket через invoker с HTTP/1.1, чтобы SignalR не обходил handler.
    /// Handler живёт вместе с сокетом: сразу Dispose оборвёт поток.
    /// </summary>
    internal static async ValueTask<WebSocket> ConnectHttp11WebSocketAsync(
        WebSocketConnectionContext context,
        Func<HttpMessageHandler> createHandler,
        CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        RestrictWebSocket(socket.Options);

        foreach (var header in context.Options.Headers)
            socket.Options.SetRequestHeader(header.Key, header.Value);

        var invoker = new HttpMessageInvoker(createHandler(), disposeHandler: true);
        try
        {
            await socket.ConnectAsync(context.Uri, invoker, cancellationToken);
            return new WebSocketWithInvoker(socket, invoker);
        }
        catch
        {
            socket.Dispose();
            invoker.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Запрещает HTTP/2 и выше на конкретном запросе.
    /// </summary>
    private static void RestrictToHttp11(HttpRequestMessage request)
    {
        request.Version = HttpVersion.Version11;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
    }

    /// <summary>
    /// Включает только TLS 1.2 на внутреннем handler, не заменяя его.
    /// </summary>
    internal static void RestrictToTls12(HttpMessageHandler handler)
    {
        switch (handler)
        {
            case SocketsHttpHandler sockets:
                sockets.SslOptions ??= new SslClientAuthenticationOptions();
                sockets.SslOptions.EnabledSslProtocols = SslProtocols.Tls12;
                sockets.SslOptions.ApplicationProtocols = null;
                break;
            case HttpClientHandler http:
                http.SslProtocols = SslProtocols.Tls12;
                break;
            case DelegatingHandler { InnerHandler: not null } nested:
                RestrictToTls12(nested.InnerHandler);
                break;
        }
    }
}

/// <summary>
/// Сокет вместе с HTTP-invoker: handler нельзя Dispose до закрытия потока.
/// </summary>
internal sealed class WebSocketWithInvoker : WebSocket
{
    private readonly WebSocket _inner;
    private readonly HttpMessageInvoker _invoker;
    private int _disposed;

    public WebSocketWithInvoker(WebSocket inner, HttpMessageInvoker invoker)
    {
        _inner = inner;
        _invoker = invoker;
    }

    public override WebSocketCloseStatus? CloseStatus => _inner.CloseStatus;

    public override string? CloseStatusDescription => _inner.CloseStatusDescription;

    public override string? SubProtocol => _inner.SubProtocol;

    public override WebSocketState State => _inner.State;

    public override void Abort() => _inner.Abort();

    public override Task CloseAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken) =>
        _inner.CloseAsync(closeStatus, statusDescription, cancellationToken);

    public override Task CloseOutputAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken) =>
        _inner.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);

    public override Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer,
        CancellationToken cancellationToken) =>
        _inner.ReceiveAsync(buffer, cancellationToken);

    public override Task SendAsync(
        ArraySegment<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken) =>
        _inner.SendAsync(buffer, messageType, endOfMessage, cancellationToken);

    public override void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _inner.Dispose();
        _invoker.Dispose();
    }
}
