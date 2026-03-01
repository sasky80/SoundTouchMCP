using System.Net;
using SoundTouchMCP.Services;
using SoundTouchMCP.Tests.TestDoubles;

namespace SoundTouchMCP.Tests.Services;

public class SoundTouchClientTests
{
    [Fact]
    public async Task PowerOnAsync_SendsPressAndReleasePowerKey()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            TestHttpMessageHandler.XmlResponse(HttpStatusCode.OK, "<ok />"));
        using var httpClient = new HttpClient(handler);
        var client = new SoundTouchClient(httpClient);

        await client.PowerOnAsync("192.168.1.10");

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal("POST", request.Method);
            Assert.Contains("/key", request.Url, StringComparison.Ordinal);
        });

        Assert.Contains("state=\"press\"", handler.Requests[0].Body);
        Assert.Contains(">POWER<", handler.Requests[0].Body);
        Assert.Contains("state=\"release\"", handler.Requests[1].Body);
    }

    [Fact]
    public async Task GetVolumeAsync_ParsesTargetVolume()
    {
        var handler = new TestHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri?.AbsolutePath == "/volume")
                return TestHttpMessageHandler.XmlResponse(HttpStatusCode.OK, "<volume><targetvolume>34</targetvolume></volume>");

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var client = new SoundTouchClient(httpClient);

        var volume = await client.GetVolumeAsync("192.168.1.15");

        Assert.Equal(34, volume);
    }

    [Fact]
    public async Task GetPresetsAsync_ReturnsParsedPresets()
    {
        const string xml = """
            <presets>
              <preset id="1">
                <ContentItem><itemName>Jazz</itemName></ContentItem>
              </preset>
              <preset id="2">
                <ContentItem><itemName>News</itemName></ContentItem>
              </preset>
            </presets>
            """;

        var handler = new TestHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri?.AbsolutePath == "/presets")
                return TestHttpMessageHandler.XmlResponse(HttpStatusCode.OK, xml);

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var client = new SoundTouchClient(httpClient);

        var presets = await client.GetPresetsAsync("192.168.1.20");

        Assert.Equal(2, presets.Count);
        Assert.Equal(1, presets[0].Id);
        Assert.Equal("Jazz", presets[0].Name);
        Assert.Equal(2, presets[1].Id);
        Assert.Equal("News", presets[1].Name);
    }

    [Fact]
    public async Task GetDeviceInfoAsync_ReturnsParsedInfo()
    {
        const string xml = """
            <info deviceID="abc123">
              <name>Living Room</name>
              <type>SoundTouch 20</type>
            </info>
            """;

        var handler = new TestHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri?.AbsolutePath == "/info")
                return TestHttpMessageHandler.XmlResponse(HttpStatusCode.OK, xml);

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var client = new SoundTouchClient(httpClient);

        var info = await client.GetDeviceInfoAsync("192.168.1.21");

        Assert.Equal("abc123", info.DeviceId);
        Assert.Equal("Living Room", info.Name);
        Assert.Equal("SoundTouch 20", info.Type);
    }

    [Fact]
    public async Task GetVolumeAsync_ThrowsForPublicIpAddress()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            TestHttpMessageHandler.XmlResponse(HttpStatusCode.OK, "<volume><targetvolume>10</targetvolume></volume>"));

        using var httpClient = new HttpClient(handler);
        var client = new SoundTouchClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetVolumeAsync("8.8.8.8"));
        Assert.Empty(handler.Requests);
    }
}
