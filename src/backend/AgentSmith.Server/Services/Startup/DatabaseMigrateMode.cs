using AgentSmith.Infrastructure.Migration.Extensions;
using AgentSmith.Infrastructure.Migration.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// 2026-11-22-747db: `AgentSmith.Server.dll database migrate [--config path]` — the server image runs the same
/// migrate body as the CLI (<see cref="StoreMigrateRun"/>) and exits, so an init container pulls the image the server
/// container needs anyway instead of the SDK-based CLI image. Read before the web host is built: CreateBuilder would
/// take the words as configuration.
/// </summary>
public sealed class DatabaseMigrateMode
{
    private const string ConfigOption = "--config";
    private const string DefaultConfigPath = "/app/config/agentsmith.yml";
    private const string EfCoreCategory = "Microsoft.EntityFrameworkCore";

    /// <summary>Whether the arguments ask for migrate mode rather than the server.</summary>
    public static bool Matches(string[] args) =>
        args.Length >= 2 && args[0] == "database" && args[1] == "migrate";

    /// <summary>Migrates and answers the process exit code.</summary>
    public async Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var services = new ServiceCollection().AddLogging(builder =>
        {
            ServerCompositionBuilder.ConfigureConsoleLogging(builder);
            builder.AddFilter(EfCoreCategory, LogLevel.Warning); // the SQL of every statement is not the operator's news
        });
        await using var provider = services.AddStoreMigrate(ConfigPath(args)).BuildServiceProvider();
        return await provider.GetRequiredService<StoreMigrateRun>().RunAsync(CancellationToken.None);
    }

    /// <summary>The --config value (either form), else CONFIG_PATH, else the image's mount point.</summary>
    public string ConfigPath(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == ConfigOption && i + 1 < args.Length) return args[i + 1];
            if (args[i].StartsWith(ConfigOption + "=", StringComparison.Ordinal)) return args[i][(ConfigOption.Length + 1)..];
        }
        return Environment.GetEnvironmentVariable("CONFIG_PATH") ?? DefaultConfigPath;
    }
}
