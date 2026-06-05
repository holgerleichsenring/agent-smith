using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.Logging;
using AgentSmith.Server.Extensions;
using Microsoft.Extensions.Logging.Console;

const string DashboardCorsPolicy = DashboardConstants.CorsPolicy;

DispatcherBanner.Print();

var builder = WebApplication.CreateBuilder(args);

// p0137d: log filters + minimum level moved to appsettings.json + appsettings.Development.json.
// Console formatter wiring stays here — it's code-shape config (selecting CompactConsoleFormatter
// over the default one), not log-level filtering.
ServerCompositionBuilder.ConfigureConsoleLogging(builder.Logging);

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
// p0199: every IServiceCollection.Add* call moves into the shared
// ServerCompositionBuilder so the real-composition harness builds an
// identical DI graph. Web-only concerns (AddSandboxOptions binds against
// builder.Configuration, AddJobSpawnerAsync) stay in Program.cs.
ServerCompositionBuilder.ConfigureServices(builder.Services, configPath);
builder.Services.AddSandboxOptions(builder.Configuration);

// p0169a: dashboard API. Gated by AGENTSMITH_UI_API_ENABLED env (default on);
// operators that ship without the dashboard can flip it off via env-var.
var uiApiEnabled = !string.Equals(
    Environment.GetEnvironmentVariable("AGENTSMITH_UI_API_ENABLED"), "false",
    StringComparison.OrdinalIgnoreCase);

if (uiApiEnabled)
{
    var dashboardOrigin = Environment.GetEnvironmentVariable("AGENTSMITH_DASHBOARD_ORIGIN")
        ?? "http://localhost:3000";
    builder.Services.AddCors(o => o.AddPolicy(DashboardCorsPolicy, p => p
        .WithOrigins(dashboardOrigin)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

    builder.Services.AddSignalR();
    builder.Services.AddSingleton<SandboxExpansionRegistry>();
    builder.Services.AddSingleton<JobsBroadcaster>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<JobsBroadcaster>());
    builder.Services.AddSingleton<IRunEventFanout, JobsHubFanout>();
    builder.Services.AddSingleton<TrailReader>();
    builder.Services.AddSingleton<ResultMarkdownReader>();
    builder.Services.AddSingleton<PlanMarkdownReader>();
    builder.Services.AddSingleton<AgentSmith.Server.Services.Catalog.CatalogContentsReader>();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(o =>
    {
        o.SwaggerDoc("v1", new() { Title = "agent-smith", Version = "v1" });
        o.DocInclusionPredicate((_, api) =>
            api.RelativePath?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true);
    });
}

await builder.Services.AddJobSpawnerAsync(
    builder.Configuration,
    LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Startup"));

var app = builder.Build();

app.MapHealthEndpoints()
   .MapSlackEndpoints()
   .MapTeamsEndpoints()
   .MapWebhookEndpoints()
   .MapRunControlEndpoints();

if (uiApiEnabled)
{
    app.UseCors(DashboardCorsPolicy);
    app.MapHub<JobsHub>("/hub/jobs");
    app.MapCatalogEndpoints();
    app.UseSwagger(o => o.RouteTemplate = "api/openapi/{documentName}.json");
    app.MapGet("/api/openapi.json", (HttpContext ctx) =>
        Results.Redirect("/api/openapi/v1.json", permanent: false))
       .ExcludeFromDescription();
}

var startupConfig = app.Services.GetRequiredService<IConfigurationLoader>()
    .LoadConfig(configPath);

var validationErrors = app.Services
    .GetRequiredService<AgentSmithConfigValidator>()
    .Validate(startupConfig);
if (validationErrors.Count > 0)
{
    var logger = app.Services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("AgentSmith.Server.Startup");
    foreach (var err in validationErrors) logger.LogError("Config: {Error}", err);
    throw new InvalidOperationException(
        $"agentsmith.yml has {validationErrors.Count} validation error(s); see startup log.");
}

app.Services.GetRequiredService<PollingConfigDeprecationWarner>().Warn(startupConfig);

StartupSummaryLogger.Log(
    startupConfig,
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AgentSmith.Server.Startup"));

app.Run();

public partial class Program;
