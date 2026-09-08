using CommanderExchange;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace Domain.Tests;

public class RemoteRegistrationRefresherTests
{
    [Fact]
    public async Task успешное_обновление_присутствия_не_вызывает_полную_регистрацию()
    {
        var refreshCalls = 0;
        var registerCalls = 0;

        await RemoteRegistrationRefresher.Refresh(
            () =>
            {
                refreshCalls++;
                return Task.CompletedTask;
            },
            () =>
            {
                registerCalls++;
                return Task.CompletedTask;
            });

        Assert.Equal(1, refreshCalls);
        Assert.Equal(0, registerCalls);
    }

    [Fact]
    public async Task неизвестный_метод_обновления_использует_полную_регистрацию()
    {
        var registerCalls = 0;

        await RemoteRegistrationRefresher.Refresh(
            () => throw new HubException("Method does not exist."),
            () =>
            {
                registerCalls++;
                return Task.CompletedTask;
            });

        Assert.Equal(1, registerCalls);
    }
}
