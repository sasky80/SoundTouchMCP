using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SoundTouchMCP.Models;
using SoundTouchMCP.Services;

namespace SoundTouchMCP.Tools;

[McpServerToolType]
public class SoundTouchTools
{
    private readonly ISoundTouchClient _client;
    private readonly IDeviceStoreService _deviceStore;
    private readonly ILogger<SoundTouchTools> _logger;
    private const int DefaultPort = DeviceConfiguration.DefaultPort;

    public SoundTouchTools(
        ISoundTouchClient client,
        IDeviceStoreService deviceStore,
        ILogger<SoundTouchTools> logger)
    {
        _client = client;
        _deviceStore = deviceStore;
        _logger = logger;
    }

    private async Task<DeviceConfiguration> GetDeviceByNameAsync(string deviceName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            throw new ArgumentException("Device name cannot be empty.", nameof(deviceName));

        var configuredDevices = await _deviceStore.GetDevicesAsync(cancellationToken);
        var device = configuredDevices.FirstOrDefault(d => 
            d.Name.Equals(deviceName.Trim(), StringComparison.OrdinalIgnoreCase));
        
        if (device == null)
        {
            if (configuredDevices.Count == 0)
                throw new ArgumentException(
                    "No devices are configured. Run discovery to populate the device store.");

            var availableDevices = string.Join(", ", configuredDevices.Select(d => d.Name));
            throw new ArgumentException(
                $"Device '{deviceName}' not found. Available devices: {availableDevices}");
        }
        
        return device;
    }

    private static int GetPort(DeviceConfiguration device)
    {
        return device.Port > 0 ? device.Port : DefaultPort;
    }

    private string FormatToolError(string actionDescription, Exception exception)
    {
        _logger.LogWarning(exception, "Tool operation failed: {ActionDescription}", actionDescription);
        return $"Failed to {actionDescription}. Check device name, connectivity, and configuration.";
    }

    private string FormatArgumentError(string actionDescription, ArgumentException exception)
    {
        _logger.LogInformation(exception, "Tool validation failed: {ActionDescription}", actionDescription);
        return exception.Message;
    }

    [McpServerTool]
    [Description("Turn a SoundTouch device on or off (standby mode)")]
    public async Task<string> PowerControl(
        [Description("Name of the device as configured in devices store")] string deviceName,
        [Description("True to power on, false to power off (standby)")] bool powerOn,
        CancellationToken cancellationToken)
    {
        try
        {
            var device = await GetDeviceByNameAsync(deviceName, cancellationToken);

            if (powerOn)
            {
                await _client.PowerOnAsync(device.IpAddress, GetPort(device), cancellationToken);
                return $"Device '{deviceName}' powered on successfully.";
            }
            else
            {
                await _client.PowerOffAsync(device.IpAddress, GetPort(device), cancellationToken);
                return $"Device '{deviceName}' powered off (standby mode).";
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return FormatArgumentError($"set power state for '{deviceName}'", ex);
        }
        catch (Exception ex)
        {
            return FormatToolError($"set power state for '{deviceName}'", ex);
        }
    }

    [McpServerTool]
    [Description("Increase the volume of a SoundTouch device by one level")]
    public async Task<string> VolumeUp(
        [Description("Name of the device")] string deviceName,
        CancellationToken cancellationToken)
    {
        try
        {
            var device = await GetDeviceByNameAsync(deviceName, cancellationToken);
            var port = GetPort(device);
            await _client.VolumeUpAsync(device.IpAddress, port, cancellationToken);

            var currentVolume = await _client.GetVolumeAsync(device.IpAddress, port, cancellationToken);
            return $"Volume increased. Current volume: {currentVolume}";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return FormatArgumentError($"increase volume on '{deviceName}'", ex);
        }
        catch (Exception ex)
        {
            return FormatToolError($"increase volume on '{deviceName}'", ex);
        }
    }

    [McpServerTool]
    [Description("Decrease the volume of a SoundTouch device by one level")]
    public async Task<string> VolumeDown(
        [Description("Name of the device")] string deviceName,
        CancellationToken cancellationToken)
    {
        try
        {
            var device = await GetDeviceByNameAsync(deviceName, cancellationToken);
            var port = GetPort(device);
            await _client.VolumeDownAsync(device.IpAddress, port, cancellationToken);

            var currentVolume = await _client.GetVolumeAsync(device.IpAddress, port, cancellationToken);
            return $"Volume decreased. Current volume: {currentVolume}";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return FormatArgumentError($"decrease volume on '{deviceName}'", ex);
        }
        catch (Exception ex)
        {
            return FormatToolError($"decrease volume on '{deviceName}'", ex);
        }
    }

    [McpServerTool]
    [Description("Set the volume of a SoundTouch device to a specific level (0-100)")]
    public async Task<string> SetVolume(
        [Description("Name of the device")] string deviceName,
        [Description("Volume level (0-100)")] int level,
        CancellationToken cancellationToken)
    {
        try
        {
            var device = await GetDeviceByNameAsync(deviceName, cancellationToken);
            await _client.SetVolumeAsync(device.IpAddress, level, GetPort(device), cancellationToken);
            return $"Volume set to {level}.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return FormatArgumentError($"set volume on '{deviceName}'", ex);
        }
        catch (Exception ex)
        {
            return FormatToolError($"set volume on '{deviceName}'", ex);
        }
    }

    [McpServerTool]
    [Description("List all configured presets for a SoundTouch device")]
    public async Task<string> ListPresets(
        [Description("Name of the device")] string deviceName,
        CancellationToken cancellationToken)
    {
        try
        {
            var device = await GetDeviceByNameAsync(deviceName, cancellationToken);
            var presets = await _client.GetPresetsAsync(device.IpAddress, GetPort(device), cancellationToken);

            if (presets.Count == 0)
            {
                return $"No presets configured for device '{deviceName}'.";
            }

            var presetList = string.Join("\n", presets.Select(p => $"  {p.Id}. {p.Name}"));
            return $"Presets for '{deviceName}':\n{presetList}";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return FormatArgumentError($"list presets for '{deviceName}'", ex);
        }
        catch (Exception ex)
        {
            return FormatToolError($"list presets for '{deviceName}'", ex);
        }
    }

    [McpServerTool]
    [Description("Play a preset on a SoundTouch device by name or number (1-6)")]
    public async Task<string> PlayPreset(
        [Description("Name of the device")] string deviceName,
        [Description("Preset name or number (1-6)")] string presetIdentifier,
        CancellationToken cancellationToken)
    {
        try
        {
            var device = await GetDeviceByNameAsync(deviceName, cancellationToken);
            var port = GetPort(device);
            var normalizedPresetIdentifier = presetIdentifier?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedPresetIdentifier))
                return "Preset identifier cannot be empty. Provide a preset number (1-6) or preset name.";

            // Try to parse as a number first
            if (int.TryParse(normalizedPresetIdentifier, out var presetNumber))
            {
                if (presetNumber < 1 || presetNumber > 6)
                {
                    return "Preset number must be between 1 and 6.";
                }

                await _client.PlayPresetAsync(device.IpAddress, presetNumber, port, cancellationToken);
                return $"Playing preset {presetNumber} on '{deviceName}'.";
            }

            // Otherwise, search by name
            var presets = await _client.GetPresetsAsync(device.IpAddress, port, cancellationToken);
            var preset = presets.FirstOrDefault(p =>
                p.Name.Equals(normalizedPresetIdentifier, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(normalizedPresetIdentifier, StringComparison.OrdinalIgnoreCase));

            if (preset == null)
            {
                if (presets.Count == 0)
                    return $"No presets are configured for device '{deviceName}'.";

                var availablePresets = string.Join(", ", presets.Select(p => $"{p.Id}: {p.Name}"));
                return $"Preset '{normalizedPresetIdentifier}' not found. Available presets: {availablePresets}";
            }

            await _client.PlayPresetAsync(device.IpAddress, preset.Id, port, cancellationToken);
            return $"Playing preset '{preset.Name}' (#{preset.Id}) on '{deviceName}'.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return FormatArgumentError($"play preset on '{deviceName}'", ex);
        }
        catch (Exception ex)
        {
            return FormatToolError($"play preset on '{deviceName}'", ex);
        }
    }

    [McpServerTool]
    [Description("Enter Bluetooth pairing mode on a SoundTouch device")]
    public async Task<string> EnterBluetoothPairing(
        [Description("Name of the device")] string deviceName,
        CancellationToken cancellationToken)
    {
        try
        {
            var device = await GetDeviceByNameAsync(deviceName, cancellationToken);
            await _client.EnterBluetoothPairingAsync(device.IpAddress, GetPort(device), cancellationToken);
            return $"Device '{deviceName}' is now in Bluetooth pairing mode. " +
                   "Look for the device in your phone/tablet Bluetooth settings to pair.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return FormatArgumentError($"enter Bluetooth pairing mode on '{deviceName}'", ex);
        }
        catch (Exception ex)
        {
            return FormatToolError($"enter Bluetooth pairing mode on '{deviceName}'", ex);
        }
    }

    [McpServerTool]
    [Description("Get information about a SoundTouch device")]
    public async Task<string> GetDeviceInfo(
        [Description("Name of the device")] string deviceName,
        CancellationToken cancellationToken)
    {
        try
        {
            var device = await GetDeviceByNameAsync(deviceName, cancellationToken);
            var port = GetPort(device);
            var info = await _client.GetDeviceInfoAsync(device.IpAddress, port, cancellationToken);

            return $"Device Information for '{deviceName}':\n" +
                   $"  Type: {info.Type}\n" +
                   $"  Device ID: {info.DeviceId}\n" +
                   $"  IP Address: {device.IpAddress}\n" +
                   $"  Port: {port}";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return FormatArgumentError($"get device info for '{deviceName}'", ex);
        }
        catch (Exception ex)
        {
            return FormatToolError($"get device info for '{deviceName}'", ex);
        }
    }

    [McpServerTool]
    [Description("List all configured SoundTouch devices")]
    public async Task<string> ListDevices(CancellationToken cancellationToken)
    {
        var configuredDevices = await _deviceStore.GetDevicesAsync(cancellationToken);

        if (configuredDevices.Count == 0)
        {
            return "No devices configured. Run discovery to populate the device store.";
        }
        
        var deviceList = string.Join("\n", configuredDevices.Select(d => $"  - {d.Name} ({d.IpAddress}:{GetPort(d)})"));
        return $"Configured devices:\n{deviceList}";
    }
}
