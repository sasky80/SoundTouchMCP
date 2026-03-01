namespace SoundTouchMCP.Models;

public record Preset
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
