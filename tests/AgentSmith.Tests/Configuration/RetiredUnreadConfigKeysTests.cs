using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Retired;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// 2026-11-28-34d9d: four keys nothing read retire. A file that still sets them loads, and each one it
/// sets is reported once as an advisory; the keys beside them stay read.
/// </summary>
public sealed class RetiredUnreadConfigKeysTests
{
    private const string Yaml = """
        queue:
          max_parallel_jobs: 4
          consume_block_seconds: 5
        trackers:
          ado:
            type: azure_devops
            polling:
              enabled: true
              interval_seconds: 60
              jitter_percent: 10
        agents:
          claude-default:
            type: claude
            compaction:
              is_enabled: true
              threshold_iterations: 8
        projects:
          todolist:
            tracker: ado
            polling:
              enabled: true
              interval_seconds: 60
        """;

    private readonly RetiredConfigKeyDetector _detector = new(new RawConfigTreeReader(), new ConfigKeyPathMatcher());

    [Fact]
    public void RetiredConfigKeys_UnreadKeys_LoadWithAnAdvisory()
    {
        var findings = _detector.InYaml(Yaml, "config/agentsmith.yml");

        findings.Select(f => f.Field).Should().BeEquivalentTo(
            "queue.consume_block_seconds", "trackers.ado.polling.jitter_percent",
            "agents.claude-default.compaction.threshold_iterations", "projects.todolist.polling");
        findings.Should().OnlyContain(f => f.Severity == StartupFindingSeverity.Advisory);
    }

    [Fact]
    public void RetiredConfigKeys_UnreadKeys_TheFileStillBinds()
    {
        var raw = new RawConfigYaml().Deserialize(Yaml);

        raw.Queue.MaxParallelJobs.Should().Be(4);
        raw.Trackers["ado"].Polling!.IntervalSeconds.Should().Be(60);
        raw.Agents["claude-default"].Compaction.IsEnabled.Should().BeTrue();
    }
}
