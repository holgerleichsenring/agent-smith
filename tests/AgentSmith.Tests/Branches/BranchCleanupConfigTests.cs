using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Branches;

/// <summary>2026-11-17-713ec: branch_cleanup binds from agentsmith.yml (on, every 86400 seconds by default), and an
/// interval of zero or less is refused at load naming the value, the reason and the key.</summary>
public sealed class BranchCleanupConfigTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"agentsmith-branch-cleanup-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    private AgentSmithConfig Load(string yaml)
    {
        File.WriteAllText(_tempFile, yaml);
        return new YamlConfigurationLoader(new RawConfigMaterializer(new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(),
            new DeploymentDefaultsApplier(), new ConfigCatalogResolver(), new AgentSmithPaths()), new NoOpSystemEventPublisher())
            .LoadConfig(_tempFile);
    }

    [Fact]
    public void BranchCleanup_Default_IsOnDailyAndValid()
    {
        var config = Load("projects: {}\n");

        config.BranchCleanup.Enabled.Should().BeTrue();
        config.BranchCleanup.IntervalSeconds.Should().Be(86400);
        BranchCleanupConfigValidator.Findings(config).Should().BeEmpty();
    }

    [Fact]
    public void BranchCleanup_BindsFromYaml()
    {
        var cleanup = Load("projects: {}\nbranch_cleanup:\n  enabled: false\n  interval_seconds: 7200\n").BranchCleanup;

        cleanup.Enabled.Should().BeFalse();
        cleanup.IntervalSeconds.Should().Be(7200);
    }

    [Fact]
    public void BranchCleanup_ZeroInterval_RefusedAtLoadNamingValueReasonAndKey()
    {
        var config = Load("projects: {}\nbranch_cleanup:\n  interval_seconds: 0\n");

        var refusal = new AgentSmithConfigValidator(new SkillsConfigValidator()).Validate(config).Should().ContainSingle().Subject;
        refusal.Should().Contain("branch_cleanup.interval_seconds").And.Contain("is 0").And.Contain("positive").And.Contain("no interval");
        BranchCleanupConfigValidator.Findings(config).Should().ContainSingle(f => f.IsBlocking && f.Field == "branch_cleanup");
    }

    [Fact]
    public void BranchCleanup_AShortInterval_IsTheOperatorsChoice() =>
        new BranchCleanupConfig { IntervalSeconds = 60 }.Problems().Should().BeEmpty("no floor beyond a positive interval");
}
