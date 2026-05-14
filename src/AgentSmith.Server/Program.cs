using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Logging;
using AgentSmith.Server.Extensions;
using Microsoft.Extensions.Logging.Console;

DispatcherBanner.Print();

var builder = WebApplication.CreateBuilder(args);

// p0137d: log filters + minimum level moved to appsettings.json + appsettings.Development.json.
// Console formatter wiring stays here — it's code-shape config (selecting CompactConsoleFormatter
// over the default one), not log-level filtering.
builder.Logging.AddConsole(options => options.FormatterName = CompactConsoleFormatter.FormatterName);
builder.Logging.AddConsoleFormatter<CompactConsoleFormatter, ConsoleFormatterOptions>(
    options => options.IncludeScopes = true);

// p0137b: single composition-root AddHttpClient() — registers IHttpClientFactory + named-options
// infrastructure. Per-feature extensions (AddTeamsAdapter, AddSlackAdapter, AddAgentSmithInfrastructure)
// add their typed clients via AddHttpClient<T>() on top of this.
builder.Services.AddHttpClient();

// p0137b: scope validation always on; build-time validation in Development catches lifetime
// violations (e.g. Singleton consuming Scoped) at startup instead of as confusing runtime errors.
builder.Host.UseDefaultServiceProvider(o =>
{
    o.ValidateScopes = true;
    o.ValidateOnBuild = builder.Environment.IsDevelopment();
});

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
    .AddSandbox()
    .AddSandboxOptions(builder.Configuration)
    .AddSandboxGlobalConfig()
    .AddOrchestratorGlobalConfig()
    .AddSlackAdapter()
    .AddTeamsAdapter()
    .AddIntentHandlers()
    .AddWebhookHandlers()
    .AddLongRunningServices();

await builder.Services.AddJobSpawnerAsync(
    builder.Configuration,
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
