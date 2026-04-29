using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services;
using AgentSmith.Server.Extensions;

DispatcherBanner.Print();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(
    builder.Environment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information);

var configPath = Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "/app/config/agentsmith.yml";
builder.Services.AddSingleton(new ServerContext(configPath));
builder.Services.AddSingleton<IProgressReporter>(sp =>
    new ConsoleProgressReporter(
        sp.GetRequiredService<ILogger<ConsoleProgressReporter>>(), headless: true));

builder.Services
    .AddRedis()
    .AddCoreDispatcherServices()
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
