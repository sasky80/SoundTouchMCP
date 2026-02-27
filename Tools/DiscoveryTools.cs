using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using SoundTouchMCP.Models;
using SoundTouchMCP.Services;

namespace SoundTouchMCP.Tools;

[McpServerToolType]
public class DiscoveryTools
{
    private readonly DeviceDiscoveryService _discoveryService;
    private readonly SoundTouchConfiguration _config;
    private readonly string _configFilePath;

    public DiscoveryTools(
        DeviceDiscoveryService discoveryService,
        IOptions<SoundTouchConfiguration> config,
        IHostEnvironment hostEnvironment)
    {
        _discoveryService = discoveryService;
        _config = config.Value;
        _configFilePath = Path.Combine(hostEnvironment.ContentRootPath, "appsettings.json");
    }

    [McpServerTool]
    [Description(
        "Discover SoundTouch devices on a local network subnet and update the device list in appsettings.json. " +
        "New devices are added, existing ones are skipped, and devices not found are optionally removed.")]
    public async Task<string> DiscoverDevices(
        [Description(
            "Subnet to scan in CIDR notation (e.g. '192.168.1.0/24') or short form (e.g. '192.168.1'). " +
            "If omitted, the host's primary subnet is used automatically.")]
        string? subnet,
        [Description("If true, remove devices from the config that were not found during discovery. Default is false.")]
        bool removeNotFound,
        CancellationToken cancellationToken)
    {
        string resolvedSubnet;
        try
        {
            resolvedSubnet = string.IsNullOrWhiteSpace(subnet)
                ? DeviceDiscoveryService.GetHostSubnet()
                : subnet.Trim();
        }
        catch (InvalidOperationException ex)
        {
            return $"Could not determine subnet: {ex.Message}";
        }

        List<DeviceConfiguration> found;
        try
        {
            found = await _discoveryService.ScanSubnetAsync(resolvedSubnet, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return $"Invalid subnet: {ex.Message}";
        }

        var existingIps = new HashSet<string>(
            _config.Devices.Select(d => d.IpAddress),
            StringComparer.OrdinalIgnoreCase);

        var added = new List<DeviceConfiguration>();
        var skipped = new List<DeviceConfiguration>();

        foreach (var device in found)
        {
            if (existingIps.Contains(device.IpAddress))
                skipped.Add(device);
            else
                added.Add(device);
        }

        var removed = new List<DeviceConfiguration>();
        if (removeNotFound)
        {
            var foundIps = new HashSet<string>(
                found.Select(d => d.IpAddress),
                StringComparer.OrdinalIgnoreCase);

            removed = _config.Devices
                .Where(d => !foundIps.Contains(d.IpAddress))
                .ToList();
        }

        // Apply changes to in-memory config and persist
        if (added.Count > 0 || removed.Count > 0)
        {
            foreach (var d in added)
                _config.Devices.Add(d);
            foreach (var d in removed)
                _config.Devices.Remove(d);

            PersistConfig();
        }

        return BuildSummary(resolvedSubnet, found.Count, added, skipped, removed);
    }

    private void PersistConfig()
    {
        var json = File.ReadAllText(_configFilePath);
        var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })
            as JsonObject ?? new JsonObject();

        var devicesArray = new JsonArray();
        foreach (var device in _config.Devices)
        {
            devicesArray.Add(new JsonObject
            {
                ["Name"] = device.Name,
                ["IpAddress"] = device.IpAddress
            });
        }

        var soundTouch = root["SoundTouch"] as JsonObject ?? new JsonObject();
        soundTouch["Devices"] = devicesArray;
        root["SoundTouch"] = soundTouch;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_configFilePath, root.ToJsonString(options));
    }

    private static string BuildSummary(
        string subnet,
        int totalFound,
        List<DeviceConfiguration> added,
        List<DeviceConfiguration> skipped,
        List<DeviceConfiguration> removed)
    {
        var lines = new List<string>
        {
            $"Discovery complete on subnet {subnet}. Found {totalFound} SoundTouch device(s).",
            string.Empty
        };

        if (added.Count > 0)
        {
            lines.Add($"Added ({added.Count}):");
            lines.AddRange(added.Select(d => $"  + {d.Name} ({d.IpAddress})"));
        }

        if (skipped.Count > 0)
        {
            lines.Add($"Already known ({skipped.Count}):");
            lines.AddRange(skipped.Select(d => $"  = {d.Name} ({d.IpAddress})"));
        }

        if (removed.Count > 0)
        {
            lines.Add($"Removed ({removed.Count}):");
            lines.AddRange(removed.Select(d => $"  - {d.Name} ({d.IpAddress})"));
        }

        if (added.Count == 0 && removed.Count == 0)
            lines.Add("No changes made to appsettings.json.");
        else
            lines.Add("appsettings.json has been updated.");

        return string.Join("\n", lines);
    }
}
