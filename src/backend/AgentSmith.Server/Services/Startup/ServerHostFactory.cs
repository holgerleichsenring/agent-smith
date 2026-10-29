using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// p0391a: builds the server. The diagnostic surface comes up FIRST and unconditionally —
/// the host, the health endpoint and the findings route need no database, no queue and no
/// valid configuration. Every optional dependency is composed behind a probe that records a
/// finding, so the only failure that can stop this method is the listener failing to bind
/// its port, and then there is no channel left to report through anyway.
///
/// Program.cs delegates here so the boot-under-failure tests build the REAL composition.
/// A throw reintroduced into a startup path fails those tests.
/// </summary>
public static class ServerHostFactory
{
    private const string ConfigPathEnvVar = "CONFIG_PATH";
    private const string DefaultConfigPath = "/app/config/agentsmith.yml";

    public static Task<WebApplication> CreateAsync(
        string[] args, Action<IServiceCollection>? substitutions = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        var auth = TokenAuthority();
        ConfigureHost(builder);
        ConfigureServices(builder, auth);
        builder.Services.AddServerPreflight(auth).AddStartupProbes().AddBuildIdentity().AddFailureReasons();
        // A test substitutes an absent dependency here; production passes nothing.
        substitutions?.Invoke(builder.Services);

        var app = builder.Build();
        app.UseFailureReasons().MapServerEndpoints();
        if (DashboardApiExtensions.IsEnabled) app.MapDashboardApi();
        // p0503b: after the map calls, because MapDashboardApi is where UseCors is
        // registered and a preflight must be answered before anything can refuse it.
        app.UseServerAuthentication(auth).MigrateRoleMapping().MigratePipelineAliases().MigrateLegacyTriggersAfterStart();
        app.RunProbesAfterStart();
        return Task.FromResult(app);
    }

    /// <summary>
    /// p0503b: the auth block is needed BEFORE Build(), which is before the container that
    /// registers the bootstrap reader exists — so the reader is used directly here. The
    /// findings this read would record are recorded again by the DI-resolved BootstrapConfig
    /// during the startup probes, into the list the findings endpoint serves; they collapse
    /// by identity, so the operator sees one finding rather than two.
    /// </summary>
    private static TokenAuthorityConfig? TokenAuthority() =>
        new BootstrapConfigReader(
            new EnvConfigStoreLocation(), new RawConfigYaml(),
            new AuthEnvironmentOverlay(), new PersistenceEnvironmentOverlay())
            .Read().Auth;

    private static void ConfigureServices(WebApplicationBuilder builder, TokenAuthorityConfig? auth)
    {
        // p0199: every IServiceCollection.Add* call lives in the shared
        // ServerCompositionBuilder so the real-composition harness builds an identical
        // DI graph. Web-only concerns stay here.
        ServerCompositionBuilder.ConfigureServices(builder.Services, ConfigPath());
        builder.Services.AddSandboxOptions(builder.Configuration);
        if (DashboardApiExtensions.IsEnabled) builder.Services.AddDashboardApi();
        builder.Services.AddServerAuthentication(auth);
    }

    private static void ConfigureHost(WebApplicationBuilder builder)
    {
        // p0137d: log filters + minimum level live in appsettings.json. Console formatter
        // wiring stays here — it is code-shape config, not log-level filtering.
        ServerCompositionBuilder.ConfigureConsoleLogging(builder.Logging);
        // p0137b: single composition-root AddHttpClient() — registers IHttpClientFactory +
        // named-options infrastructure that per-feature extensions add typed clients onto.
        builder.Services.AddHttpClient();
        // p0391a: a background service that dies must not take the process with it. The
        // default (StopHost) turns any unhandled fault in a leader loop into a dead server
        // that cannot report why the loop died.
        builder.Services.Configure<HostOptions>(o =>
            o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
        builder.Host.UseDefaultServiceProvider(o =>
        {
            o.ValidateScopes = true;
            o.ValidateOnBuild = string.Equals(
                Environment.GetEnvironmentVariable("AGENTSMITH_VALIDATE_DI"), "true",
                StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string ConfigPath() =>
        Environment.GetEnvironmentVariable(ConfigPathEnvVar) ?? DefaultConfigPath;
}
