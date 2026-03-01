using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SoundTouchMCP.Services;

public static class SubnetUtilities
{
    private const int MinSubnetPrefixLength = 16;
    private const int MaxSubnetPrefixLength = 30;
    private const int MaxSubnetHostCount = 4096;

    public static (string baseAddress, int prefixLength) ParseSubnet(string? subnet)
    {
        if (string.IsNullOrWhiteSpace(subnet))
            return DetectHostSubnet();

        subnet = subnet.Trim();

        // Accept short form like "192.168.1" → "192.168.1.0/24"
        if (!subnet.Contains('/'))
        {
            var parts = subnet.Split('.');
            if (parts.Length == 3)
                subnet = $"{subnet}.0/24";
            else if (parts.Length == 4)
                subnet = $"{subnet}/24";
            else
                throw new ArgumentException($"Cannot parse subnet '{subnet}'. Expected CIDR (e.g. 192.168.1.0/24).");
        }

        var slashIdx = subnet.IndexOf('/');
        var ipPart = subnet[..slashIdx];
        var prefixPart = subnet[(slashIdx + 1)..];

        if (!IPAddress.TryParse(ipPart, out var address) || address.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException($"Invalid IP in subnet: '{ipPart}'");

        if (!int.TryParse(prefixPart, out var prefix) ||
            prefix < MinSubnetPrefixLength ||
            prefix > MaxSubnetPrefixLength)
        {
            throw new ArgumentException(
                $"Invalid prefix length: '{prefixPart}'. Must be {MinSubnetPrefixLength}-{MaxSubnetPrefixLength}.");
        }

        var hostCount = GetHostCount(prefix);
        if (hostCount > MaxSubnetHostCount)
        {
            throw new ArgumentException(
                $"Subnet '{subnet}' is too broad ({hostCount} hosts). " +
                $"Use a subnet with at most {MaxSubnetHostCount} hosts.");
        }

        return (GetNetworkAddress(address, prefix), prefix);
    }

    public static (string baseAddress, int prefixLength) DetectHostSubnet()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up ||
                ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            foreach (var addr in ni.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                var ip = addr.Address;
                var mask = addr.IPv4Mask;
                if (mask == null || mask.Equals(IPAddress.Any))
                    continue;

                var prefix = CountBits(mask.GetAddressBytes());
                var network = GetNetworkAddress(ip, prefix);
                return (network, prefix);
            }
        }

        throw new InvalidOperationException(
            "Could not detect host subnet. Please provide a subnet explicitly (e.g. 192.168.1.0/24).");
    }

    public static IEnumerable<string> EnumerateHosts(string networkAddress, int prefixLength)
    {
        var netBytes = IPAddress.Parse(networkAddress).GetAddressBytes();
        var mask = PrefixToMask(prefixLength);

        uint network = ToUInt32(netBytes);
        uint broadcast = network | ~ToUInt32(mask);

        // Exclude network address and broadcast address
        for (uint ip = network + 1; ip < broadcast; ip++)
        {
            yield return new IPAddress(ToBigEndianBytes(ip)).ToString();
        }
    }

    private static string GetNetworkAddress(IPAddress address, int prefixLength)
    {
        var ipBytes = address.GetAddressBytes();
        var mask = PrefixToMask(prefixLength);
        var networkBytes = new byte[4];
        for (int i = 0; i < 4; i++)
            networkBytes[i] = (byte)(ipBytes[i] & mask[i]);
        return new IPAddress(networkBytes).ToString();
    }

    private static byte[] PrefixToMask(int prefixLength)
    {
        uint mask = prefixLength == 0 ? 0 : (uint)(0xFFFFFFFF << (32 - prefixLength));
        return ToBigEndianBytes(mask);
    }

    private static uint ToUInt32(byte[] bytes) =>
        (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);

    private static byte[] ToBigEndianBytes(uint value) =>
        [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static int GetHostCount(int prefixLength)
    {
        var hostBits = 32 - prefixLength;
        if (hostBits <= 1)
            return 0;

        return (1 << hostBits) - 2;
    }

    private static int CountBits(byte[] bytes)
    {
        int count = 0;
        foreach (var b in bytes)
        {
            var x = b;
            while (x != 0)
            {
                count += x & 1;
                x >>= 1;
            }
        }

        return count;
    }
}