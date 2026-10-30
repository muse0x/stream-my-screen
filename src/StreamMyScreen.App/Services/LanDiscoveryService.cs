using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace StreamMyScreen.App.Services;

public static class LanDiscoveryService
{
    public static string GetActiveLocalIp()
    {
        var activeInterface = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                         && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                         && !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                         && !ni.Description.Contains("WSL", StringComparison.OrdinalIgnoreCase)
                         && !ni.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
                         && !ni.Name.Contains("vEthernet", StringComparison.OrdinalIgnoreCase)
                         && ni.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
            .OrderByDescending(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 1 : 0)
            .FirstOrDefault();

        if (activeInterface != null)
        {
            var ipProp = activeInterface.GetIPProperties().UnicastAddresses
                .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork);

            if (ipProp != null)
            {
                return ipProp.Address.ToString();
            }
        }

        return "127.0.0.1";
    }
}

