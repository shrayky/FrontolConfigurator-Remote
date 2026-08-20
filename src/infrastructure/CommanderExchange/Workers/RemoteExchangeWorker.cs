using Microsoft.Extensions.Hosting;

namespace CommanderExchange.Workers;

public class RemoteExchangeWorker : BackgroundService
{
    private readonly SignalRRemoteClient _signalRClient;

    public RemoteExchangeWorker(SignalRRemoteClient signalRClient)
    {
        _signalRClient = signalRClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_signalRClient.ConnectionUp())
                await _signalRClient.StartAsync();

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
