using AgentSmith.Cli;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0349: the CLI stays purely file-based with ZERO DB dependency in its service
/// graph — a one-shot scan never needs an editable/versioned/DB config. (The new
/// `config import/export` verbs touch the DB deliberately via a hand-built context,
/// exactly like `database migrate`, not through this graph.)
/// </summary>
public sealed class CliFileBasedConfigTests
{
    [Fact]
    public void Cli_RunsPurelyFileBased_NoDbDependency()
    {
        var configPath = Path.Combine(Path.GetTempPath(), $"agentsmith-cli-{Guid.NewGuid():N}.yml");
        File.WriteAllText(configPath, "agents:\n  a: { type: claude, model: sonnet-4 }\n");
        try
        {
            using var services = ServiceProviderFactory.Build(verbose: false, headless: true, configPath: configPath);

            services.GetService<AgentSmithDbContext>().Should().BeNull("the CLI graph wires no DbContext");
            services.GetService<IConfigDocumentStore>().Should().BeNull("no DB config store in the CLI");
            services.GetRequiredService<IConfigStore>().Should().BeOfType<FileConfigStore>(
                "the CLI reads config from the read-only file store");
        }
        finally
        {
            File.Delete(configPath);
        }
    }
}
