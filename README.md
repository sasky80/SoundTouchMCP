# SoundTouch MCP Server

An [MCP](https://modelcontextprotocol.io/) server for controlling Bose SoundTouch speakers via the [SoundTouch WebServices API](https://github.com/thlucas1/homeassistantcomponent_soundtouchplus/wiki/SoundTouch-WebServices-API).

## Features

- Power on / off (standby)
- Volume up, down, and set to a specific level
- List and play presets by name or number
- Bluetooth pairing mode
- Device info and listing
- Network discovery — Zeroconf (`_soundtouch._tcp.local.`) and subnet scan

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- One or more Bose SoundTouch devices on your network

## Quick Start

```bash
git clone <repository-url>
cd SoundTouchMCP
cp appsettings.example.json appsettings.json
dotnet run
```

Devices are discovered automatically at runtime — use the `DiscoverDevices` or `DiscoverDevicesOnSubnet` tool from your MCP client.

## Configuration

### `appsettings.json`

Controls logging and discovery tuning. Copy `appsettings.example.json` to get started:

```json
{
  "SoundTouch": {
    "Discovery": {
      "ProbeTimeoutMs": 3000,
      "Zeroconf": {
        "ScanTimeMs": 5000,
        "SocketRetries": 4,
        "SocketRetryDelayMs": 1000,
        "DiscoveryPasses": 2,
        "PassDelayMs": 700
      }
    }
  }
}
```

| Setting | Purpose |
|---------|---------|
| `Discovery:ProbeTimeoutMs` | HTTP probe timeout for Zeroconf and subnet discovery |
| `Discovery:Zeroconf:*` | Tune Zeroconf resilience on unstable networks |

### Device store

Devices are persisted separately from `appsettings.json`:

| OS | Path |
|----|------|
| macOS | `~/Library/Application Support/SoundTouchMCP/devices.json` |
| Windows | `%APPDATA%/SoundTouchMCP/devices.json` |
| Linux | `$XDG_CONFIG_HOME/SoundTouchMCP/devices.json` |

Override with the `SOUNDTOUCH_DEVICES_PATH` environment variable.

If the store doesn't exist yet, it bootstraps from `SoundTouch:Devices` in `appsettings.json` (one-time).

### Config file resolution

The server finds `appsettings.json` using the first match:

1. `--config /path/to/appsettings.json` (CLI argument)
2. `SOUNDTOUCH_APPSETTINGS_PATH` environment variable
3. `SOUNDTOUCH_CONFIG_DIR` environment variable → `<dir>/appsettings.json`
4. Next to the server executable
5. Server content root (fallback)

## MCP Client Setup

### VS Code (GitHub Copilot)

Add to `.vscode/mcp.json`:

```json
{
  "servers": {
    "soundtouch": {
      "command": "/absolute/path/to/SoundTouchMCP/publish/SoundTouchMCP",
      "args": [],
      "env": {
        "SOUNDTOUCH_CONFIG_DIR": "/absolute/path/to/your/config/folder"
      }
    }
  }
}
```

Or run via `dotnet` directly:

```json
{
  "servers": {
    "soundtouch": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/SoundTouchMCP/SoundTouchMCP.csproj"],
      "env": {
        "SOUNDTOUCH_CONFIG_DIR": "/absolute/path/to/your/config/folder"
      }
    }
  }
}
```

Then: **Command Palette → MCP: List Servers → start `soundtouch`**.

### Claude Desktop

Add to your Claude Desktop config (`~/Library/Application Support/Claude/claude_desktop_config.json` on macOS, `%APPDATA%\Claude\claude_desktop_config.json` on Windows):

```json
{
  "mcpServers": {
    "soundtouch": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/SoundTouchMCP"],
      "env": {}
    }
  }
}
```

## Publishing

```bash
# macOS (Apple Silicon)
dotnet publish ./SoundTouchMCP.csproj -c Release -r osx-arm64 --self-contained false -o ./publish

# Windows (x64)
dotnet publish ./SoundTouchMCP.csproj -c Release -r win-x64 --self-contained false -o ./publish-win
```

Other runtimes: `osx-x64`, `linux-x64`.

## MCP Tools

| Tool | Description | Key Parameters |
|------|-------------|----------------|
| `DiscoverDevices` | Discover devices via Zeroconf and update the store | `removeNotFound`, `forceRefresh` |
| `DiscoverDevicesOnSubnet` | Discover devices by scanning a subnet on port 8090 | `subnet` (CIDR, e.g. `192.168.1.0/24`; auto-detected if omitted), `removeNotFound`, `forceRefresh` |
| `PowerControl` | Turn a device on or off | `deviceName`, `powerOn` |
| `VolumeUp` | Increase volume by one step | `deviceName` |
| `VolumeDown` | Decrease volume by one step | `deviceName` |
| `SetVolume` | Set volume to a specific level (0–100) | `deviceName`, `level` |
| `ListPresets` | List all presets (1–6) for a device | `deviceName` |
| `PlayPreset` | Play a preset by name or number | `deviceName`, `presetIdentifier` |
| `EnterBluetoothPairing` | Enter Bluetooth pairing mode | `deviceName` |
| `GetDeviceInfo` | Get device type, ID, and IP | `deviceName` |
| `ListDevices` | List all configured devices | — |

> **Tip:** Use `DiscoverDevices(forceRefresh: true)` to fully reconcile the store with currently reachable devices (removes stale entries).
>
> Subnet scan safety: prefix length must be between `/16` and `/30`.

## License

MIT

## Contributing

Contributions welcome — feel free to open a Pull Request.
