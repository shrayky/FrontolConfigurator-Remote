using Microsoft.AspNetCore.SignalR;

namespace CommanderExchange;

internal static class RemoteRegistrationRefresher
{
    public static async Task Refresh(Func<Task> refreshPresence, Func<Task> registerRemote)
    {
        try
        {
            await refreshPresence();
        }
        catch (HubException)
        {
            await registerRemote();
        }
    }
}
