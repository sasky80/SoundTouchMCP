using SoundTouchMCP.Models;
using SoundTouchMCP.Services;

namespace SoundTouchMCP.Tests.TestDoubles;

internal sealed class StubDeviceStoreService : IDeviceStoreService
{
    private readonly List<DeviceConfiguration> _devices;

    public StubDeviceStoreService(IEnumerable<DeviceConfiguration>? devices = null)
    {
        _devices = devices?.ToList() ?? [];
    }

    public string DevicesFilePath => "stub://devices.json";

    public bool ThrowOnGetDevices { get; set; }

    public List<DeviceConfiguration> LastAdded { get; private set; } = [];
    public List<DeviceConfiguration> LastUpdated { get; private set; } = [];
    public List<DeviceConfiguration> LastRemoved { get; private set; } = [];

    public Task<IReadOnlyList<DeviceConfiguration>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        if (ThrowOnGetDevices)
            throw new InvalidOperationException("GetDevices failed");

        return Task.FromResult<IReadOnlyList<DeviceConfiguration>>(_devices.ToList());
    }

    public Task ApplyChangesAsync(
        IEnumerable<DeviceConfiguration> added,
        IEnumerable<DeviceConfiguration> updated,
        IEnumerable<DeviceConfiguration> removed,
        CancellationToken cancellationToken = default)
    {
        LastAdded = added.ToList();
        LastUpdated = updated.ToList();
        LastRemoved = removed.ToList();

        var byIp = _devices
            .GroupBy(device => device.IpAddress, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var device in LastAdded)
        {
            if (!byIp.ContainsKey(device.IpAddress))
            {
                _devices.Add(device);
                byIp[device.IpAddress] = device;
            }
        }

        foreach (var device in LastUpdated)
        {
            if (!byIp.TryGetValue(device.IpAddress, out var existing))
                continue;

            var index = _devices.IndexOf(existing);
            if (index >= 0)
            {
                _devices[index] = existing with
                {
                    Name = device.Name,
                    Port = device.Port
                };
                byIp[device.IpAddress] = _devices[index];
            }
        }

        if (LastRemoved.Count > 0)
        {
            var removedSet = LastRemoved
                .Select(device => device.IpAddress)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _devices.RemoveAll(device => removedSet.Contains(device.IpAddress));
        }

        return Task.CompletedTask;
    }
}

internal sealed class StubSoundTouchClient : ISoundTouchClient
{
    public bool ThrowOnCall { get; set; }

    public List<(string IpAddress, int Port)> PowerOnCalls { get; } = [];
    public List<(string IpAddress, int Port)> PowerOffCalls { get; } = [];
    public List<(string IpAddress, int Port)> VolumeUpCalls { get; } = [];
    public List<(string IpAddress, int Port)> VolumeDownCalls { get; } = [];
    public List<(string IpAddress, int Port)> EnterBluetoothPairingCalls { get; } = [];
    public List<(string IpAddress, int Port)> GetDeviceInfoCalls { get; } = [];
    public List<(string IpAddress, int Level, int Port)> SetVolumeCalls { get; } = [];
    public List<(string IpAddress, int PresetNumber, int Port)> PlayPresetCalls { get; } = [];

    public int VolumeToReturn { get; set; } = 25;
    public List<Preset> PresetsToReturn { get; set; } = [];
    public DeviceInfo DeviceInfoToReturn { get; set; } = new() { DeviceId = "id", Name = "name", Type = "type" };

    public Task PowerOnAsync(string ipAddress, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default)
    {
        ThrowIfNeeded();
        PowerOnCalls.Add((ipAddress, port));
        return Task.CompletedTask;
    }

    public Task PowerOffAsync(string ipAddress, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default)
    {
        ThrowIfNeeded();
        PowerOffCalls.Add((ipAddress, port));
        return Task.CompletedTask;
    }

    public Task VolumeUpAsync(string ipAddress, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default)
    {
        ThrowIfNeeded();
        VolumeUpCalls.Add((ipAddress, port));
        return Task.CompletedTask;
    }

    public Task VolumeDownAsync(string ipAddress, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default)
    {
        ThrowIfNeeded();
        VolumeDownCalls.Add((ipAddress, port));
        return Task.CompletedTask;
    }

    public Task SetVolumeAsync(string ipAddress, int level, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default)
    {
        ThrowIfNeeded();
        SetVolumeCalls.Add((ipAddress, level, port));
        return Task.CompletedTask;
    }

    public Task<int> GetVolumeAsync(string ipAddress, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default)
    {
        ThrowIfNeeded();
        return Task.FromResult(VolumeToReturn);
    }

    public Task<List<Preset>> GetPresetsAsync(string ipAddress, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default)
    {
        ThrowIfNeeded();
        return Task.FromResult(PresetsToReturn.ToList());
    }

    public Task PlayPresetAsync(string ipAddress, int presetNumber, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default)
    {
        ThrowIfNeeded();
        PlayPresetCalls.Add((ipAddress, presetNumber, port));
        return Task.CompletedTask;
    }

    public Task EnterBluetoothPairingAsync(string ipAddress, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default)
    {
        ThrowIfNeeded();
        EnterBluetoothPairingCalls.Add((ipAddress, port));
        return Task.CompletedTask;
    }

    public Task<DeviceInfo> GetDeviceInfoAsync(string ipAddress, int port = DeviceConfiguration.DefaultPort, CancellationToken cancellationToken = default)
    {
        ThrowIfNeeded();
        GetDeviceInfoCalls.Add((ipAddress, port));
        return Task.FromResult(DeviceInfoToReturn);
    }

    private void ThrowIfNeeded()
    {
        if (ThrowOnCall)
            throw new InvalidOperationException("Client call failed");
    }
}

internal sealed class StubDeviceDiscoveryService : IDeviceDiscoveryService
{
    public List<DeviceConfiguration> ZeroconfResult { get; set; } = [];
    public List<DeviceConfiguration> SubnetResult { get; set; } = [];
    public string HostSubnet { get; set; } = "192.168.1.0/24";

    public Exception? DiscoverViaZeroconfException { get; set; }
    public Exception? ScanSubnetException { get; set; }
    public Exception? GetHostSubnetException { get; set; }

    public string? LastSubnetInput { get; private set; }

    public Task<List<DeviceConfiguration>> DiscoverViaZeroconfAsync(CancellationToken cancellationToken = default)
    {
        if (DiscoverViaZeroconfException is not null)
            throw DiscoverViaZeroconfException;

        return Task.FromResult(ZeroconfResult.ToList());
    }

    public Task<List<DeviceConfiguration>> ScanSubnetAsync(string? subnet, CancellationToken cancellationToken = default)
    {
        LastSubnetInput = subnet;

        if (ScanSubnetException is not null)
            throw ScanSubnetException;

        return Task.FromResult(SubnetResult.ToList());
    }

    public string GetHostSubnet()
    {
        if (GetHostSubnetException is not null)
            throw GetHostSubnetException;

        return HostSubnet;
    }
}
