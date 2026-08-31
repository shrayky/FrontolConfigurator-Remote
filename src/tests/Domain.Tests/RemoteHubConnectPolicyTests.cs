using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class RemoteHubConnectPolicyTests
{
    [Fact]
    public void реконнект_без_паузы_в_30_секунд()
    {
        Assert.DoesNotContain(TimeSpan.FromSeconds(30), RemoteHubConnectPolicy.ReconnectDelays);
        Assert.Equal(TimeSpan.FromSeconds(3), RemoteHubConnectPolicy.ReconnectDelays.Max());
    }

    [Fact]
    public void handshake_не_дольше_8_секунд()
    {
        Assert.Equal(TimeSpan.FromSeconds(8), RemoteHubConnectPolicy.HandshakeTimeout);
        Assert.Equal(TimeSpan.FromSeconds(8), RemoteHubConnectPolicy.ConnectTimeout);
    }

    [Fact]
    public void server_timeout_больше_keepalive_чтобы_jpeg_не_рвал_пинг()
    {
        Assert.True(RemoteHubConnectPolicy.ServerTimeout >= RemoteHubConnectPolicy.KeepAliveInterval * 2);
        Assert.True(RemoteHubConnectPolicy.ServerTimeout > TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void heartbeat_раз_в_минуту_чтобы_файл_лога_ротировался()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), RemoteHubConnectPolicy.HeartbeatInterval);
    }
}
