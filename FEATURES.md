# Features

See [README.md](README.md) for setup, configuration, and MCP client integration.

## Implemented

### Device Discovery
- **Zeroconf** — discovers via `_soundtouch._tcp.local.`, uses advertised host/port
- **Subnet scan** — probes port 8090 across a CIDR range (auto-detects host subnet if omitted)
- **Config sync** — adds new devices, updates name/port for known ones, optionally removes stale entries (`forceRefresh`)

### Power Control
- **On** — POWER key press/release via `/key`
- **Off** — standby via `/standby`

### Volume Control
- **Up / Down** — adjusts by one step, reports new level
- **Set** — sets to a specific level (0–100)

### Preset Management
- **List** — shows presets 1–6 with names
- **Play** — by number (1–6) or by name (supports partial matching)

### Bluetooth
- **Pairing mode** — puts device into BT pairing via `/enterBluetoothPairing`

### Device Information
- **Info** — returns device type, ID, IP, and port
- **List** — shows all configured devices from the device store

## SoundTouch API Endpoints Used

| Endpoint | Purpose |
|----------|---------|
| `/key` | Power, volume, and preset key presses |
| `/standby` | Power off (standby) |
| `/volume` | Get / set volume |
| `/presets` | List presets |
| `/enterBluetoothPairing` | Bluetooth pairing mode |
| `/info` | Device information |

## Error Handling

- Invalid device name → lists available devices
- Invalid preset number (must be 1–6)
- Invalid volume level (must be 0–100)
- Network / HTTP failures
- XML parsing errors

## Future Ideas

- Now Playing status
- Play / Pause / Next / Previous
- Mute / Unmute
- Multi-room zone management
- Source selection (Bluetooth, AUX, etc.)
- Preset add / remove
- Recently played content
