using SoundTouchMCP.Services;

namespace SoundTouchMCP.Tests.Services;

public class NetworkAddressGuardTests
{
    [Fact]
    public void TryParseAllowedDeviceAddress_ReturnsTrue_ForPrivateLanAddress()
    {
        var result = NetworkAddressGuard.TryParseAllowedDeviceAddress("192.168.1.25", out var parsedAddress);

        Assert.True(result);
        Assert.NotNull(parsedAddress);
        Assert.Equal("192.168.1.25", parsedAddress!.ToString());
    }

    [Fact]
    public void TryParseAllowedDeviceAddress_ReturnsFalse_ForPublicAddress()
    {
        var result = NetworkAddressGuard.TryParseAllowedDeviceAddress("8.8.8.8", out var parsedAddress);

        Assert.False(result);
        Assert.Null(parsedAddress);
    }

    [Fact]
    public void TryParseAllowedDeviceAddress_ReturnsFalse_ForIpv6Address()
    {
        var result = NetworkAddressGuard.TryParseAllowedDeviceAddress("fe80::1", out var parsedAddress);

        Assert.False(result);
        Assert.Null(parsedAddress);
    }
}
