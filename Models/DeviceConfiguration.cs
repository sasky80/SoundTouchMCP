namespace SoundTouchMCP.Models;

public class SoundTouchConfiguration
{
    public List<DeviceConfiguration> Devices { get; set; } = new();
}

public class DeviceConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}
