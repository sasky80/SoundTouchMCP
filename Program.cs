using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using SoundTouchMCP.Models;
using SoundTouchMCP.Services;

var builder = Host.CreateApplicationBuilder(args);

var appSettingsPath = ResolveAppSettingsPath(args, builder.Environment.ContentRootPath);

// Configure logging to stderr
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Add configuration
builder.Configuration.AddJsonFile(appSettingsPath, optional: false, reloadOnChange: true);

// Configure and validate SoundTouch settings
builder.Services
    .AddOptions<SoundTouchConfiguration>()
    .Bind(builder.Configuration.GetSection("SoundTouch"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<SoundTouchConfiguration>, SoundTouchConfigurationValidator>();

// Register HttpClient for SoundTouchClient
builder.Services.AddHttpClient<ISoundTouchClient, SoundTouchClient>();
builder.Services.AddHttpClient("SoundTouchDiscoveryClient", (serviceProvider, client) =>
{
    var config = serviceProvider.GetRequiredService<IOptions<SoundTouchConfiguration>>().Value;
    var timeoutMs = Math.Clamp(config.Discovery.ProbeTimeoutMs, 500, 30_000);
    client.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
})
.SetHandlerLifetime(TimeSpan.FromMinutes(5));

// Register device discovery service
builder.Services.AddSingleton<IDeviceDiscoveryService, SoundTouchMCP.Services.DeviceDiscoveryService>();
builder.Services.AddSingleton<IDeviceStoreService, DeviceStoreService>();

// Add MCP Server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

static string ResolveAppSettingsPath(string[] args, string contentRootPath)
{
    var cliPath = TryGetConfigPathFromArgs(args);
    if (!string.IsNullOrWhiteSpace(cliPath))
        return EnsureExistingPath(cliPath, "--config");

    var envAppSettingsPath = Environment.GetEnvironmentVariable("SOUNDTOUCH_APPSETTINGS_PATH");
    if (!string.IsNullOrWhiteSpace(envAppSettingsPath))
        return EnsureExistingPath(envAppSettingsPath, "SOUNDTOUCH_APPSETTINGS_PATH");

    var envConfigDir = Environment.GetEnvironmentVariable("SOUNDTOUCH_CONFIG_DIR");
    if (!string.IsNullOrWhiteSpace(envConfigDir))
    {
        var fromDir = Path.Combine(envConfigDir, "appsettings.json");
        return EnsureExistingPath(fromDir, "SOUNDTOUCH_CONFIG_DIR");
    }

    var besideExecutable = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    if (File.Exists(besideExecutable))
        return Path.GetFullPath(besideExecutable);

    var contentRootCandidate = Path.Combine(contentRootPath, "appsettings.json");
    if (File.Exists(contentRootCandidate))
        return Path.GetFullPath(contentRootCandidate);

    throw new InvalidOperationException(
        "Could not locate appsettings.json. Provide --config, set SOUNDTOUCH_APPSETTINGS_PATH, " +
        "or set SOUNDTOUCH_CONFIG_DIR.");
}

static string? TryGetConfigPathFromArgs(string[] args)
{
    for (var index = 0; index < args.Length; index++)
    {
        var arg = args[index];
        if (arg.Equals("--config", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length)
                throw new ArgumentException("Missing value for --config.");

            return args[index + 1];
        }

        if (arg.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
        {
            var value = arg[("--config=".Length)..];
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Missing value for --config.");

            return value;
        }
    }

    return null;
}

static string EnsureExistingPath(string path, string source)
{
    var resolved = Path.GetFullPath(path);
    if (!File.Exists(resolved))
        throw new FileNotFoundException($"Configuration path from {source} does not exist: {resolved}");

    return resolved;
}
