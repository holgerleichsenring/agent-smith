using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Logging;
using Microsoft.Extensions.Logging.Console;

namespace AgentSmith.Server.Services;

/// <summary>
/// p0199: single source of truth for the Server's DI composition. Program.cs
/// delegates the service-registration chain to <see cref="ConfigureServices"/>
/// so the real-composition pipeline harness can build the identical
/// ServiceCollection and run handlers exactly as production does. Diverging
/// the two compositions reintroduces the p0198 class of bug — DI wiring that
/// works in unit tests but not in production.
///
/// What stays in Program.cs: the WebApplication-specific concerns (logging
/// formatter, env-var validation, AddJobSpawnerAsync which needs builder.
/// Configuration, dashboard endpoint mapping, app.Run()). What moves here:
/// every IServiceCollection.Add* call that a non-WebApplication consumer
/// (the harness, future CLI shapes) needs to mirror.
/// </summary>
public static class ServerCompositionBuilder
{
    public static IServiceCollection ConfigureServices(
        IServiceCollection services, string configPath)
    {
        services.AddSingleton(new ServerContext(configPath));
        services.AddSingleton<IProgressReporter>(sp =>
            new ConsoleProgressReporter(
                sp.GetRequiredService<ILogger<ConsoleProgressReporter>>(), headless: true));

        services
            .AddRedis()
            .AddCoreDispatcherServices()
            .AddServerCompositionOverrides()
            // p0246b: opt-in relational lease (swaps the NoOp lease for the
            // DB-backed guard when AGENTSMITH_PERSISTENCE_PROVIDER is set). Must
            // run AFTER the overrides registered the NoOp default so RemoveAll wins.
            .AddRelationalPersistence()
            .AddSandbox()
            .AddSandboxGlobalConfig()
            .AddOrchestratorGlobalConfig()
            .AddSlackAdapter()
            .AddTeamsAdapter()
            .AddIntentHandlers()
            .AddWebhookHandlers()
            .AddLongRunningServices();

        // p0198-followup-2: AddAgentSmithCore (inside AddCoreDispatcherServices)
        // registers AgentSmithConfig.Empty() as a placeholder. Override AFTER
        // the core chain so last-binding-wins makes the loaded config win.
        // Putting the override BEFORE the core chain lets the placeholder
        // overwrite us — that was the bug in p0198-followup v1.
        services.AddSingleton<AgentSmithConfig>(sp =>
            sp.GetRequiredService<IConfigurationLoader>().LoadConfig(configPath));

        return services;
    }

    public static void ConfigureConsoleLogging(ILoggingBuilder logging)
    {
        logging.AddConsole(options =>
            options.FormatterName = CompactConsoleFormatter.FormatterName);
        logging.AddConsoleFormatter<CompactConsoleFormatter, ConsoleFormatterOptions>(
            options => options.IncludeScopes = true);
    }
}
