using SoundTouchMCP.Models;

namespace SoundTouchMCP.Services;

public interface ISoundTouchClient
{
    Task PowerOnAsync(string ipAddress, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default);
    Task PowerOffAsync(string ipAddress, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default);
    Task VolumeUpAsync(string ipAddress, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default);
    Task VolumeDownAsync(string ipAddress, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default);
    Task SetVolumeAsync(string ipAddress, int level, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default);
    Task<int> GetVolumeAsync(string ipAddress, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default);
    Task<List<Preset>> GetPresetsAsync(string ipAddress, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default);
    Task PlayPresetAsync(string ipAddress, int presetNumber, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default);
    Task EnterBluetoothPairingAsync(string ipAddress, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default);
    Task<DeviceInfo> GetDeviceInfoAsync(string ipAddress, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default);
}
