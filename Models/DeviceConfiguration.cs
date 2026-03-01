namespace SoundTouchMCP.Models;

public class SoundTouchConfiguration
{
    public List<DeviceConfiguration> Devices { get; set; } = new();
    public DiscoveryConfiguration Discovery { get; set; } = new();
}

public class DiscoveryConfiguration
{
    public int ProbeTimeoutMs { get; set; } = 3000;
    public ZeroconfDiscoveryConfiguration Zeroconf { get; set; } = new();
}

public class ZeroconfDiscoveryConfiguration
{
    public int ScanTimeMs { get; set; } = 5000;
    public int SocketRetries { get; set; } = 4;
    public int SocketRetryDelayMs { get; set; } = 1000;
    public int DiscoveryPasses { get; set; } = 2;
    public int PassDelayMs { get; set; } = 700;
}

public record DeviceConfiguration
{
    public const int DefaultPort = 8090;
    public string Name { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public int Port { get; init; } = DefaultPort;
}
