using System.Net.NetworkInformation;

namespace LiteServer.Core;

public static class PortChecker
{
    /// <summary>
    /// Checks whether the specified TCP port is currently in use by any process.
    /// </summary>
    public static bool IsPortOccupied(int port)
    {
        try
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpConnections = ipGlobalProperties.GetActiveTcpListeners();
            return tcpConnections.Any(endpoint => endpoint.Port == port);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the first available port starting from basePort.
    /// </summary>
    public static int GetAvailablePort(int basePort)
    {
        int port = basePort;
        while (IsPortOccupied(port) && port < 65535)
        {
            port++;
        }
        return port;
    }
}
