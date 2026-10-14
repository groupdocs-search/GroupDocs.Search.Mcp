using System.Reflection;
using GroupDocs.Mcp.Core.Licensing;
using GroupDocs.Search.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var version = typeof(Program).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion
    ?.Split('+')[0]
    ?? "0.0.0";

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services
    .AddGroupDocsMcp()
    .AddLocalStorage(SearchConfig.DefaultStorageFolder);
builder.Services.AddSingleton<ILicenseManager, SearchLicenseManager>();
// Search is stateful: indexes live under their own root (GROUPDOCS_MCP_INDEX_PATH), resolved
// alongside the storage folder so the tools read exactly the documents the storage layer serves.
builder.Services.AddSingleton(
    SearchConfig.FromEnvironment(Environment.GetEnvironmentVariable, Directory.GetCurrentDirectory()));
builder.Services
    .AddMcpServer(options => { options.ServerInfo = new() { Name = "GroupDocs.Search.Mcp", Version = version }; })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
await builder.Build().RunAsync();
