using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class SendInputMappingTests
{
    [Fact]
    public void ноль_даёт_0()
    {
        Assert.Equal(0, SendInputMapping.ToAbsolute(0f));
    }

    [Fact]
    public void единица_даёт_65535()
    {
        Assert.Equal(65535, SendInputMapping.ToAbsolute(1f));
    }

    [Fact]
    public void центр_даёт_32768()
    {
        Assert.Equal(32768, SendInputMapping.ToAbsolute(0.5f));
    }

    [Fact]
    public void значения_вне_0_1_обрезаются()
    {
        Assert.Equal(0, SendInputMapping.ToAbsolute(-0.2f));
        Assert.Equal(65535, SendInputMapping.ToAbsolute(1.4f));
    }
}
