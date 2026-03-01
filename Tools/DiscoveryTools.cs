using System.ComponentModel;
using ModelContextProtocol.Server;
using SoundTouchMCP.Models;
using SoundTouchMCP.Services;

namespace SoundTouchMCP.Tools;

[McpServerToolType]
public class DiscoveryTools
{
    private readonly IDeviceDiscoveryService _discoveryService;
    private readonly IDeviceStoreService _deviceStore;

    public DiscoveryTools(
        IDeviceDiscoveryService discoveryService,
        IDeviceStoreService deviceStore)
    {
        _discoveryService = discoveryService;
        _deviceStore = deviceStore;
    }

    [McpServerTool]
    [Description(
        "Discover SoundTouch devices via Zeroconf (_soundtouch._tcp.local.) and update devices store.")]
    public async Task<string> DiscoverDevices(
        [Description("If true, remove devices from the config that were not found during discovery. Default is false.")]
        bool removeNotFound = false,
        [Description("If true, force refresh the device list by removing all configured devices not found in this discovery run. Default is false.")]
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        return await DiscoverAndPersistAsync(
            async token => await _discoveryService.DiscoverViaZeroconfAsync(token),
            "zeroconf (_soundtouch._tcp.local.)",
            removeNotFound,
            forceRefresh,
            cancellationToken);
    }

    [McpServerTool]
    [Description(
        "Discover SoundTouch devices by scanning a subnet on port 8090 and update devices store.")]
    public async Task<string> DiscoverDevicesOnSubnet(
        [Description(
            "Subnet to scan in CIDR notation (e.g. '192.168.1.0/24') or short form (e.g. '192.168.1'). " +
            "If omitted, the host's primary subnet is used automatically.")]
        string? subnet = null,
        [Description("If true, remove devices from the config that were not found during discovery. Default is false.")]
        bool removeNotFound = false,
        [Description("If true, force refresh the device list by removing all configured devices not found in this discovery run. Default is false.")]
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        string resolvedSubnet;
        try
        {
            resolvedSubnet = string.IsNullOrWhiteSpace(subnet)
                ? _discoveryService.GetHostSubnet()
                : subnet.Trim();
        }
        catch (InvalidOperationException ex)
        {
            return $"Could not determine subnet: {ex.Message}";
        }

        try
        {
            return await DiscoverAndPersistAsync(
                async token => await _discoveryService.ScanSubnetAsync(resolvedSubnet, token),
                $"subnet scan ({resolvedSubnet}, port 8090)",
                removeNotFound,
                forceRefresh,
                cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return $"Invalid subnet: {ex.Message}";
        }
    }

    private async Task<string> DiscoverAndPersistAsync(
        Func<CancellationToken, Task<List<DeviceConfiguration>>> discover,
        string discoveryMethod,
        bool removeNotFound,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var effectiveRemoveNotFound = removeNotFound || forceRefresh;

        var found = await discover(cancellationToken);

        var added = new List<DeviceConfiguration>();
        var skipped = new List<DeviceConfiguration>();
        var updated = new List<DeviceConfiguration>();
        var configuredDevices = await _deviceStore.GetDevicesAsync(cancellationToken);

        var existingByIp = configuredDevices
            .GroupBy(d => d.IpAddress, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var device in found)
        {
            if (!existingByIp.TryGetValue(device.IpAddress, out var existing))
            {
                added.Add(device);
                continue;
            }

            var changed = false;
            if (!string.Equals(existing.Name, device.Name, StringComparison.Ordinal))
            {
                changed = true;
            }

            if (existing.Port != device.Port)
            {
                changed = true;
            }

            if (changed)
                updated.Add(device);
            else
                skipped.Add(device);
        }

        var removed = new List<DeviceConfiguration>();
        if (effectiveRemoveNotFound)
        {
            var foundIps = new HashSet<string>(
                found.Select(d => d.IpAddress),
                StringComparer.OrdinalIgnoreCase);

            removed = configuredDevices
                .Where(d => !foundIps.Contains(d.IpAddress))
                .ToList();
        }

        if (added.Count > 0 || updated.Count > 0 || removed.Count > 0)
            await _deviceStore.ApplyChangesAsync(added, updated, removed, cancellationToken);

        return BuildSummary(discoveryMethod, found.Count, added, updated, skipped, removed);
    }

    private static string BuildSummary(
        string method,
        int totalFound,
        List<DeviceConfiguration> added,
        List<DeviceConfiguration> updated,
        List<DeviceConfiguration> skipped,
        List<DeviceConfiguration> removed)
    {
        var lines = new List<string>
        {
            $"Discovery complete using {method}. Found {totalFound} SoundTouch device(s).",
            string.Empty
        };

        if (added.Count > 0)
        {
            lines.Add($"Added ({added.Count}):");
            lines.AddRange(added.Select(d => $"  + {d.Name} ({d.IpAddress}:{d.Port})"));
        }

        if (updated.Count > 0)
        {
            lines.Add($"Updated ({updated.Count}):");
            lines.AddRange(updated.Select(d => $"  ~ {d.Name} ({d.IpAddress}:{d.Port})"));
        }

        if (skipped.Count > 0)
        {
            lines.Add($"Already known ({skipped.Count}):");
            lines.AddRange(skipped.Select(d => $"  = {d.Name} ({d.IpAddress}:{d.Port})"));
        }

        if (removed.Count > 0)
        {
            lines.Add($"Removed ({removed.Count}):");
            lines.AddRange(removed.Select(d => $"  - {d.Name} ({d.IpAddress}:{d.Port})"));
        }

        if (added.Count == 0 && updated.Count == 0 && removed.Count == 0)
            lines.Add("No changes made to the device store.");
        else
            lines.Add("The device store has been updated.");

        return string.Join("\n", lines);
    }
}
