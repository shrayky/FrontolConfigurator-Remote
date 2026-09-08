using Domain.RemoteDesktop;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CommanderExchange.Workers;

public class RemoteExchangeWorker : BackgroundService
{
    private readonly SignalRRemoteClient _signalRClient;
    private readonly ILogger<RemoteExchangeWorker> _logger;

    public RemoteExchangeWorker(SignalRRemoteClient signalRClient, ILogger<RemoteExchangeWorker> logger)
    {
        _signalRClient = signalRClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Heartbeat remote: фаза {Phase}", _signalRClient.Phase);

            if (RemoteHubHeartbeatPolicy.ShouldRefreshRegistration(_signalRClient.Phase))
                await _signalRClient.RefreshRegistrationAsync();
            else
                await _signalRClient.StartAsync();

            await Task.Delay(RemoteHubConnectPolicy.HeartbeatInterval, stoppingToken);
        }
    }
}
