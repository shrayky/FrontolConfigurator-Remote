using System.Net;
using System.Net.WebSockets;
using CommanderExchange;
using Xunit;

namespace Domain.Tests;

public class ForceHttp11MessageHandlerTests
{
    [Fact]
    public void RestrictWebSocket_SetsHttp20OrLower_SoSignalRPassesHttpClient()
    {
        using var socket = new ClientWebSocket();

        ForceHttp11MessageHandler.RestrictWebSocket(socket.Options);

        Assert.Equal(HttpVersion.Version20, socket.Options.HttpVersion);
        Assert.Equal(HttpVersionPolicy.RequestVersionOrLower, socket.Options.HttpVersionPolicy);
    }
}
