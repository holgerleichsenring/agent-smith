using AgentSmith.Cli;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0349: the CLI's CONFIGURATION stays purely file-based — a one-shot scan never needs
/// an editable/versioned/DB config. (The `config import/export` verbs touch the DB
/// deliberately via a hand-built context, exactly like `database migrate`, not through
/// this graph.)
/// <para>
/// p0423 narrowed the rule to what it was always about. The CLI now wires a DbContext,
/// because a run must WRITE ITSELF DOWN and its previous publisher was a no-op — twelve
/// hours of live debugging against a run database of zero bytes. Reading config from a
/// database and recording a run into one are different concerns; only the first is
/// forbidden here.
/// </para>
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

            services.GetService<IConfigDocumentStore>().Should().BeNull("no DB config store in the CLI");
            services.GetRequiredService<IConfigStore>().Should().BeOfType<FileConfigStore>(
                "the CLI reads config from the read-only file store");
            services.GetService<AgentSmithDbContext>().Should().NotBeNull(
                "p0423: the run record is not config — a run that writes nothing down "
                + "makes every diagnostic question cost another run");
        }
        finally
        {
            File.Delete(configPath);
        }
    }
}
