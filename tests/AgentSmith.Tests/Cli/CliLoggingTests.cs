using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Cli;

/// <summary>
/// 2026-11-02-2b26f: one demo run printed 1,148 EF Core SQL statements between its own lines. The CLI
/// keeps Entity Framework at Warning unless --verbose asks for everything.
/// </summary>
public sealed class CliLoggingTests
{
    private const string DbCommandCategory = "Microsoft.EntityFrameworkCore.Database.Command";

    [Fact]
    public void NotVerbose_EfCoreInformation_IsNotLogged()
    {
        var logger = LoggerFor(verbose: false);

        logger.IsEnabled(LogLevel.Information).Should().BeFalse("the run store's SQL is not the run's output");
        logger.IsEnabled(LogLevel.Warning).Should().BeTrue("an EF warning still reaches the operator");
    }

    [Fact]
    public void Verbose_EfCoreInformation_IsLogged()
    {
        LoggerFor(verbose: true).IsEnabled(LogLevel.Information).Should().BeTrue();
    }

    private static ILogger LoggerFor(bool verbose)
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-cli-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, """
            agents:
              default:
                type: claude
                model: sonnet
            """);
        try
        {
            var provider = AgentSmith.Cli.ServiceProviderFactory.Build(
                configPath: path, verbose: verbose, headless: true);
            return provider.GetRequiredService<ILoggerFactory>().CreateLogger(DbCommandCategory);
        }
        finally { File.Delete(path); }
    }
}
