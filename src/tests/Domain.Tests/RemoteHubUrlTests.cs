using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class RemoteHubUrlTests
{
    [Fact]
    public void FromAddress_WithoutTrailingSlash_AppendsHubPath()
    {
        var result = RemoteHubUrl.FromAddress("https://fc:2589");

        Assert.Equal("https://fc:2589/hubs/remote", result);
    }

    [Fact]
    public void FromAddress_WithTrailingSlash_DoesNotDuplicateSlash()
    {
        var result = RemoteHubUrl.FromAddress("https://fc:2589/");

        Assert.Equal("https://fc:2589/hubs/remote", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void FromAddress_EmptyOrWhitespace_ReturnsEmpty(string address)
    {
        var result = RemoteHubUrl.FromAddress(address);

        Assert.Equal(string.Empty, result);
    }
}
