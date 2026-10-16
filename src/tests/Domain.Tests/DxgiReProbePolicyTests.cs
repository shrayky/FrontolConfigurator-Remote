using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class DxgiReProbePolicyTests
{
    private static readonly long IntervalMs = (long)DxgiReProbePolicy.Interval.TotalMilliseconds;

    [Fact]
    public void без_отказа_проба_не_нужна()
    {
        var probe = DxgiReProbePolicy.ShouldProbe(
            initFailed: false,
            nowTicks: 1_000_000,
            lastProbeTicks: 0);

        Assert.False(probe);
    }

    [Fact]
    public void сразу_после_отказа_не_пробуем()
    {
        var probe = DxgiReProbePolicy.ShouldProbe(
            initFailed: true,
            nowTicks: 1_000_000,
            lastProbeTicks: 1_000_000);

        Assert.False(probe);
    }

    [Fact]
    public void до_истечения_интервала_не_пробуем()
    {
        var probe = DxgiReProbePolicy.ShouldProbe(
            initFailed: true,
            nowTicks: 1_000_000 + IntervalMs - 1,
            lastProbeTicks: 1_000_000);

        Assert.False(probe);
    }

    [Fact]
    public void по_истечении_интервала_пробуем_снова()
    {
        var probe = DxgiReProbePolicy.ShouldProbe(
            initFailed: true,
            nowTicks: 1_000_000 + IntervalMs,
            lastProbeTicks: 1_000_000);

        Assert.True(probe);
    }

    [Fact]
    public void после_долгого_простоя_тоже_пробуем()
    {
        var probe = DxgiReProbePolicy.ShouldProbe(
            initFailed: true,
            nowTicks: 1_000_000 + IntervalMs * 10,
            lastProbeTicks: 1_000_000);

        Assert.True(probe);
    }

    [Fact]
    public void интервал_пробы_не_мельче_десятков_секунд()
    {
        Assert.True(DxgiReProbePolicy.Interval >= TimeSpan.FromSeconds(10));
    }
}
