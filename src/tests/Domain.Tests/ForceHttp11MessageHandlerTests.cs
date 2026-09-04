using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using CommanderExchange;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Xunit;

namespace Domain.Tests;

public class ForceHttp11MessageHandlerTests
{
    [Fact]
    public void RestrictWebSocket_ставит_http11_exact()
    {
        using var socket = new ClientWebSocket();

        ForceHttp11MessageHandler.RestrictWebSocket(socket.Options);

        Assert.Equal(HttpVersion.Version11, socket.Options.HttpVersion);
        Assert.Equal(HttpVersionPolicy.RequestVersionExact, socket.Options.HttpVersionPolicy);
    }

    [Fact]
    public void ApplyLegacyOsHubOptions_включает_long_polling_и_фабрику()
    {
        var options = new HttpConnectionOptions
        {
            Transports = HttpTransportType.WebSockets
        };

        ForceHttp11MessageHandler.ApplyLegacyOsHubOptions(options, () => new SocketsHttpHandler());

        Assert.Equal(HttpTransportType.WebSockets | HttpTransportType.LongPolling, options.Transports);
        Assert.NotNull(options.WebSocketFactory);
    }

    [Fact]
    public async Task ConnectHttp11WebSocket_шлёт_get_http11_через_invoker()
    {
        var capture = new CaptureHandler();
        var context = new WebSocketConnectionContext(
            new Uri("wss://win7-hub.test/hubs/remote"),
            new HttpConnectionOptions());

        await Assert.ThrowsAsync<WebSocketException>(() =>
            ForceHttp11MessageHandler.ConnectHttp11WebSocketAsync(
                context, () => capture, CancellationToken.None).AsTask());

        Assert.NotNull(capture.Request);
        Assert.Equal(HttpMethod.Get, capture.Request.Method);
        Assert.Equal(HttpVersion.Version11, capture.Request.Version);
        Assert.NotEqual(HttpMethod.Connect, capture.Request.Method);
        Assert.True(capture.Disposed);
    }

    [Fact]
    public async Task ConnectHttp11WebSocket_не_закрывает_handler_пока_сокет_жив()
    {
        var handshake = new WebsocketHandshakeHandler();
        var context = new WebSocketConnectionContext(
            new Uri("wss://win7-hub.test/hubs/remote"),
            new HttpConnectionOptions());

        var socket = await ForceHttp11MessageHandler.ConnectHttp11WebSocketAsync(
            context, () => handshake, CancellationToken.None);

        Assert.False(handshake.Disposed);
        socket.Dispose();
        Assert.True(handshake.Disposed);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public bool Disposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            throw new HttpRequestException("captured");
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class WebsocketHandshakeHandler : HttpMessageHandler
    {
        public bool Disposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var key = request.Headers.GetValues("Sec-WebSocket-Key").First();
            var accept = Convert.ToBase64String(
                SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

            var response = new HttpResponseMessage(HttpStatusCode.SwitchingProtocols)
            {
                Version = HttpVersion.Version11,
                Content = new DuplexStreamContent()
            };
            response.Headers.TryAddWithoutValidation("Connection", "Upgrade");
            response.Headers.TryAddWithoutValidation("Upgrade", "websocket");
            response.Headers.TryAddWithoutValidation("Sec-WebSocket-Accept", accept);
            return Task.FromResult(response);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class DuplexStreamContent : HttpContent
    {
        private readonly MemoryStream _stream = new();

        protected override Stream CreateContentReadStream(CancellationToken cancellationToken) =>
            _stream;

        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult<Stream>(_stream);

        protected override void SerializeToStream(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }
    }
}
