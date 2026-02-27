using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Xml.Linq;
using SoundTouchMCP.Models;

namespace SoundTouchMCP.Services;

public class DeviceDiscoveryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private const int SoundTouchPort = 8090;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(500);

    public DeviceDiscoveryService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Scans the given subnet (CIDR notation, e.g. "192.168.1.0/24") for SoundTouch devices.
    /// If subnet is null or empty, the host's primary subnet is used.
    /// </summary>
    public async Task<List<DeviceConfiguration>> ScanSubnetAsync(
        string? subnet,
        CancellationToken cancellationToken = default)
    {
        var (baseAddress, prefixLength) = ParseSubnet(subnet);
        var ips = EnumerateHosts(baseAddress, prefixLength);

        var tasks = ips.Select(ip => ProbeHostAsync(ip, cancellationToken));
        var results = await Task.WhenAll(tasks);

        return results.Where(d => d != null).Cast<DeviceConfiguration>().ToList();
    }

    /// <summary>
    /// Returns the auto-detected subnet string (e.g. "192.168.1.0/24") from the host's primary interface.
    /// </summary>
    public static string GetHostSubnet()
    {
        var (baseAddress, prefixLength) = DetectHostSubnet();
        return $"{baseAddress}/{prefixLength}";
    }

    private async Task<DeviceConfiguration?> ProbeHostAsync(string ip, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(ProbeTimeout);

            using var client = _httpClientFactory.CreateClient();
            client.Timeout = ProbeTimeout;

            var url = $"http://{ip}:{SoundTouchPort}/info";
            var response = await client.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            var xml = await response.Content.ReadAsStringAsync(cts.Token);
            var doc = XDocument.Parse(xml);

            // Verify it looks like a SoundTouch /info response
            if (doc.Root?.Name.LocalName != "info")
                return null;

            var name = doc.Root.Element("name")?.Value;
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return new DeviceConfiguration { Name = name, IpAddress = ip };
        }
        catch
        {
            return null;
        }
    }

    private static (string baseAddress, int prefixLength) ParseSubnet(string? subnet)
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

        if (!int.TryParse(prefixPart, out var prefix) || prefix < 1 || prefix > 30)
            throw new ArgumentException($"Invalid prefix length: '{prefixPart}'. Must be 1-30.");

        return (GetNetworkAddress(address, prefix), prefix);
    }

    private static (string baseAddress, int prefixLength) DetectHostSubnet()
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

    private static string GetNetworkAddress(IPAddress address, int prefixLength)
    {
        var ipBytes = address.GetAddressBytes();
        var mask = PrefixToMask(prefixLength);
        var networkBytes = new byte[4];
        for (int i = 0; i < 4; i++)
            networkBytes[i] = (byte)(ipBytes[i] & mask[i]);
        return new IPAddress(networkBytes).ToString();
    }

    private static IEnumerable<string> EnumerateHosts(string networkAddress, int prefixLength)
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

    private static byte[] PrefixToMask(int prefixLength)
    {
        uint mask = prefixLength == 0 ? 0 : (uint)(0xFFFFFFFF << (32 - prefixLength));
        return ToBigEndianBytes(mask);
    }

    private static uint ToUInt32(byte[] bytes) =>
        (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);

    private static byte[] ToBigEndianBytes(uint value) =>
        [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static int CountBits(byte[] bytes)
    {
        int count = 0;
        foreach (var b in bytes)
        {
            var x = b;
            while (x != 0) { count += x & 1; x >>= 1; }
        }
        return count;
    }
}
