using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class ClipboardTextGuardTests
{
    [Fact]
    public void IsAllowed_false_для_пустого()
    {
        Assert.False(ClipboardTextGuard.IsAllowed(null));
        Assert.False(ClipboardTextGuard.IsAllowed(""));
    }

    [Fact]
    public void IsAllowed_true_для_текста_в_лимите()
    {
        Assert.True(ClipboardTextGuard.IsAllowed("касса"));
        Assert.True(ClipboardTextGuard.IsAllowed(new string('a', ClipboardTextGuard.MaxChars)));
    }

    [Fact]
    public void IsAllowed_false_длиннее_32000()
    {
        Assert.Equal(32_000, ClipboardTextGuard.MaxChars);
        Assert.False(ClipboardTextGuard.IsAllowed(new string('a', ClipboardTextGuard.MaxChars + 1)));
    }
}
