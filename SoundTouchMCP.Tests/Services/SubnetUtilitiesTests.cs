using SoundTouchMCP.Services;

namespace SoundTouchMCP.Tests.Services;

public class SubnetUtilitiesTests
{
    [Fact]
    public void ParseSubnet_UsesShortForm24_WhenThreeOctetsProvided()
    {
        var (baseAddress, prefixLength) = SubnetUtilities.ParseSubnet("192.168.10");

        Assert.Equal("192.168.10.0", baseAddress);
        Assert.Equal(24, prefixLength);
    }

    [Fact]
    public void ParseSubnet_ThrowsForTooBroadSubnet()
    {
        var ex = Assert.Throws<ArgumentException>(() => SubnetUtilities.ParseSubnet("192.168.0.0/16"));

        Assert.Contains("too broad", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnumerateHosts_ReturnsAllUsableHosts_ForSlash30()
    {
        var hosts = SubnetUtilities.EnumerateHosts("192.168.1.0", 30).ToList();

        Assert.Equal(["192.168.1.1", "192.168.1.2"], hosts);
    }
}
