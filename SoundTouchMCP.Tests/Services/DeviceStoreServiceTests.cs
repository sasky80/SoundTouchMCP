using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoundTouchMCP.Models;
using SoundTouchMCP.Services;

namespace SoundTouchMCP.Tests.Services;

public class DeviceStoreServiceTests : IDisposable
{
    private const string DevicesPathEnvVar = "SOUNDTOUCH_DEVICES_PATH";

    private readonly string _testRootDirectory;

    public DeviceStoreServiceTests()
    {
        _testRootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SoundTouchMCP.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_testRootDirectory);
    }

    [Fact]
    public async Task GetDevicesAsync_InitializesFromFallback_AndDeduplicatesByIp()
    {
        var fallbackDevices = new List<DeviceConfiguration>
        {
            new() { Name = "Living Room", IpAddress = "192.168.1.20", Port = 8090 },
            new() { Name = "Duplicate", IpAddress = "192.168.1.20", Port = 8091 }
        };

        using var service = CreateService(fallbackDevices);

        var devices = await service.GetDevicesAsync();

        Assert.Single(devices);
        Assert.Equal("Living Room", devices[0].Name);
        Assert.Equal(8090, devices[0].Port);
        Assert.True(File.Exists(service.DevicesFilePath));
    }

    [Fact]
    public async Task ApplyChangesAsync_AddsUpdatesAndRemovesDevices()
    {
        using var service = CreateService([]);

        await service.ApplyChangesAsync(
            added:
            [
                new DeviceConfiguration
                {
                    Name = "Kitchen",
                    IpAddress = "192.168.1.30",
                    Port = 8090
                }
            ],
            updated: [],
            removed: []);

        var afterAdd = await service.GetDevicesAsync();
        Assert.Single(afterAdd);
        Assert.Equal("Kitchen", afterAdd[0].Name);

        await service.ApplyChangesAsync(
            added: [],
            updated:
            [
                new DeviceConfiguration
                {
                    Name = "Kitchen Updated",
                    IpAddress = "192.168.1.30",
                    Port = 9000
                }
            ],
            removed: []);

        var afterUpdate = await service.GetDevicesAsync();
        Assert.Single(afterUpdate);
        Assert.Equal("Kitchen Updated", afterUpdate[0].Name);
        Assert.Equal(9000, afterUpdate[0].Port);

        await service.ApplyChangesAsync(
            added: [],
            updated: [],
            removed:
            [
                new DeviceConfiguration
                {
                    Name = "Kitchen Updated",
                    IpAddress = "192.168.1.30",
                    Port = 9000
                }
            ]);

        var afterRemove = await service.GetDevicesAsync();
        Assert.Empty(afterRemove);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(DevicesPathEnvVar, null);

        if (Directory.Exists(_testRootDirectory))
            Directory.Delete(_testRootDirectory, recursive: true);
    }

    private DeviceStoreService CreateService(List<DeviceConfiguration> fallbackDevices)
    {
        var devicesFilePath = Path.Combine(_testRootDirectory, "devices.json");
        Environment.SetEnvironmentVariable(DevicesPathEnvVar, devicesFilePath);

        var options = Options.Create(
            new SoundTouchConfiguration
            {
                Devices = fallbackDevices
            });

        return new DeviceStoreService(options, NullLogger<DeviceStoreService>.Instance);
    }
}
