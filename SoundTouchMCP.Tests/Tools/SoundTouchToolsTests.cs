using Microsoft.Extensions.Logging.Abstractions;
using SoundTouchMCP.Models;
using SoundTouchMCP.Tests.TestDoubles;
using SoundTouchMCP.Tools;

namespace SoundTouchMCP.Tests.Tools;

public class SoundTouchToolsTests
{
    private const string EmptyDevicesMessage = "No devices configured. Run discovery to populate the device store.";

    [Fact]
    public async Task ListDevices_ReturnsConfiguredDevicesList()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Living Room", IpAddress = "192.168.1.10", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient();
        var tools = CreateTools(client, store);

        var result = await tools.ListDevices(CancellationToken.None);

        Assert.Contains("Configured devices:", result, StringComparison.Ordinal);
        Assert.Contains("Living Room (192.168.1.10:8090)", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListDevices_ReturnsEmptyMessage_WhenNoDevicesConfigured()
    {
        var store = new StubDeviceStoreService([]);
        var client = new StubSoundTouchClient();
        var tools = CreateTools(client, store);

        var result = await tools.ListDevices(CancellationToken.None);

        Assert.Equal(EmptyDevicesMessage, result);
    }

    [Fact]
    public async Task PowerControl_PowerOn_CallsClientAndReturnsSuccessMessage()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Kitchen", IpAddress = "192.168.1.20", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient();
        var tools = CreateTools(client, store);

        var result = await tools.PowerControl("Kitchen", true, CancellationToken.None);

        Assert.Single(client.PowerOnCalls);
        Assert.Equal(("192.168.1.20", 8090), client.PowerOnCalls[0]);
        Assert.Equal("Device 'Kitchen' powered on successfully.", result);
    }

    [Fact]
    public async Task PowerControl_PowerOff_CallsClientAndReturnsSuccessMessage()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Kitchen", IpAddress = "192.168.1.20", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient();
        var tools = CreateTools(client, store);

        var result = await tools.PowerControl("Kitchen", false, CancellationToken.None);

        Assert.Single(client.PowerOffCalls);
        Assert.Equal(("192.168.1.20", 8090), client.PowerOffCalls[0]);
        Assert.Equal("Device 'Kitchen' powered off (standby mode).", result);
    }

    [Fact]
    public async Task PowerControl_WhenDeviceMissing_ReturnsArgumentErrorMessage()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Kitchen", IpAddress = "192.168.1.20", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient();
        var tools = CreateTools(client, store);

        var result = await tools.PowerControl("Bedroom", true, CancellationToken.None);

        Assert.Contains("Device 'Bedroom' not found.", result, StringComparison.Ordinal);
        Assert.Contains("Available devices: Kitchen", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetVolume_WhenClientThrows_ReturnsGenericFailureMessage()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Office", IpAddress = "192.168.1.21", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient { ThrowOnCall = true };
        var tools = CreateTools(client, store);

        var result = await tools.SetVolume("Office", 45, CancellationToken.None);

        Assert.Equal("Failed to set volume on 'Office'. Check device name, connectivity, and configuration.", result);
    }

    [Fact]
    public async Task SetVolume_UsesDefaultPort_WhenConfiguredPortIsZero()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Office", IpAddress = "192.168.1.21", Port = 0 }
        ]);
        var client = new StubSoundTouchClient();
        var tools = CreateTools(client, store);

        var result = await tools.SetVolume("Office", 33, CancellationToken.None);

        Assert.Equal("Volume set to 33.", result);
        Assert.Single(client.SetVolumeCalls);
        Assert.Equal(("192.168.1.21", 33, DeviceConfiguration.DefaultPort), client.SetVolumeCalls[0]);
    }

    [Fact]
    public async Task VolumeUp_ReturnsCurrentVolume_AndCallsClient()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Bedroom", IpAddress = "192.168.1.22", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient { VolumeToReturn = 41 };
        var tools = CreateTools(client, store);

        var result = await tools.VolumeUp("Bedroom", CancellationToken.None);

        Assert.Single(client.VolumeUpCalls);
        Assert.Equal(("192.168.1.22", 8090), client.VolumeUpCalls[0]);
        Assert.Equal("Volume increased. Current volume: 41", result);
    }

    [Fact]
    public async Task VolumeDown_ReturnsCurrentVolume_AndCallsClient()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Bedroom", IpAddress = "192.168.1.22", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient { VolumeToReturn = 17 };
        var tools = CreateTools(client, store);

        var result = await tools.VolumeDown("Bedroom", CancellationToken.None);

        Assert.Single(client.VolumeDownCalls);
        Assert.Equal(("192.168.1.22", 8090), client.VolumeDownCalls[0]);
        Assert.Equal("Volume decreased. Current volume: 17", result);
    }

    [Fact]
    public async Task VolumeUp_ReturnsArgumentError_WhenDeviceNameIsEmpty()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Bedroom", IpAddress = "192.168.1.22", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient();
        var tools = CreateTools(client, store);

        var result = await tools.VolumeUp("   ", CancellationToken.None);

        Assert.Equal("Device name cannot be empty. (Parameter 'deviceName')", result);
    }

    [Fact]
    public async Task VolumeDown_ReturnsGenericFailure_WhenClientThrows()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Bedroom", IpAddress = "192.168.1.22", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient { ThrowOnCall = true };
        var tools = CreateTools(client, store);

        var result = await tools.VolumeDown("Bedroom", CancellationToken.None);

        Assert.Equal("Failed to decrease volume on 'Bedroom'. Check device name, connectivity, and configuration.", result);
    }

    [Fact]
    public async Task ListPresets_ReturnsPresetList_WhenConfigured()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Office", IpAddress = "192.168.1.21", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient
        {
            PresetsToReturn =
            [
                new Preset { Id = 1, Name = "Jazz" },
                new Preset { Id = 2, Name = "News" }
            ]
        };
        var tools = CreateTools(client, store);

        var result = await tools.ListPresets("Office", CancellationToken.None);

        Assert.Contains("Presets for 'Office':", result, StringComparison.Ordinal);
        Assert.Contains("1. Jazz", result, StringComparison.Ordinal);
        Assert.Contains("2. News", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListPresets_ReturnsEmptyMessage_WhenNoPresets()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Office", IpAddress = "192.168.1.21", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient { PresetsToReturn = [] };
        var tools = CreateTools(client, store);

        var result = await tools.ListPresets("Office", CancellationToken.None);

        Assert.Equal("No presets configured for device 'Office'.", result);
    }

    [Fact]
    public async Task PlayPreset_ByNumber_CallsClientWithPresetNumber()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Office", IpAddress = "192.168.1.21", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient();
        var tools = CreateTools(client, store);

        var result = await tools.PlayPreset("Office", "2", CancellationToken.None);

        Assert.Single(client.PlayPresetCalls);
        Assert.Equal(("192.168.1.21", 2, 8090), client.PlayPresetCalls[0]);
        Assert.Equal("Playing preset 2 on 'Office'.", result);
    }

    [Fact]
    public async Task PlayPreset_ByName_WhenNotFound_ReturnsAvailablePresetsMessage()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Office", IpAddress = "192.168.1.21", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient
        {
            PresetsToReturn =
            [
                new Preset { Id = 1, Name = "Jazz" },
                new Preset { Id = 2, Name = "News" }
            ]
        };
        var tools = CreateTools(client, store);

        var result = await tools.PlayPreset("Office", "Rock", CancellationToken.None);

        Assert.Contains("Preset 'Rock' not found.", result, StringComparison.Ordinal);
        Assert.Contains("1: Jazz", result, StringComparison.Ordinal);
        Assert.Contains("2: News", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlayPreset_ByName_UsesMatchingPreset()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Office", IpAddress = "192.168.1.21", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient
        {
            PresetsToReturn =
            [
                new Preset { Id = 4, Name = "Top Rock" },
                new Preset { Id = 5, Name = "Talk" }
            ]
        };
        var tools = CreateTools(client, store);

        var result = await tools.PlayPreset("Office", "rock", CancellationToken.None);

        Assert.Single(client.PlayPresetCalls);
        Assert.Equal(("192.168.1.21", 4, 8090), client.PlayPresetCalls[0]);
        Assert.Equal("Playing preset 'Top Rock' (#4) on 'Office'.", result);
    }

    [Fact]
    public async Task PlayPreset_ReturnsValidationMessage_WhenIdentifierIsEmpty()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Office", IpAddress = "192.168.1.21", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient();
        var tools = CreateTools(client, store);

        var result = await tools.PlayPreset("Office", "  ", CancellationToken.None);

        Assert.Equal("Preset identifier cannot be empty. Provide a preset number (1-6) or preset name.", result);
    }

    [Fact]
    public async Task PlayPreset_ReturnsValidationMessage_WhenNumberOutOfRange()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Office", IpAddress = "192.168.1.21", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient();
        var tools = CreateTools(client, store);

        var result = await tools.PlayPreset("Office", "7", CancellationToken.None);

        Assert.Equal("Preset number must be between 1 and 6.", result);
    }

    [Fact]
    public async Task PlayPreset_ByName_ReturnsNoPresetsMessage_WhenNoneConfigured()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Office", IpAddress = "192.168.1.21", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient { PresetsToReturn = [] };
        var tools = CreateTools(client, store);

        var result = await tools.PlayPreset("Office", "Rock", CancellationToken.None);

        Assert.Equal("No presets are configured for device 'Office'.", result);
    }

    [Fact]
    public async Task EnterBluetoothPairing_CallsClientAndReturnsSuccessMessage()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Office", IpAddress = "192.168.1.21", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient();
        var tools = CreateTools(client, store);

        var result = await tools.EnterBluetoothPairing("Office", CancellationToken.None);

        Assert.Single(client.EnterBluetoothPairingCalls);
        Assert.Equal(("192.168.1.21", 8090), client.EnterBluetoothPairingCalls[0]);
        Assert.Contains("is now in Bluetooth pairing mode", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDeviceInfo_ReturnsFormattedDeviceDetails()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Office", IpAddress = "192.168.1.21", Port = 0 }
        ]);
        var client = new StubSoundTouchClient
        {
            DeviceInfoToReturn = new DeviceInfo
            {
                DeviceId = "abc123",
                Name = "Office Speaker",
                Type = "SoundTouch"
            }
        };
        var tools = CreateTools(client, store);

        var result = await tools.GetDeviceInfo("Office", CancellationToken.None);

        Assert.Single(client.GetDeviceInfoCalls);
        Assert.Equal(("192.168.1.21", DeviceConfiguration.DefaultPort), client.GetDeviceInfoCalls[0]);
        Assert.Contains("Type: SoundTouch", result, StringComparison.Ordinal);
        Assert.Contains("Device ID: abc123", result, StringComparison.Ordinal);
        Assert.Contains("IP Address: 192.168.1.21", result, StringComparison.Ordinal);
        Assert.Contains($"Port: {DeviceConfiguration.DefaultPort}", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDeviceInfo_ReturnsGenericFailure_WhenClientThrows()
    {
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Office", IpAddress = "192.168.1.21", Port = 8090 }
        ]);
        var client = new StubSoundTouchClient { ThrowOnCall = true };
        var tools = CreateTools(client, store);

        var result = await tools.GetDeviceInfo("Office", CancellationToken.None);

        Assert.Equal("Failed to get device info for 'Office'. Check device name, connectivity, and configuration.", result);
    }

    [Fact]
    public async Task SetVolume_ReturnsArgumentError_WhenNoDevicesConfigured()
    {
        var store = new StubDeviceStoreService([]);
        var client = new StubSoundTouchClient();
        var tools = CreateTools(client, store);

        var result = await tools.SetVolume("Office", 15, CancellationToken.None);

        Assert.Equal("No devices are configured. Run discovery to populate the device store.", result);
    }

    private static SoundTouchTools CreateTools(StubSoundTouchClient client, StubDeviceStoreService store)
    {
        return new SoundTouchTools(client, store, NullLogger<SoundTouchTools>.Instance);
    }
}
