namespace SoundTouchMCP.Models;

public record DeviceInfo
{
    public string DeviceId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
}
