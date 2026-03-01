using SoundTouchMCP.Models;

namespace SoundTouchMCP.Services;

public interface IDeviceStoreService
{
    string DevicesFilePath { get; }
    Task<IReadOnlyList<DeviceConfiguration>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task ApplyChangesAsync(
        IEnumerable<DeviceConfiguration> added,
        IEnumerable<DeviceConfiguration> updated,
        IEnumerable<DeviceConfiguration> removed,
        CancellationToken cancellationToken = default);
}
