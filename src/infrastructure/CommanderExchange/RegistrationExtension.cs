using Capture;
using CommanderExchange.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace CommanderExchange;

public static class RegistrationExtension
{
    /// <summary>
    /// Регистрирует захват экрана, SignalR-клиент remote-модуля и фоновый воркер подключения.
    /// </summary>
    public static IServiceCollection AddCommanderExchange(this IServiceCollection services)
    {
        services.AddScreenCapture();
        services.AddSingleton<SignalRRemoteClient>();
        services.AddHostedService<RemoteExchangeWorker>();
        return services;
    }
}
