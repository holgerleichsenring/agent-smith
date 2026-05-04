using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Logging;
using AgentSmith.Server.Extensions;
using Microsoft.Extensions.Logging.Console;

DispatcherBanner.Print();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole(options => options.FormatterName = CompactConsoleFormatter.FormatterName);
builder.Logging.AddConsoleFormatter<CompactConsoleFormatter, ConsoleFormatterOptions>(
    options => options.IncludeScopes = true);
builder.Logging.AddFilter("Microsoft", LogLevel.Information);
builder.Logging.AddFilter("System", LogLevel.Information);
builder.Logging.SetMinimumLevel(
    builder.Environment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information);

var configPath = Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "/app/config/agentsmith.yml";
if (!File.Exists(configPath))
{
    Console.Error.WriteLine(
        $"FATAL: agentsmith config not found at '{configPath}'.\n" +
        $"  • K8s: mount your ConfigMap as a volume at /app/config (see deploy/k8s/8-deployment-server.yaml).\n" +
        $"  • Docker: -v /path/to/agentsmith.yml:/app/config/agentsmith.yml\n" +
        $"  • Custom path: set CONFIG_PATH=/path/to/agentsmith.yml.");
    Environment.Exit(78);   // EX_CONFIG: configuration error
}
builder.Services.AddSingleton(new ServerContext(configPath));
builder.Services.AddSingleton<IProgressReporter>(sp =>
    new ConsoleProgressReporter(
        sp.GetRequiredService<ILogger<ConsoleProgressReporter>>(), headless: true));

builder.Services
    .AddRedis()
    .AddCoreDispatcherServices()
    .AddServerCompositionOverrides()
    .AddSlackAdapter()
    .AddTeamsAdapter()
    .AddIntentHandlers()
    .AddWebhookHandlers()
    .AddLongRunningServices();

await builder.Services.AddJobSpawnerAsync(
    LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Startup"));

var app = builder.Build();

app.MapHealthEndpoints()
   .MapSlackEndpoints()
   .MapTeamsEndpoints()
   .MapWebhookEndpoints();

var startupConfig = app.Services.GetRequiredService<IConfigurationLoader>()
    .LoadConfig(configPath);
StartupSummaryLogger.Log(
    startupConfig,
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AgentSmith.Server.Startup"));

app.Run();

public partial class Program;
