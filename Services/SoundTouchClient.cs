using System.Net;
using System.Net.Sockets;
using SoundTouchMCP.Models;

namespace SoundTouchMCP.Services;

public class SoundTouchClient : ISoundTouchClient
{
    private readonly HttpClient _httpClient;
    private const int DefaultPort = DeviceConfiguration.DefaultPort;
    private const string EndpointStandby = "/standby";
    private const string EndpointVolume = "/volume";
    private const string EndpointPresets = "/presets";
    private const string EndpointBluetoothPairing = "/enterBluetoothPairing";
    private const string EndpointInfo = "/info";
    private const string EndpointKey = "/key";
    private const string KeyPower = "POWER";
    private const string KeyVolumeUp = "VOLUME_UP";
    private const string KeyVolumeDown = "VOLUME_DOWN";
    private const string KeyPresetPrefix = "PRESET_";
    private const string KeySender = "Gabbo";

    public SoundTouchClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private string GetDeviceUrl(string ipAddress, string endpoint, int port = DefaultPort)
    {
        var parsedAddress = ParseIpAddress(ipAddress);
        var validatedPort = ValidatePort(port);
        var host = parsedAddress.AddressFamily == AddressFamily.InterNetworkV6
            ? $"[{parsedAddress}]"
            : parsedAddress.ToString();

        return $"http://{host}:{validatedPort}{endpoint}";
    }

    private static IPAddress ParseIpAddress(string ipAddress)
    {
        if (!NetworkAddressGuard.TryParseAllowedDeviceAddress(ipAddress, out var parsed))
            throw new ArgumentException(
                "Device address must be in a private LAN range (10.x.x.x, 172.16-31.x.x, 192.168.x.x) or link-local (169.254.x.x).",
                nameof(ipAddress));

        return parsed!;
    }

    private static int ValidatePort(int port)
    {
        if (port < 1 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

        return port;
    }

    /// <summary>
    /// Powers the device on by sending a POWER key press and release
    /// </summary>
    public async Task PowerOnAsync(string ipAddress, int port = DefaultPort, CancellationToken cancellationToken = default)
    {
        await SendKeyAsync(ipAddress, KeyPower, port, cancellationToken);
    }

    /// <summary>
    /// Puts the device into standby mode (power off)
    /// </summary>
    public async Task PowerOffAsync(string ipAddress, int port = DefaultPort, CancellationToken cancellationToken = default)
    {
        var url = GetDeviceUrl(ipAddress, EndpointStandby, port);
        await GetAsync(url, cancellationToken);
    }

    /// <summary>
    /// Increases the volume by one level
    /// </summary>
    public async Task VolumeUpAsync(string ipAddress, int port = DefaultPort, CancellationToken cancellationToken = default)
    {
        await SendKeyAsync(ipAddress, KeyVolumeUp, port, cancellationToken);
    }

    /// <summary>
    /// Decreases the volume by one level
    /// </summary>
    public async Task VolumeDownAsync(string ipAddress, int port = DefaultPort, CancellationToken cancellationToken = default)
    {
        await SendKeyAsync(ipAddress, KeyVolumeDown, port, cancellationToken);
    }

    /// <summary>
    /// Sets the volume to a specific level (0-100)
    /// </summary>
    public async Task SetVolumeAsync(string ipAddress, int level, int port = DefaultPort, CancellationToken cancellationToken = default)
    {
        if (level < 0 || level > 100)
            throw new ArgumentException("Volume level must be between 0 and 100", nameof(level));

        var url = GetDeviceUrl(ipAddress, EndpointVolume, port);
        var xml = $"<volume>{level}</volume>";
        await PostXmlAsync(url, xml, cancellationToken);
    }

    /// <summary>
    /// Gets the current volume level
    /// </summary>
    public async Task<int> GetVolumeAsync(string ipAddress, int port = DefaultPort, CancellationToken cancellationToken = default)
    {
        var url = GetDeviceUrl(ipAddress, EndpointVolume, port);
        var response = await GetAsync(url, cancellationToken);
        var doc = SecureXmlParser.Parse(response);
        var targetVolume = doc.Root?.Element("targetvolume")?.Value;
        return int.TryParse(targetVolume, out var volume) ? volume : 0;
    }

    /// <summary>
    /// Lists all configured presets
    /// </summary>
    public async Task<List<Preset>> GetPresetsAsync(string ipAddress, int port = DefaultPort, CancellationToken cancellationToken = default)
    {
        var url = GetDeviceUrl(ipAddress, EndpointPresets, port);
        var response = await GetAsync(url, cancellationToken);
        var doc = SecureXmlParser.Parse(response);
        
        var presets = new List<Preset>();
        var presetElements = doc.Root?.Elements("preset");
        
        if (presetElements != null)
        {
            foreach (var preset in presetElements)
            {
                var id = preset.Attribute("id")?.Value;
                var contentItem = preset.Element("ContentItem");
                var itemName = contentItem?.Element("itemName")?.Value;
                
                if (id != null && itemName != null && int.TryParse(id, out var parsedId))
                {
                    presets.Add(new Preset
                    {
                        Id = parsedId,
                        Name = itemName
                    });
                }
            }
        }
        
        return presets;
    }

    /// <summary>
    /// Plays a preset by number (1-6)
    /// </summary>
    public async Task PlayPresetAsync(string ipAddress, int presetNumber, int port = DefaultPort, CancellationToken cancellationToken = default)
    {
        if (presetNumber < 1 || presetNumber > 6)
            throw new ArgumentException("Preset number must be between 1 and 6", nameof(presetNumber));

        var keyName = $"{KeyPresetPrefix}{presetNumber}";

        await SendKeyAsync(ipAddress, keyName, port, cancellationToken);
    }

    /// <summary>
    /// Enters Bluetooth pairing mode
    /// </summary>
    public async Task EnterBluetoothPairingAsync(string ipAddress, int port = DefaultPort, CancellationToken cancellationToken = default)
    {
        var url = GetDeviceUrl(ipAddress, EndpointBluetoothPairing, port);
        await GetAsync(url, cancellationToken);
    }

    /// <summary>
    /// Gets device information
    /// </summary>
    public async Task<DeviceInfo> GetDeviceInfoAsync(string ipAddress, int port = DefaultPort, CancellationToken cancellationToken = default)
    {
        var url = GetDeviceUrl(ipAddress, EndpointInfo, port);
        var response = await GetAsync(url, cancellationToken);
        var doc = SecureXmlParser.Parse(response);
        
        var deviceId = doc.Root?.Attribute("deviceID")?.Value ?? "Unknown";
        var name = doc.Root?.Element("name")?.Value ?? "Unknown";
        var type = doc.Root?.Element("type")?.Value ?? "Unknown";
        
        return new DeviceInfo
        {
            DeviceId = deviceId,
            Name = name,
            Type = type
        };
    }

    private async Task<string> GetAsync(string url, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private async Task PostXmlAsync(string url, string xmlContent, CancellationToken cancellationToken)
    {
        var content = new StringContent(xmlContent, System.Text.Encoding.UTF8, "text/xml");
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task SendKeyAsync(string ipAddress, string keyName, int port, CancellationToken cancellationToken)
    {
        var url = GetDeviceUrl(ipAddress, EndpointKey, port);
        await PostXmlAsync(url, $"<key state=\"press\" sender=\"{KeySender}\">{keyName}</key>", cancellationToken);
        await Task.Delay(100, cancellationToken);
        await PostXmlAsync(url, $"<key state=\"release\" sender=\"{KeySender}\">{keyName}</key>", cancellationToken);
    }
}
