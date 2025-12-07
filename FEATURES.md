# SoundTouch MCP Server - Features Summary

## Implemented Features ✅

### 1. Power Control
- **Turn On**: Powers on the device using the POWER key press/release
- **Turn Off**: Puts the device into standby mode using the `/standby` endpoint

### 2. Volume Control
- **Volume Up**: Increases volume by one level
- **Volume Down**: Decreases volume by one level
- **Set Volume**: Sets volume to a specific level (0-100)
- **Get Volume**: Retrieves current volume level

### 3. Preset Management
- **List Presets**: Shows all configured presets (1-6) with their names
- **Play Preset by Number**: Select and play preset 1-6
- **Play Preset by Name**: Search and play preset by name (supports partial matching)

### 4. Bluetooth Pairing
- **Enter Bluetooth Pairing Mode**: Puts device into pairing mode to connect with phones/tablets

### 5. Device Information
- **Get Device Info**: Retrieves device type, ID, and IP address
- **List Devices**: Shows all configured devices from appsettings.json

## MCP Tools Available

All tools are exposed through the Model Context Protocol:

1. `PowerControl` - Control device power state
2. `VolumeUp` - Increase volume
3. `VolumeDown` - Decrease volume
4. `SetVolume` - Set specific volume level
5. `ListPresets` - List all presets
6. `PlayPreset` - Play preset by name or number
7. `EnterBluetoothPairing` - Enter Bluetooth pairing mode
8. `GetDeviceInfo` - Get device information
9. `ListDevices` - List all configured devices

## Configuration

Devices are configured in `appsettings.json`:

```json
{
  "SoundTouch": {
    "Devices": [
      {
        "Name": "Living Room Speaker",
        "IpAddress": "192.168.1.131"
      },
      {
        "Name": "Bedroom Soundbar",
        "IpAddress": "192.168.1.130"
      }
    ]
  }
}
```

## API Endpoints Used

The server uses the following SoundTouch WebServices API endpoints:

- `/key` - For power, volume, and preset key presses
- `/standby` - For powering off devices
- `/volume` - For getting/setting volume levels
- `/presets` - For listing presets
- `/enterBluetoothPairing` - For Bluetooth pairing mode
- `/info` - For device information

## Example Usage with Claude

**User**: "Turn on my Living Room Speaker"
**Claude**: *Uses PowerControl tool to power on the device*

**User**: "List all presets on the Living Room Speaker"
**Claude**: *Uses ListPresets tool to show all presets*

**User**: "Play the K-LOVE preset"
**Claude**: *Uses PlayPreset tool to play the preset by name*

**User**: "Set volume to 50 on the Bedroom Soundbar"
**Claude**: *Uses SetVolume tool with level 50*

**User**: "Put the Living Room Speaker in Bluetooth pairing mode"
**Claude**: *Uses EnterBluetoothPairing tool*

## Technical Details

- **Language**: C# / .NET 8.0
- **MCP SDK**: ModelContextProtocol 0.5.0-preview.1
- **Transport**: stdio (Standard Input/Output)
- **Communication Protocol**: XML over HTTP
- **Port**: 8090 (SoundTouch default)

## Error Handling

The server includes robust error handling for:
- Invalid device names (with list of available devices)
- Invalid preset numbers (must be 1-6)
- Invalid volume levels (must be 0-100)
- Network connectivity issues
- XML parsing errors
- HTTP communication failures

## Future Enhancement Ideas

Potential features that could be added:
- Now Playing status
- Play/Pause/Next/Previous track controls
- Mute/Unmute
- Zone management (multi-room)
- Select source (Bluetooth, AUX, etc.)
- Add/remove presets
- Get recently played content
