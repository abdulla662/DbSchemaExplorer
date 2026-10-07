using System.Net;
using System.Net.Sockets;

namespace DbSchemaExplorer.Services;

public static class ConnectionGuard
{
    private static readonly int[] AllowedMySqlPorts = { 3306 };
    private static readonly int[] AllowedSqlPorts  = { 1433 };

    public static async Task<string?> ValidateAsync(string host, string dbType)
    {
        if (string.IsNullOrWhiteSpace(host))
            return "Server name is required.";

        // Parse host:port
        int port = dbType == "mysql" ? 3306 : 1433;
        if (host.Contains(':'))
        {
            var parts = host.Split(':', 2);
            host = parts[0];
            if (!int.TryParse(parts[1], out port))
                return "Invalid port number.";
        }

        // Port whitelist
        var allowed = dbType == "mysql" ? AllowedMySqlPorts : AllowedSqlPorts;
        if (!allowed.Contains(port))
            return $"Port {port} is not allowed. Only standard database ports are permitted.";

        // Resolve hostname
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host);
        }
        catch
        {
            return $"Cannot resolve hostname '{host}'.";
        }

        if (addresses.Length == 0)
            return "Could not resolve the server hostname.";

        foreach (var ip in addresses)
        {
            if (IsPrivate(ip))
                return $"Connections to private/internal IP addresses are not allowed.";
        }

        return null; // valid
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // Block loopback and link-local
            return IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal;
        }

        var bytes = ip.GetAddressBytes();
        return IPAddress.IsLoopback(ip)
            || bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 169 && bytes[1] == 254)
            || bytes[0] == 127;
    }
}
