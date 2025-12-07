using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using SoundTouchMCP.Models;
using SoundTouchMCP.Services;

namespace SoundTouchMCP.Tools;

[McpServerToolType]
public class SoundTouchTools
{
    private readonly SoundTouchClient _client;
    private readonly SoundTouchConfiguration _config;

    public SoundTouchTools(SoundTouchClient client, IOptions<SoundTouchConfiguration> config)
    {
        _client = client;
        _config = config.Value;
    }

    private DeviceConfiguration GetDeviceByName(string deviceName)
    {
        var device = _config.Devices.FirstOrDefault(d => 
            d.Name.Equals(deviceName, StringComparison.OrdinalIgnoreCase));
        
        if (device == null)
        {
            var availableDevices = string.Join(", ", _config.Devices.Select(d => d.Name));
            throw new ArgumentException(
                $"Device '{deviceName}' not found. Available devices: {availableDevices}");
        }
        
        return device;
    }

    [McpServerTool]
    [Description("Turn a SoundTouch device on or off (standby mode)")]
    public async Task<string> PowerControl(
        [Description("Name of the device as configured in appsettings.json")] string deviceName,
        [Description("True to power on, false to power off (standby)")] bool powerOn,
        CancellationToken cancellationToken)
    {
        var device = GetDeviceByName(deviceName);
        
        if (powerOn)
        {
            await _client.PowerOnAsync(device.IpAddress, cancellationToken);
            return $"Device '{deviceName}' powered on successfully.";
        }
        else
        {
            await _client.PowerOffAsync(device.IpAddress, cancellationToken);
            return $"Device '{deviceName}' powered off (standby mode).";
        }
    }

    [McpServerTool]
    [Description("Increase the volume of a SoundTouch device by one level")]
    public async Task<string> VolumeUp(
        [Description("Name of the device")] string deviceName,
        CancellationToken cancellationToken)
    {
        var device = GetDeviceByName(deviceName);
        await _client.VolumeUpAsync(device.IpAddress, cancellationToken);
        
        var currentVolume = await _client.GetVolumeAsync(device.IpAddress, cancellationToken);
        return $"Volume increased. Current volume: {currentVolume}";
    }

    [McpServerTool]
    [Description("Decrease the volume of a SoundTouch device by one level")]
    public async Task<string> VolumeDown(
        [Description("Name of the device")] string deviceName,
        CancellationToken cancellationToken)
    {
        var device = GetDeviceByName(deviceName);
        await _client.VolumeDownAsync(device.IpAddress, cancellationToken);
        
        var currentVolume = await _client.GetVolumeAsync(device.IpAddress, cancellationToken);
        return $"Volume decreased. Current volume: {currentVolume}";
    }

    [McpServerTool]
    [Description("Set the volume of a SoundTouch device to a specific level (0-100)")]
    public async Task<string> SetVolume(
        [Description("Name of the device")] string deviceName,
        [Description("Volume level (0-100)")] int level,
        CancellationToken cancellationToken)
    {
        var device = GetDeviceByName(deviceName);
        await _client.SetVolumeAsync(device.IpAddress, level, cancellationToken);
        return $"Volume set to {level}.";
    }

    [McpServerTool]
    [Description("List all configured presets for a SoundTouch device")]
    public async Task<string> ListPresets(
        [Description("Name of the device")] string deviceName,
        CancellationToken cancellationToken)
    {
        var device = GetDeviceByName(deviceName);
        var presets = await _client.GetPresetsAsync(device.IpAddress, cancellationToken);
        
        if (presets.Count == 0)
        {
            return $"No presets configured for device '{deviceName}'.";
        }
        
        var presetList = string.Join("\n", presets.Select(p => $"  {p.Id}. {p.Name}"));
        return $"Presets for '{deviceName}':\n{presetList}";
    }

    [McpServerTool]
    [Description("Play a preset on a SoundTouch device by name or number (1-6)")]
    public async Task<string> PlayPreset(
        [Description("Name of the device")] string deviceName,
        [Description("Preset name or number (1-6)")] string presetIdentifier,
        CancellationToken cancellationToken)
    {
        var device = GetDeviceByName(deviceName);
        
        // Try to parse as a number first
        if (int.TryParse(presetIdentifier, out var presetNumber))
        {
            if (presetNumber < 1 || presetNumber > 6)
            {
                return "Preset number must be between 1 and 6.";
            }
            
            await _client.PlayPresetAsync(device.IpAddress, presetNumber, cancellationToken);
            return $"Playing preset {presetNumber} on '{deviceName}'.";
        }
        
        // Otherwise, search by name
        var presets = await _client.GetPresetsAsync(device.IpAddress, cancellationToken);
        var preset = presets.FirstOrDefault(p => 
            p.Name.Equals(presetIdentifier, StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains(presetIdentifier, StringComparison.OrdinalIgnoreCase));
        
        if (preset == null)
        {
            var availablePresets = string.Join(", ", presets.Select(p => $"{p.Id}: {p.Name}"));
            return $"Preset '{presetIdentifier}' not found. Available presets: {availablePresets}";
        }
        
        await _client.PlayPresetAsync(device.IpAddress, preset.Id, cancellationToken);
        return $"Playing preset '{preset.Name}' (#{preset.Id}) on '{deviceName}'.";
    }

    [McpServerTool]
    [Description("Enter Bluetooth pairing mode on a SoundTouch device")]
    public async Task<string> EnterBluetoothPairing(
        [Description("Name of the device")] string deviceName,
        CancellationToken cancellationToken)
    {
        var device = GetDeviceByName(deviceName);
        await _client.EnterBluetoothPairingAsync(device.IpAddress, cancellationToken);
        return $"Device '{deviceName}' is now in Bluetooth pairing mode. " +
               "Look for the device in your phone/tablet Bluetooth settings to pair.";
    }

    [McpServerTool]
    [Description("Get information about a SoundTouch device")]
    public async Task<string> GetDeviceInfo(
        [Description("Name of the device")] string deviceName,
        CancellationToken cancellationToken)
    {
        var device = GetDeviceByName(deviceName);
        var info = await _client.GetDeviceInfoAsync(device.IpAddress, cancellationToken);
        
        return $"Device Information for '{deviceName}':\n" +
               $"  Type: {info.Type}\n" +
               $"  Device ID: {info.DeviceId}\n" +
               $"  IP Address: {device.IpAddress}";
    }

    [McpServerTool]
    [Description("List all configured SoundTouch devices")]
    public Task<string> ListDevices(CancellationToken cancellationToken)
    {
        if (_config.Devices.Count == 0)
        {
            return Task.FromResult("No devices configured. Please add devices to appsettings.json.");
        }
        
        var deviceList = string.Join("\n", _config.Devices.Select(d => $"  - {d.Name} ({d.IpAddress})"));
        return Task.FromResult($"Configured devices:\n{deviceList}");
    }
}
