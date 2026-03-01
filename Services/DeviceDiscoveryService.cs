using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoundTouchMCP.Models;
using Zeroconf;

namespace SoundTouchMCP.Services;

public class DeviceDiscoveryService : IDeviceDiscoveryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DeviceDiscoveryService> _logger;
    private readonly ZeroconfDiscoveryConfiguration _zeroconf;
    private readonly TimeSpan _probeTimeout;
    private const int SoundTouchPort = DeviceConfiguration.DefaultPort;
    private const string SoundTouchService = "_soundtouch._tcp.local.";
    private const int MaxConcurrentSubnetProbes = 32;

    public DeviceDiscoveryService(
        IHttpClientFactory httpClientFactory,
        ILogger<DeviceDiscoveryService> logger,
        IOptions<SoundTouchConfiguration> config)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _zeroconf = config.Value.Discovery.Zeroconf;
        _probeTimeout = TimeSpan.FromMilliseconds(config.Value.Discovery.ProbeTimeoutMs);
    }

    /// <summary>
    /// Discovers SoundTouch devices using Zeroconf (_soundtouch._tcp.local.) and probes discovered host/port.
    /// </summary>
    public async Task<List<DeviceConfiguration>> DiscoverViaZeroconfAsync(
        CancellationToken cancellationToken = default)
    {
        var hosts = await ResolveZeroconfHostsAsync(cancellationToken);
        if (hosts.Count == 0)
            return [];

        var candidates = hosts
            .SelectMany(host =>
            {
                var service = host.Services
                    .FirstOrDefault(kvp =>
                        string.Equals(kvp.Key, SoundTouchService, StringComparison.OrdinalIgnoreCase))
                    .Value
                    ?? host.Services.Values.FirstOrDefault();

                if (service == null || string.IsNullOrWhiteSpace(host.IPAddress))
                    return [];

                return new[]
                {
                    new
                    {
                        IpAddress = host.IPAddress,
                        Port = service.Port,
                        NameHint = string.IsNullOrWhiteSpace(host.DisplayName) ? host.Id : host.DisplayName
                    }
                };
            })
            .GroupBy(x => x.IpAddress, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var tasks = candidates.Select(c =>
            ProbeHostAsync(c.IpAddress, c.Port, c.NameHint, cancellationToken));
        var results = await Task.WhenAll(tasks);

        return results.Where(d => d != null).Cast<DeviceConfiguration>().ToList();
    }

    private async Task<IReadOnlyList<IZeroconfHost>> ResolveZeroconfHostsAsync(
        CancellationToken cancellationToken)
    {
        var scanTime = TimeSpan.FromMilliseconds(Math.Max(500, _zeroconf.ScanTimeMs));
        var socketRetries = Math.Max(1, _zeroconf.SocketRetries);
        var socketRetryDelayMs = Math.Max(100, _zeroconf.SocketRetryDelayMs);
        var discoveryPasses = Math.Max(1, _zeroconf.DiscoveryPasses);
        var passDelay = TimeSpan.FromMilliseconds(Math.Max(0, _zeroconf.PassDelayMs));

        for (int attempt = 1; attempt <= discoveryPasses; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var hosts = await ZeroconfResolver.ResolveAsync(
                    SoundTouchService,
                    scanTime: scanTime,
                    retries: socketRetries,
                    retryDelayMilliseconds: socketRetryDelayMs,
                    cancellationToken: cancellationToken);

                if (hosts.Count > 0)
                    return hosts;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Zeroconf discovery attempt {Attempt}/{TotalAttempts} failed.", attempt, discoveryPasses);
            }

            if (attempt < discoveryPasses && passDelay > TimeSpan.Zero)
                await Task.Delay(passDelay, cancellationToken);
        }

        return [];
    }

    /// <summary>
    /// Scans the given subnet (CIDR notation, e.g. "192.168.1.0/24") for SoundTouch devices.
    /// If subnet is null or empty, the host's primary subnet is used.
    /// </summary>
    public async Task<List<DeviceConfiguration>> ScanSubnetAsync(
        string? subnet,
        CancellationToken cancellationToken = default)
    {
        var (baseAddress, prefixLength) = SubnetUtilities.ParseSubnet(subnet);
        var ips = SubnetUtilities.EnumerateHosts(baseAddress, prefixLength);
        var discovered = new List<DeviceConfiguration>();
        var discoveredLock = new object();

        await Parallel.ForEachAsync(
            ips,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = MaxConcurrentSubnetProbes
            },
            async (ip, ct) =>
            {
                var result = await ProbeHostAsync(ip, SoundTouchPort, null, ct);
                if (result is null)
                    return;

                lock (discoveredLock)
                {
                    discovered.Add(result);
                }
            });

        return discovered;
    }

    /// <summary>
    /// Returns the auto-detected subnet string (e.g. "192.168.1.0/24") from the host's primary interface.
    /// </summary>
    public string GetHostSubnet()
    {
        var (baseAddress, prefixLength) = SubnetUtilities.DetectHostSubnet();
        return $"{baseAddress}/{prefixLength}";
    }

    private async Task<DeviceConfiguration?> ProbeHostAsync(
        string ip,
        int port,
        string? nameHint,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!IsAllowedProbeTarget(ip, port))
                return null;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_probeTimeout);

            using var client = _httpClientFactory.CreateClient("SoundTouchDiscoveryClient");

            var url = $"http://{ip}:{port}/info";
            var response = await client.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            var xml = await response.Content.ReadAsStringAsync(cts.Token);
            var doc = SecureXmlParser.Parse(xml);

            // Verify it looks like a SoundTouch /info response
            if (doc.Root?.Name.LocalName != "info")
                return null;

            var name = doc.Root.Element("name")?.Value;
            if (string.IsNullOrWhiteSpace(name))
                name = nameHint;

            if (string.IsNullOrWhiteSpace(name))
                return null;

            return new DeviceConfiguration { Name = name, IpAddress = ip, Port = port };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Probe failed for host {Ip}:{Port}.", ip, port);
            return null;
        }
    }


    private static bool IsAllowedProbeTarget(string ip, int port)
    {
        if (port < 1 || port > 65535)
            return false;

        if (!IPAddress.TryParse(ip, out var address))
            return false;

        return NetworkAddressGuard.IsAllowedPrivateOrLinkLocalIPv4(address);
    }

}
