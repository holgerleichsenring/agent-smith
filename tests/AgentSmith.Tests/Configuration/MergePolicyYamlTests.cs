using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Retired;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// 2026-11-17-023fa: the project's merge block loads from YAML with underscored keys. 2026-11-25-9992i: it carries the CI
/// reading keys only; when a person looks is the project's review (end by default), and a file still naming a retired merge
/// key is told so by name.
/// </summary>
public sealed class MergePolicyYamlTests
{
    private const string Head = """
        agents:
          claude: { type: Claude, model: claude-sonnet-4-20250514 }
        secrets: { token: x }
        repos:
          app: { type: GitHub, url: https://github.com/example/app, auth: token }
        trackers:
          app: { type: GitHub, url: https://github.com/example/app, auth: token }
        projects:
          app:
            agent: claude
            tracker: app
            repos: [app]
            pipeline: code

        """;

    // 2026-11-17-3435a
    [Fact]
    public void MergeBlock_Loads_CiEvidenceBounds()
    {
        var project = Load(Head + "    merge:\n      ci_excerpt_chars: 1500\n      ci_failed_checks_read: 2\n");

        project.Merge.CiExcerptChars.Should().Be(1500);
        project.Merge.CiFailedChecksRead.Should().Be(2);
        Load(Head).Merge.CiExcerptChars.Should().Be(4000, "the default when the block says nothing");
    }

    // 2026-11-17-3435b
    [Fact]
    public void MergeBlock_Loads_TheCiLoopsKeys()
    {
        var project = Load(Head + "    merge:\n      ci_poll_seconds: 30\n      ci_settle_seconds: 90\n"
            + "      ci_pending_seconds: 600\n      ci_fix_attempts: 5\n");

        (project.Merge.CiPollSeconds, project.Merge.CiSettleSeconds, project.Merge.CiPendingSeconds).Should().Be((30, 90, 600));
        project.Merge.CiFixAttempts.Should().Be(5); // 2026-11-17-3435c
    }

    // 2026-11-25-9992i
    [Theory]
    [InlineData("    review: each\n", ReviewMode.Each)]
    [InlineData("    review: none\n", ReviewMode.None)]
    [InlineData("    review: end\n", ReviewMode.End)]
    [InlineData("", ReviewMode.End)]
    public void Review_Loads_AndDefaultsToEnd(string line, ReviewMode expected) =>
        Load(Head + line).Review.Should().Be(expected);

    // 2026-11-25-9992i
    [Fact]
    public void Review_AnUnknownValue_IsRefusedByName()
    {
        var findings = new List<StartupFinding>();

        ProjectModeResolver.Review("app", "sometimes", findings).Should().BeNull();

        findings.Should().ContainSingle().Which.Reason.Should().Contain("review 'sometimes'").And.Contain("end, each, none");
    }

    // 2026-11-25-9992i: the loader ignores the retired keys; the retired-key table names each one with review.
    [Fact]
    public void RetiredMergeKeys_AreEachReported_NamingReview()
    {
        const string yaml = """
            projects:
              app:
                merge:
                  when: on_ci_green
                  series_when: manual
                  respect_branch_protection: true
                  refresh_open_pull_requests: true
                  ci_poll_seconds: 30
            """;

        var findings = new RetiredConfigKeyDetector(new RawConfigTreeReader(), new ConfigKeyPathMatcher())
            .InYaml(yaml, "config/agentsmith.yml");

        findings.Select(f => f.Field).Should().BeEquivalentTo("projects.app.merge.when", "projects.app.merge.series_when",
            "projects.app.merge.respect_branch_protection", "projects.app.merge.refresh_open_pull_requests");
        findings.Should().AllSatisfy(f => f.Reason.Should().Contain("review: end | each | none"));
    }

    // 2026-11-25-9992i: a file still naming them loads, at review: end.
    [Fact]
    public void RetiredMergeKeys_AreIgnoredByTheLoader()
    {
        var project = Load(Head + "    merge:\n      when: on_ci_green\n      respect_branch_protection: true\n");

        (project.Review, project.Merge.CiPollSeconds).Should().Be((ReviewMode.End, 60));
    }

    private static ResolvedProject Load(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"merge-policy-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, yaml);
        try
        {
            var loader = new YamlConfigurationLoader(new RawConfigMaterializer(new ProjectConfigNormalizer(),
                new EffectiveTriggerBuilder(), new DeploymentDefaultsApplier(), new ConfigCatalogResolver(),
                new AgentSmithPaths()), new NoOpSystemEventPublisher());
            return loader.LoadConfig(path).Projects["app"];
        }
        finally
        {
            File.Delete(path);
        }
    }
}
