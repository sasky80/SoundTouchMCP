using SoundTouchMCP.Models;
using SoundTouchMCP.Tests.TestDoubles;
using SoundTouchMCP.Tools;

namespace SoundTouchMCP.Tests.Tools;

public class DiscoveryToolsTests
{
    [Fact]
    public async Task DiscoverDevices_AddsNewDevices_AndPersistsChanges()
    {
        var discovery = new StubDeviceDiscoveryService
        {
            ZeroconfResult =
            [
                new DeviceConfiguration { Name = "Kitchen", IpAddress = "192.168.1.30", Port = 8090 }
            ]
        };
        var store = new StubDeviceStoreService([]);
        var tools = new DiscoveryTools(discovery, store);

        var result = await tools.DiscoverDevices(removeNotFound: false, forceRefresh: false, CancellationToken.None);

        Assert.Contains("Found 1 SoundTouch device(s)", result, StringComparison.Ordinal);
        Assert.Contains("Added (1):", result, StringComparison.Ordinal);
        Assert.Single(store.LastAdded);
        Assert.Equal("192.168.1.30", store.LastAdded[0].IpAddress);
        Assert.Empty(store.LastUpdated);
        Assert.Empty(store.LastRemoved);
    }

    [Fact]
    public async Task DiscoverDevices_UpdatesExistingDevice_WhenNameChanges()
    {
        var discovery = new StubDeviceDiscoveryService
        {
            ZeroconfResult =
            [
                new DeviceConfiguration { Name = "Kitchen New", IpAddress = "192.168.1.30", Port = 9000 }
            ]
        };
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Kitchen Old", IpAddress = "192.168.1.30", Port = 8090 }
        ]);
        var tools = new DiscoveryTools(discovery, store);

        var result = await tools.DiscoverDevices(removeNotFound: false, forceRefresh: false, CancellationToken.None);

        Assert.Contains("Updated (1):", result, StringComparison.Ordinal);
        Assert.Single(store.LastUpdated);
        Assert.Equal("Kitchen New", store.LastUpdated[0].Name);
        Assert.Equal(9000, store.LastUpdated[0].Port);
    }

    [Fact]
    public async Task DiscoverDevices_RemovesMissingDevices_WhenForceRefreshEnabled()
    {
        var discovery = new StubDeviceDiscoveryService
        {
            ZeroconfResult =
            [
                new DeviceConfiguration { Name = "Living Room", IpAddress = "192.168.1.10", Port = 8090 }
            ]
        };
        var store = new StubDeviceStoreService(
        [
            new DeviceConfiguration { Name = "Living Room", IpAddress = "192.168.1.10", Port = 8090 },
            new DeviceConfiguration { Name = "Bedroom", IpAddress = "192.168.1.11", Port = 8090 }
        ]);
        var tools = new DiscoveryTools(discovery, store);

        var result = await tools.DiscoverDevices(removeNotFound: false, forceRefresh: true, CancellationToken.None);

        Assert.Contains("Removed (1):", result, StringComparison.Ordinal);
        Assert.Single(store.LastRemoved);
        Assert.Equal("192.168.1.11", store.LastRemoved[0].IpAddress);
    }

    [Fact]
    public async Task DiscoverDevicesOnSubnet_ReturnsInvalidSubnetMessage_WhenDiscoveryThrowsArgumentException()
    {
        var discovery = new StubDeviceDiscoveryService
        {
            ScanSubnetException = new ArgumentException("bad subnet")
        };
        var store = new StubDeviceStoreService([]);
        var tools = new DiscoveryTools(discovery, store);

        var result = await tools.DiscoverDevicesOnSubnet(
            subnet: "192.168",
            removeNotFound: false,
            forceRefresh: false,
            cancellationToken: CancellationToken.None);

        Assert.Equal("Invalid subnet: bad subnet", result);
    }

    [Fact]
    public async Task DiscoverDevicesOnSubnet_ReturnsHostSubnetError_WhenAutodetectFails()
    {
        var discovery = new StubDeviceDiscoveryService
        {
            GetHostSubnetException = new InvalidOperationException("no interface")
        };
        var store = new StubDeviceStoreService([]);
        var tools = new DiscoveryTools(discovery, store);

        var result = await tools.DiscoverDevicesOnSubnet(
            subnet: null,
            removeNotFound: false,
            forceRefresh: false,
            cancellationToken: CancellationToken.None);

        Assert.Equal("Could not determine subnet: no interface", result);
    }
}
