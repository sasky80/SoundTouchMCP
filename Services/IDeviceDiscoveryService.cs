using SoundTouchMCP.Models;

namespace SoundTouchMCP.Services;

public interface IDeviceDiscoveryService
{
    Task<List<DeviceConfiguration>> DiscoverViaZeroconfAsync(CancellationToken cancellationToken = default);
    Task<List<DeviceConfiguration>> ScanSubnetAsync(string? subnet, CancellationToken cancellationToken = default);
    string GetHostSubnet();
}
