using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoundTouchMCP.Models;

namespace SoundTouchMCP.Services;

public class DeviceStoreService : IDeviceStoreService, IDisposable
{
    private const string DevicesPathEnvVar = "SOUNDTOUCH_DEVICES_PATH";
    private const int AtomicReplaceMaxAttempts = 3;
    private const int AtomicReplaceRetryDelayMs = 50;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly string _devicesFilePath;
    private readonly List<DeviceConfiguration> _devices = [];
    private readonly List<DeviceConfiguration> _fallbackDevices;
    private bool _isInitialized;
    private bool _disposed;
    private readonly ILogger<DeviceStoreService> _logger;

    public DeviceStoreService(IOptions<SoundTouchConfiguration> config, ILogger<DeviceStoreService> logger)
    {
        _logger = logger;
        _devicesFilePath = ResolveDevicesFilePath();
        _fallbackDevices = DeduplicateByIp(config.Value.Devices);
    }

    public string DevicesFilePath => _devicesFilePath;

    public async Task<IReadOnlyList<DeviceConfiguration>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _sync.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            return _devices.ToList();
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task ApplyChangesAsync(
        IEnumerable<DeviceConfiguration> added,
        IEnumerable<DeviceConfiguration> updated,
        IEnumerable<DeviceConfiguration> removed,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _sync.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);

            var existingByIp = _devices
                .Where(d => !string.IsNullOrWhiteSpace(d.IpAddress))
                .GroupBy(d => d.IpAddress, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var device in added)
            {
                if (string.IsNullOrWhiteSpace(device.IpAddress) || existingByIp.ContainsKey(device.IpAddress))
                    continue;

                _devices.Add(device);
                existingByIp[device.IpAddress] = device;
            }

            foreach (var device in updated)
            {
                if (string.IsNullOrWhiteSpace(device.IpAddress))
                    continue;

                if (existingByIp.TryGetValue(device.IpAddress, out var existing))
                {
                    var updatedDevice = existing with
                    {
                        Name = device.Name,
                        Port = device.Port
                    };

                    var index = _devices.IndexOf(existing);
                    if (index >= 0)
                        _devices[index] = updatedDevice;

                    existingByIp[device.IpAddress] = updatedDevice;
                }
            }

            var toRemove = new HashSet<string>(
                removed
                    .Where(d => !string.IsNullOrWhiteSpace(d.IpAddress))
                    .Select(d => d.IpAddress),
                StringComparer.OrdinalIgnoreCase);

            if (toRemove.Count > 0)
                _devices.RemoveAll(d => toRemove.Contains(d.IpAddress));

            await PersistDevicesAsync(_devices, cancellationToken);
        }
        finally
        {
            _sync.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _sync.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DeviceStoreService));
    }

    private async Task EnsureInitializedLockedAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized)
            return;

        List<DeviceConfiguration> initial;
        if (File.Exists(_devicesFilePath))
        {
            var existing = await ReadDevicesFromDiskAsync(_devicesFilePath, cancellationToken);
            initial = DeduplicateByIp(existing);
        }
        else
        {
            initial = DeduplicateByIp(_fallbackDevices);
            if (initial.Count > 0)
                await PersistDevicesAsync(initial, cancellationToken);
        }

        _devices.Clear();
        _devices.AddRange(initial);
        _isInitialized = true;
    }

    private static string ResolveDevicesFilePath()
    {
        var safeBaseDirectory = GetSafeBaseDirectory();
        var envPath = Environment.GetEnvironmentVariable(DevicesPathEnvVar);
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            var resolved = Path.GetFullPath(envPath);
            if (!IsPathUnderBaseDirectory(resolved, safeBaseDirectory))
            {
                throw new InvalidOperationException(
                    $"Environment variable {DevicesPathEnvVar} must point inside '{safeBaseDirectory}'.");
            }

            return resolved;
        }

        return Path.Combine(safeBaseDirectory, "SoundTouchMCP", "devices.json");
    }

    private static string GetSafeBaseDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
            appData = AppContext.BaseDirectory;

        return Path.GetFullPath(appData);
    }

    private static bool IsPathUnderBaseDirectory(string candidatePath, string baseDirectory)
    {
        var normalizedBase = Path.GetFullPath(baseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedCandidate = Path.GetFullPath(candidatePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return normalizedCandidate.Equals(normalizedBase, StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(
                normalizedBase + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<DeviceConfiguration>> ReadDevicesFromDiskAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var payload = JsonSerializer.Deserialize<DeviceStorePayload>(json);
            if (payload?.Devices != null)
                return payload.Devices;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse device store payload format at {Path}, trying legacy format.", path);
        }

        try
        {
            return JsonSerializer.Deserialize<List<DeviceConfiguration>>(json) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse device store file at {Path}. Using empty device list.", path);
            return [];
        }
    }

    private async Task PersistDevicesAsync(List<DeviceConfiguration> devices, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_devicesFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var payload = new DeviceStorePayload { Devices = devices };
        var serializedPayload = JsonSerializer.Serialize(payload, JsonOptions);
        var targetDirectory = string.IsNullOrWhiteSpace(directory)
            ? AppContext.BaseDirectory
            : directory;
        var tempFilePath = Path.Combine(
            targetDirectory,
            $".{Path.GetFileName(_devicesFilePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(tempFilePath, serializedPayload, cancellationToken);
            await MoveAtomicallyWithRetryAsync(tempFilePath, _devicesFilePath, cancellationToken);
        }
        finally
        {
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);
        }
    }

    private async Task MoveAtomicallyWithRetryAsync(
        string tempFilePath,
        string destinationFilePath,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= AtomicReplaceMaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                File.Move(tempFilePath, destinationFilePath, overwrite: true);
                return;
            }
            catch (Exception ex) when (
                attempt < AtomicReplaceMaxAttempts &&
                (ex is IOException || ex is UnauthorizedAccessException))
            {
                _logger.LogDebug(
                    ex,
                    "Atomic replace failed on attempt {Attempt}/{MaxAttempts} for {Path}. Retrying.",
                    attempt,
                    AtomicReplaceMaxAttempts,
                    destinationFilePath);

                await Task.Delay(AtomicReplaceRetryDelayMs * attempt, cancellationToken);
            }
        }
    }

    private static List<DeviceConfiguration> DeduplicateByIp(IEnumerable<DeviceConfiguration> devices)
    {
        return devices
            .Where(d => !string.IsNullOrWhiteSpace(d.IpAddress))
            .GroupBy(d => d.IpAddress, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private class DeviceStorePayload
    {
        public List<DeviceConfiguration> Devices { get; set; } = [];
    }
}