# SoundTouch MCP Server

A Model Context Protocol (MCP) server for controlling Bose SoundTouch devices via the SoundTouch WebServices API.

## Features

- **Power Control**: Turn SoundTouch devices on/off
- **Volume Control**: Adjust volume up and down
- **Preset Management**: List all configured presets and play them by name or number
- **Bluetooth Pairing**: Enter Bluetooth pairing mode

## Prerequisites

- .NET 8.0 SDK
- One or more Bose SoundTouch devices on your network
- Static IP addresses configured for your SoundTouch devices (recommended)

## Configuration

Edit `appsettings.json` to configure your SoundTouch devices:

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

## Installation

1. Clone this repository:
   ```bash
   git clone <repository-url>
   cd SoundTouchMCP
   ```

2. Configure your devices in `appsettings.json`:
   ```bash
   cp appsettings.example.json appsettings.json
   # Edit appsettings.json with your device information
   ```

3. Build the project:
   ```bash
   dotnet build
   ```

4. Run the server:
   ```bash
   dotnet run
   ```

## Using with MCP Clients

### Claude Desktop Configuration

Add this to your Claude Desktop configuration file:

**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
**macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "soundtouch": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/SoundTouchMCP"],
      "env": {}
    }
  }
}
```

Or use the published executable:

```json
{
  "mcpServers": {
    "soundtouch": {
      "command": "path/to/SoundTouchMCP.exe",
      "args": [],
      "env": {}
    }
  }
}
```

## Publishing

To create a standalone executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

For other platforms:
- macOS: `-r osx-x64`
- Linux: `-r linux-x64`

## MCP Tools

### PowerControl
Turn a device on or off.

**Parameters:**
- `deviceName` (string): Name of the device as configured in appsettings.json
- `powerOn` (boolean): true to turn on, false to turn off (standby)

### VolumeUp
Increase the volume of a device.

**Parameters:**
- `deviceName` (string): Name of the device

### VolumeDown
Decrease the volume of a device.

**Parameters:**
- `deviceName` (string): Name of the device

### SetVolume
Set the volume to a specific level.

**Parameters:**
- `deviceName` (string): Name of the device
- `level` (number): Volume level (0-100)

### ListPresets
List all configured presets for a device.

**Parameters:**
- `deviceName` (string): Name of the device

### PlayPreset
Play a preset by name or number.

**Parameters:**
- `deviceName` (string): Name of the device
- `presetIdentifier` (string): Preset name or number (1-6)

### EnterBluetoothPairing
Enter Bluetooth pairing mode.

**Parameters:**
- `deviceName` (string): Name of the device

## API Reference

This server uses the [SoundTouch WebServices API](https://github.com/thlucas1/homeassistantcomponent_soundtouchplus/wiki/SoundTouch-WebServices-API).

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
