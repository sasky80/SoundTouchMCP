using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SoundTouchMCP.Models;
using SoundTouchMCP.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to stderr
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Add configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Configure SoundTouch settings
builder.Services.Configure<SoundTouchConfiguration>(
    builder.Configuration.GetSection("SoundTouch"));

// Register HttpClient for SoundTouchClient
builder.Services.AddHttpClient<SoundTouchClient>();

// Add MCP Server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
