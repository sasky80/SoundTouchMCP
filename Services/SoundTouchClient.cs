using System.Xml.Linq;

namespace SoundTouchMCP.Services;

public class SoundTouchClient
{
    private readonly HttpClient _httpClient;
    private const int DefaultPort = 8090;

    public SoundTouchClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private string GetDeviceUrl(string ipAddress, string endpoint)
    {
        return $"http://{ipAddress}:{DefaultPort}{endpoint}";
    }

    /// <summary>
    /// Powers the device on by sending a POWER key press and release
    /// </summary>
    public async Task PowerOnAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        var url = GetDeviceUrl(ipAddress, "/key");
        
        // Send POWER key press
        var pressXml = "<key state=\"press\" sender=\"Gabbo\">POWER</key>";
        await PostXmlAsync(url, pressXml, cancellationToken);
        
        await Task.Delay(100, cancellationToken); // Small delay between press and release
        
        // Send POWER key release
        var releaseXml = "<key state=\"release\" sender=\"Gabbo\">POWER</key>";
        await PostXmlAsync(url, releaseXml, cancellationToken);
    }

    /// <summary>
    /// Puts the device into standby mode (power off)
    /// </summary>
    public async Task PowerOffAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        var url = GetDeviceUrl(ipAddress, "/standby");
        await GetAsync(url, cancellationToken);
    }

    /// <summary>
    /// Increases the volume by one level
    /// </summary>
    public async Task VolumeUpAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        var url = GetDeviceUrl(ipAddress, "/key");
        
        var pressXml = "<key state=\"press\" sender=\"Gabbo\">VOLUME_UP</key>";
        await PostXmlAsync(url, pressXml, cancellationToken);
        
        await Task.Delay(100, cancellationToken);
        
        var releaseXml = "<key state=\"release\" sender=\"Gabbo\">VOLUME_UP</key>";
        await PostXmlAsync(url, releaseXml, cancellationToken);
    }

    /// <summary>
    /// Decreases the volume by one level
    /// </summary>
    public async Task VolumeDownAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        var url = GetDeviceUrl(ipAddress, "/key");
        
        var pressXml = "<key state=\"press\" sender=\"Gabbo\">VOLUME_DOWN</key>";
        await PostXmlAsync(url, pressXml, cancellationToken);
        
        await Task.Delay(100, cancellationToken);
        
        var releaseXml = "<key state=\"release\" sender=\"Gabbo\">VOLUME_DOWN</key>";
        await PostXmlAsync(url, releaseXml, cancellationToken);
    }

    /// <summary>
    /// Sets the volume to a specific level (0-100)
    /// </summary>
    public async Task SetVolumeAsync(string ipAddress, int level, CancellationToken cancellationToken = default)
    {
        if (level < 0 || level > 100)
            throw new ArgumentException("Volume level must be between 0 and 100", nameof(level));

        var url = GetDeviceUrl(ipAddress, "/volume");
        var xml = $"<volume>{level}</volume>";
        await PostXmlAsync(url, xml, cancellationToken);
    }

    /// <summary>
    /// Gets the current volume level
    /// </summary>
    public async Task<int> GetVolumeAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        var url = GetDeviceUrl(ipAddress, "/volume");
        var response = await GetAsync(url, cancellationToken);
        var doc = XDocument.Parse(response);
        var targetVolume = doc.Root?.Element("targetvolume")?.Value;
        return int.TryParse(targetVolume, out var volume) ? volume : 0;
    }

    /// <summary>
    /// Lists all configured presets
    /// </summary>
    public async Task<List<Preset>> GetPresetsAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        var url = GetDeviceUrl(ipAddress, "/presets");
        var response = await GetAsync(url, cancellationToken);
        var doc = XDocument.Parse(response);
        
        var presets = new List<Preset>();
        var presetElements = doc.Root?.Elements("preset");
        
        if (presetElements != null)
        {
            foreach (var preset in presetElements)
            {
                var id = preset.Attribute("id")?.Value;
                var contentItem = preset.Element("ContentItem");
                var itemName = contentItem?.Element("itemName")?.Value;
                
                if (id != null && itemName != null)
                {
                    presets.Add(new Preset
                    {
                        Id = int.Parse(id),
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
    public async Task PlayPresetAsync(string ipAddress, int presetNumber, CancellationToken cancellationToken = default)
    {
        if (presetNumber < 1 || presetNumber > 6)
            throw new ArgumentException("Preset number must be between 1 and 6", nameof(presetNumber));

        var url = GetDeviceUrl(ipAddress, "/key");
        var keyName = $"PRESET_{presetNumber}";
        
        var pressXml = $"<key state=\"press\" sender=\"Gabbo\">{keyName}</key>";
        await PostXmlAsync(url, pressXml, cancellationToken);
        
        await Task.Delay(100, cancellationToken);
        
        var releaseXml = $"<key state=\"release\" sender=\"Gabbo\">{keyName}</key>";
        await PostXmlAsync(url, releaseXml, cancellationToken);
    }

    /// <summary>
    /// Enters Bluetooth pairing mode
    /// </summary>
    public async Task EnterBluetoothPairingAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        var url = GetDeviceUrl(ipAddress, "/enterBluetoothPairing");
        await GetAsync(url, cancellationToken);
    }

    /// <summary>
    /// Gets device information
    /// </summary>
    public async Task<DeviceInfo> GetDeviceInfoAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        var url = GetDeviceUrl(ipAddress, "/info");
        var response = await GetAsync(url, cancellationToken);
        var doc = XDocument.Parse(response);
        
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
}

public class Preset
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class DeviceInfo
{
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
