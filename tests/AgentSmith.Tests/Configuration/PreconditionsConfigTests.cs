using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// 2026-11-16-f3fbc: preconditions.recheck_interval_seconds binds from agentsmith.yml with a default of 300, and an
/// interval under 60 is refused at load with a message naming the value, the reason and the key.
/// </summary>
public sealed class PreconditionsConfigTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"agentsmith-preconditions-{Guid.NewGuid():N}.yml");

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
    public void Preconditions_Default_Is300AndValid()
    {
        var config = Load("projects: {}\n");

        config.Preconditions.RecheckIntervalSeconds.Should().Be(300);
        PreconditionsConfigValidator.Findings(config).Should().BeEmpty();
    }

    [Fact]
    public void Preconditions_BindsFromYaml()
    {
        Load("projects: {}\npreconditions:\n  recheck_interval_seconds: 600\n").Preconditions.RecheckIntervalSeconds.Should().Be(600);
    }

    [Fact]
    public void Preconditions_Under60_RefusedAtLoadNamingValueReasonAndKey()
    {
        var config = Load("projects: {}\npreconditions:\n  recheck_interval_seconds: 30\n");

        var refusal = new AgentSmithConfigValidator(new SkillsConfigValidator()).Validate(config).Should().ContainSingle().Subject;
        refusal.Should().Contain("preconditions.recheck_interval_seconds").And.Contain("is 30")
            .And.Contain("60").And.Contain("rate-limits");
        PreconditionsConfigValidator.Findings(config).Should().ContainSingle(f => f.IsBlocking && f.Field == "preconditions");
    }

    [Fact]
    public void Preconditions_Exactly60_Passes() =>
        new PreconditionsConfig { RecheckIntervalSeconds = 60 }.Problems().Should().BeEmpty();
}
