namespace Domain.RemoteDesktop;

public static class RemoteHubUrl
{
    /// <summary>
    /// Строит URL хаба remote по адресу центрального сервера.
    /// </summary>
    public static string FromAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return string.Empty;

        return $"{address.TrimEnd('/')}/hubs/remote";
    }
}
