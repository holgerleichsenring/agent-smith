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

// Scope validation is always on (cheap, runtime). ValidateOnBuild is NOT: it eagerly
// instantiates EVERY singleton at app.Build() to validate the graph — Redis connect, the
// k8s client (InClusterConfig), the DbContext, the sandbox factory, every probe/reader —
// all BEFORE the API can listen. That is a dev-only guard (DI-lifetime violations), and the
// running server never needs it: ServerDiLifetimeTests already builds the full graph with
// ValidateOnBuild=true. Tying it to IsDevelopment() made a Development-env production server
// pay the whole eager-resolution cost on every startup. Now opt-in only, default off, so the
// API comes up fast regardless of ASPNETCORE_ENVIRONMENT.
builder.Host.UseDefaultServiceProvider(o =>
{
    o.ValidateScopes = true;
    o.ValidateOnBuild = string.Equals(
        Environment.GetEnvironmentVariable("AGENTSMITH_VALIDATE_DI"), "true",
        StringComparison.OrdinalIgnoreCase);
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
    // Comma-separated list so several dashboard origins (staging + prod, or a
    // reverse-proxy host) can be allowed at once — the env-var was a single
    // origin, which silently broke any dashboard not on :3000.
    var dashboardOrigins = (Environment.GetEnvironmentVariable("AGENTSMITH_DASHBOARD_ORIGIN")
            ?? "http://localhost:3000")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    // The dashboard is an operator-local tool, so always allow loopback origins (whatever port
    // the dev-server hops to: 3000/3001/3002…) plus any explicitly configured
    // AGENTSMITH_DASHBOARD_ORIGIN. A loopback origin is the operator's own machine; a hostile
    // website cannot forge it, so this is safe regardless of environment.
    builder.Services.AddCors(o => o.AddPolicy(DashboardCorsPolicy, p => p
        .AllowAnyHeader().AllowAnyMethod().AllowCredentials()
        .SetIsOriginAllowed(origin =>
            dashboardOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)
            || (Uri.TryCreate(origin, UriKind.Absolute, out var u)
                && (u.IsLoopback || u.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))))));

    builder.Services.AddSignalR();
    builder.Services.AddSingleton<SandboxExpansionRegistry>();
    builder.Services.AddSingleton<JobsBroadcaster>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<JobsBroadcaster>());
    // p0246c: fan out to the SignalR hub AND (when persistence is on) project
    // every structured event to the DB. CompositeRunEventFanout resolves the
    // RunDbProjector optionally — null when persistence is off → pure passthrough.
    builder.Services.AddSingleton<JobsHubFanout>();
    builder.Services.AddSingleton<IRunEventFanout>(sp => new CompositeRunEventFanout(
        sp.GetRequiredService<JobsHubFanout>(),
        sp.GetService<AgentSmith.Infrastructure.Persistence.Services.RunDbProjector>()));
    builder.Services.AddSingleton<TrailReader>();
    builder.Services.AddSingleton<ResultMarkdownReader>();
    builder.Services.AddSingleton<PlanMarkdownReader>();
    builder.Services.AddSingleton<AnalyzeMarkdownReader>();
    builder.Services.AddSingleton<AgentSmith.Server.Services.Catalog.CatalogContentsReader>();
    builder.Services.AddSingleton<
        AgentSmith.Server.Services.Diagnostics.IInfraConnectivityProbe,
        AgentSmith.Server.Services.Diagnostics.InfraConnectivityProbe>();
    builder.Services.AddSingleton<
        AgentSmith.Server.Services.Diagnostics.IChatConnectivityProbe,
        AgentSmith.Server.Services.Diagnostics.ChatConnectivityProbe>();
    builder.Services.AddSingleton<
        AgentSmith.Server.Services.Diagnostics.IConnectionDiagnosticsService,
        AgentSmith.Server.Services.Diagnostics.ConnectionDiagnosticsService>();

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
    app.MapRunQueryEndpoints();
    app.MapCatalogEndpoints();
    app.MapConfigQueryEndpoints();
    app.MapDiagnosticsEndpoints();
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
