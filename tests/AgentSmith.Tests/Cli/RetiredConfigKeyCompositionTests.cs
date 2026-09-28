using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Cli;

/// <summary>
/// The retired-key detector reaches a real load only if the composition hands it to the loader —
/// both are optional constructor parameters, so a missing registration is silence, not an error.
/// This loads a file through the CLI's own container and reads the finding back.
/// </summary>
public sealed class RetiredConfigKeyCompositionTests
{
    [Fact]
    public void CliLoad_AFileSettingParentLinkType_RecordsTheAdvisory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-retired-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, """
            agents:
              default:
                type: claude
                model: sonnet
            trackers:
              jira-main:
                type: jira
                auth: token
                parent_link_type: Relates
            """);
        try
        {
            using var provider = AgentSmith.Cli.ServiceProviderFactory.Build(
                configPath: path, verbose: false, headless: true);
            provider.GetRequiredService<AgentSmithConfig>();

            provider.GetRequiredService<IStartupFindings>().All.Should().ContainSingle(
                    f => f.Field == "trackers.jira-main.parent_link_type")
                .Which.IsBlocking.Should().BeFalse();
        }
        finally { File.Delete(path); }
    }
}
