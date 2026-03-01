using System.Net;
using System.Net.Sockets;

namespace SoundTouchMCP.Services;

public static class NetworkAddressGuard
{
    public static bool TryParseAllowedDeviceAddress(string ipAddress, out IPAddress? parsedAddress)
    {
        parsedAddress = null;

        if (string.IsNullOrWhiteSpace(ipAddress) ||
            !IPAddress.TryParse(ipAddress, out var parsed) ||
            !IsAllowedPrivateOrLinkLocalIPv4(parsed))
        {
            return false;
        }

        parsedAddress = parsed;
        return true;
    }

    public static bool IsAllowedPrivateOrLinkLocalIPv4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
            return false;

        var bytes = address.GetAddressBytes();

        if (bytes[0] == 10)
            return true;

        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            return true;

        if (bytes[0] == 192 && bytes[1] == 168)
            return true;

        if (bytes[0] == 169 && bytes[1] == 254)
            return true;

        return false;
    }
}