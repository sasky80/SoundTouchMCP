using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoundTouchMCP.Models;
using SoundTouchMCP.Services;
using SoundTouchMCP.Tests.TestDoubles;

namespace SoundTouchMCP.Tests.Services;

public class DeviceDiscoveryServiceTests
{
    [Fact]
    public async Task ScanSubnetAsync_ReturnsDevicesWithValidInfoResponse()
    {
        var handler = new TestHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri?.AbsolutePath == "/info")
                return TestHttpMessageHandler.XmlResponse(HttpStatusCode.OK, "<info><name>Kitchen</name></info>");

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var factory = new StubHttpClientFactory(httpClient);
        var service = CreateService(factory);

        var devices = await service.ScanSubnetAsync("192.168.1.0/30");

        Assert.Equal(2, devices.Count);
        Assert.All(devices, device =>
        {
            Assert.Equal("Kitchen", device.Name);
            Assert.Equal(DeviceConfiguration.DefaultPort, device.Port);
        });
        Assert.Contains(devices, d => d.IpAddress == "192.168.1.1");
        Assert.Contains(devices, d => d.IpAddress == "192.168.1.2");
    }

    [Fact]
    public async Task ScanSubnetAsync_ReturnsEmpty_WhenSubnetIsPublic()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            TestHttpMessageHandler.XmlResponse(HttpStatusCode.OK, "<info><name>Public</name></info>"));

        using var httpClient = new HttpClient(handler);
        var factory = new StubHttpClientFactory(httpClient);
        var service = CreateService(factory);

        var devices = await service.ScanSubnetAsync("8.8.8.0/30");

        Assert.Empty(devices);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ScanSubnetAsync_ReturnsEmpty_WhenInfoRootElementIsNotInfo()
    {
        var handler = new TestHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri?.AbsolutePath == "/info")
                return TestHttpMessageHandler.XmlResponse(HttpStatusCode.OK, "<status><name>Kitchen</name></status>");

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var factory = new StubHttpClientFactory(httpClient);
        var service = CreateService(factory);

        var devices = await service.ScanSubnetAsync("192.168.1.0/30");

        Assert.Empty(devices);
    }

    [Fact]
    public async Task ScanSubnetAsync_ReturnsEmpty_WhenInfoNameIsMissing()
    {
        var handler = new TestHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri?.AbsolutePath == "/info")
                return TestHttpMessageHandler.XmlResponse(HttpStatusCode.OK, "<info></info>");

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var factory = new StubHttpClientFactory(httpClient);
        var service = CreateService(factory);

        var devices = await service.ScanSubnetAsync("192.168.1.0/30");

        Assert.Empty(devices);
    }

    [Fact]
    public async Task ScanSubnetAsync_ReturnsEmpty_WhenProbeTimesOut()
    {
        var handler = new TestHttpMessageHandler(async (_, _, token) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), token);
            return TestHttpMessageHandler.XmlResponse(HttpStatusCode.OK, "<info><name>Slow Device</name></info>");
        });

        using var httpClient = new HttpClient(handler);
        var factory = new StubHttpClientFactory(httpClient);
        var service = CreateService(factory, probeTimeoutMs: 25);

        var devices = await service.ScanSubnetAsync("192.168.1.0/30");

        Assert.Empty(devices);
    }

    [Fact]
    public async Task ScanSubnetAsync_ReturnsEmpty_WhenProbeThrowsException()
    {
        var handler = new TestHttpMessageHandler((_, _) => throw new InvalidOperationException("simulated failure"));

        using var httpClient = new HttpClient(handler);
        var factory = new StubHttpClientFactory(httpClient);
        var service = CreateService(factory);

        var devices = await service.ScanSubnetAsync("192.168.1.0/30");

        Assert.Empty(devices);
    }

    [Fact]
    public async Task ScanSubnetAsync_Throws_WhenSubnetIsInvalid()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            TestHttpMessageHandler.XmlResponse(HttpStatusCode.OK, "<info><name>Device</name></info>"));

        using var httpClient = new HttpClient(handler);
        var factory = new StubHttpClientFactory(httpClient);
        var service = CreateService(factory);

        await Assert.ThrowsAsync<ArgumentException>(() => service.ScanSubnetAsync("192.168"));
    }

    [Fact]
    public async Task ScanSubnetAsync_ThrowsOperationCanceledException_WhenCallerCancels()
    {
        var handler = new TestHttpMessageHandler(async (_, _, token) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), token);
            return TestHttpMessageHandler.XmlResponse(HttpStatusCode.OK, "<info><name>Device</name></info>");
        });

        using var httpClient = new HttpClient(handler);
        var factory = new StubHttpClientFactory(httpClient);
        var service = CreateService(factory);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ScanSubnetAsync("192.168.1.0/30", cts.Token));
    }

    [Fact]
    public async Task DiscoverViaZeroconfAsync_ThrowsOperationCanceledException_WhenCallerCancels()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            TestHttpMessageHandler.XmlResponse(HttpStatusCode.OK, "<info><name>Device</name></info>"));

        using var httpClient = new HttpClient(handler);
        var factory = new StubHttpClientFactory(httpClient);
        var service = CreateService(factory);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.DiscoverViaZeroconfAsync(cts.Token));
    }

    [Fact]
    public async Task DiscoverViaZeroconfAsync_ReturnsWithoutThrowing_WhenNoCancellationRequested()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            TestHttpMessageHandler.XmlResponse(HttpStatusCode.OK, "<info><name>Device</name></info>"));

        using var httpClient = new HttpClient(handler);
        var factory = new StubHttpClientFactory(httpClient);
        var service = CreateService(
            factory,
            probeTimeoutMs: 1000,
            zeroconf: new ZeroconfDiscoveryConfiguration
            {
                ScanTimeMs = 500,
                SocketRetries = 1,
                SocketRetryDelayMs = 100,
                DiscoveryPasses = 1,
                PassDelayMs = 0
            });

        var devices = await service.DiscoverViaZeroconfAsync();

        Assert.NotNull(devices);
    }

    private static DeviceDiscoveryService CreateService(
        IHttpClientFactory httpClientFactory,
        int probeTimeoutMs = 1000,
        ZeroconfDiscoveryConfiguration? zeroconf = null)
    {
        var options = Options.Create(
            new SoundTouchConfiguration
            {
                Discovery = new DiscoveryConfiguration
                {
                    ProbeTimeoutMs = probeTimeoutMs,
                    Zeroconf = zeroconf ?? new ZeroconfDiscoveryConfiguration()
                }
            });

        return new DeviceDiscoveryService(
            httpClientFactory,
            NullLogger<DeviceDiscoveryService>.Instance,
            options);
    }
}
