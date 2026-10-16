using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class FrameSendPolicyTests
{
    private static readonly byte[] JpegA = [0xFF, 0xD8, 0x01];
    private static readonly byte[] JpegB = [0xFF, 0xD8, 0x02];

    [Fact]
    public void пустой_не_шлём()
    {
        var policy = new FrameSendPolicy();
        var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(policy.ShouldSend(null!, now));
        Assert.False(policy.ShouldSend([], now));
    }

    [Fact]
    public void первый_кадр_шлём()
    {
        var policy = new FrameSendPolicy();
        var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(policy.ShouldSend(JpegA, now));
    }

    [Fact]
    public void одинаковый_jpeg_не_шлём()
    {
        var policy = new FrameSendPolicy();
        var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(policy.ShouldSend(JpegA, now));
        Assert.False(policy.ShouldSend(JpegA, now));
    }

    [Fact]
    public void тот_же_jpeg_через_300мс_всё_равно_не_шлём_пока_хеш_тот_же()
    {
        var policy = new FrameSendPolicy();
        var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(policy.ShouldSend(JpegA, now));
        Assert.False(policy.ShouldSend(JpegA, now.AddMilliseconds(300)));
    }

    [Fact]
    public void другой_jpeg_раньше_интервала_не_шлём()
    {
        var policy = new FrameSendPolicy();
        var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(policy.ShouldSend(JpegA, now));
        Assert.False(policy.ShouldSend(JpegB, now.AddMilliseconds(30)));
    }

    [Fact]
    public void другой_jpeg_после_интервала_15fps_шлём()
    {
        var policy = new FrameSendPolicy();
        var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(policy.ShouldSend(JpegA, now));
        Assert.True(policy.ShouldSend(JpegB, now.AddMilliseconds(67)));
    }

    [Fact]
    public void одинаковый_jpeg_через_1с_шлём_keepalive_для_позднего_зрителя()
    {
        var policy = new FrameSendPolicy();
        var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(policy.ShouldSend(JpegA, now));
        Assert.True(policy.ShouldSend(JpegA, now.AddSeconds(1)));
    }

    [Fact]
    public void интервал_200мс_режет_кадр_на_67мс()
    {
        var policy = new FrameSendPolicy();
        policy.SetIntervalMs(200);
        var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(policy.ShouldSend(JpegA, now));
        Assert.False(policy.ShouldSend(JpegB, now.AddMilliseconds(67)));
        Assert.True(policy.ShouldSend(JpegB, now.AddMilliseconds(200)));
    }

    [Fact]
    public void интервал_40мс_пропускает_следующий_кадр()
    {
        var policy = new FrameSendPolicy();
        policy.SetIntervalMs(40);
        var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(policy.ShouldSend(JpegA, now));
        Assert.True(policy.ShouldSend(JpegB, now.AddMilliseconds(40)));
    }

    [Fact]
    public void чужой_интервал_не_меняется()
    {
        var policy = new FrameSendPolicy();
        policy.SetIntervalMs(16);
        var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(policy.ShouldSend(JpegA, now));
        Assert.False(policy.ShouldSend(JpegB, now.AddMilliseconds(30)));
        Assert.True(policy.ShouldSend(JpegB, now.AddMilliseconds(67)));
    }
}
