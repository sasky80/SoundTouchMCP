# SoundTouch MCP Server

A Model Context Protocol (MCP) server for controlling Bose SoundTouch devices via the SoundTouch WebServices API.

## Features

- **Power Control**: Turn SoundTouch devices on/off
- **Volume Control**: Adjust volume up and down
- **Preset Management**: List all configured presets and play them by name or number
- **Bluetooth Pairing**: Enter Bluetooth pairing mode
- **Device Information**: Get device details and list configured devices
- **Network Discovery**: Scan subnet for SoundTouch devices and update `appsettings.json`

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

### GitHub Copilot (VS Code)

Create `.vscode/mcp.json` in your workspace:

```json
{
  "servers": {
    "soundtouch": {
      "command": "/absolute/path/to/SoundTouchMCP/publish/SoundTouchMCP",
      "args": [],
      "env": {}
    }
  }
}
```

Then in VS Code:

1. Run `MCP: List Servers` from the Command Palette.
2. Start the `soundtouch` server and approve trust when prompted.
3. Open Copilot Chat and use prompts that invoke SoundTouch tools.

If you prefer running directly via `dotnet` instead of a published binary:

```json
{
  "servers": {
    "soundtouch": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/SoundTouchMCP/SoundTouchMCP.csproj"],
      "env": {}
    }
  }
}
```

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

To publish for macOS (Apple Silicon):

```bash
dotnet publish ./SoundTouchMCP.csproj -c Release -r osx-arm64 --self-contained false -o ./publish
```

To publish for Windows (x64):

```bash
dotnet publish ./SoundTouchMCP.csproj -c Release -r win-x64 --self-contained false -o ./publish-win
```

For other platforms:
- macOS (Intel): `-r osx-x64`
- Linux: `-r linux-x64`

## MCP Tools

### DiscoverDevices
Discover SoundTouch devices on a local network subnet and update `appsettings.json`.

**Usage (short):**
- Discover devices on auto-detected subnet: `DiscoverDevices(subnet: "", removeNotFound: false)`
- Discover devices on a specific subnet: `DiscoverDevices(subnet: "192.168.1.0/24", removeNotFound: false)`

**Parameters:**
- `subnet` (string): Subnet in CIDR notation (for example `192.168.1.0/24`) or short form (for example `192.168.1`). If omitted, host subnet is auto-detected.
- `removeNotFound` (boolean): If true, remove configured devices not found during discovery.

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

### GetDeviceInfo
Get information about a SoundTouch device.

**Parameters:**
- `deviceName` (string): Name of the device

### ListDevices
List all configured SoundTouch devices.

## API Reference

This server uses the [SoundTouch WebServices API](https://github.com/thlucas1/homeassistantcomponent_soundtouchplus/wiki/SoundTouch-WebServices-API).

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
